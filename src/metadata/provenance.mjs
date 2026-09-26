// Praxis provenance transport for published research (REQ-RP-PROV, R14).
//
// The publisher carries Praxis-governed provenance; it never owns or redefines it.
// Classification is delegated to the vendored Praxis reference library
// (src/vendor/praxis, unchanged, hash-pinned). Every function here is pure: inputs
// are never mutated and results are fresh plain objects.
//
// Rules implemented (Praxis RQ-ROS-2026-A008/A009/A015/A019, DF-ROS-2026-A037):
// - supported blocks are published verbatim (unknown fields kept);
// - another major version is published verbatim and flagged, never interpreted;
// - a malformed block (including any credential-like value) is not published,
//   and is reported as a build diagnostic instead of being dropped silently;
// - lineage (`derived_from`) is recorded separately from authorship;
// - legacy free-text author fields are published only as self-declared,
//   unverified claims and never converted into structured provenance.
import YAML from "yaml";
import { SCHEMA_TAG, classify } from "../vendor/praxis/provenance-interchange.mjs";

export const SELF_DECLARED_UNVERIFIED = "self-declared-unverified";

/** Free-text authorship fields that predate structured provenance. */
export const legacyAuthorFields = Object.freeze([
  "authorAgent",
  "author",
  "author_agent",
  "created_by_agent",
  "owner_agent",
  "source_author"
]);

const lineageFields = Object.freeze(["derived_from", "derivedFrom"]);

const isObject = (value) => value !== null && typeof value === "object" && !Array.isArray(value);
const clone = (value) => JSON.parse(JSON.stringify(value));
const isAbsent = (value) => value === undefined || value === null;

/**
 * Re-reads the front matter with a YAML 1.2 core-schema reader so provenance keeps
 * its exact serialized values. gray-matter's YAML 1.1 reader turns ISO timestamps
 * into Date objects, which would change `at`/`last` and break byte equivalence.
 * Returns `undefined` when there is no raw front matter or it cannot be re-read.
 */
function faithfulFrontmatter(parsed) {
  if (typeof parsed.frontmatterText !== "string" || parsed.frontmatterText.trim() === "") {
    return undefined;
  }
  try {
    const value = YAML.parse(parsed.frontmatterText);
    return isObject(value) ? value : undefined;
  } catch {
    return undefined;
  }
}

/** The received provenance value and lineage, read as faithfully as the input allows. */
export function readProvenanceSource(parsed) {
  const faithful = faithfulFrontmatter(parsed);
  const source = faithful ?? parsed.frontmatter ?? {};
  const lineageField = lineageFields.find((field) => !isAbsent(source[field]));
  return {
    provenance: source.provenance,
    lineage: lineageField === undefined ? undefined : source[lineageField]
  };
}

function toReferenceList(value) {
  if (isAbsent(value) || value === "") {
    return [];
  }
  const items = Array.isArray(value) ? value : String(value).split(",");
  return items.map((item) => String(item).trim()).filter(Boolean);
}

const unique = (items) => items.filter((item, index) => items.indexOf(item) === index);

/**
 * Classifies a received provenance value with the Praxis rules and decides what is
 * published. Returns `{ provenance, provenanceStatus }`:
 * - absent: both `null` (the artifact reads as unattributed; nothing is inferred);
 * - supported / unsupported: the block verbatim plus its verdict;
 * - malformed: no block (it is rejected at this boundary) plus the verdict and problems.
 */
export function describeProvenance(received) {
  if (isAbsent(received)) {
    return { provenance: null, provenanceStatus: null };
  }
  const result = classify(received);
  if (result.verdict === "malformed") {
    return {
      provenance: null,
      provenanceStatus: { verdict: "malformed", schema: null, warnings: [], problems: [...result.problems] }
    };
  }
  return {
    provenance: clone(received),
    provenanceStatus: {
      verdict: result.verdict,
      schema: result.verdict === "unsupported" ? result.schema : received.schema ?? SCHEMA_TAG,
      warnings: [...result.warnings],
      problems: []
    }
  };
}

/**
 * Lineage references: the front matter's `derived_from` (or `derivedFrom`), then any
 * `derivedFrom` carried inside a supported block. An unsupported block is never read.
 * Lineage is not authorship.
 */
export function lineageOf(lineage, described) {
  const fromBlock = described.provenanceStatus?.verdict === "supported" ? toReferenceList(described.provenance.derivedFrom) : [];
  return unique([...toReferenceList(lineage), ...fromBlock]);
}

/** Legacy free-text authorship claims, labelled as self-declared and unverified. */
export function selfDeclaredAuthorsOf(frontmatter) {
  return legacyAuthorFields.flatMap((field) =>
    toReferenceList(isObject(frontmatter) ? frontmatter[field] : undefined).map((value) => ({
      field,
      value,
      status: SELF_DECLARED_UNVERIFIED
    }))
  );
}

/** All provenance-related fields of a normalized record. */
export function provenanceFields(parsed) {
  const source = readProvenanceSource(parsed);
  const described = describeProvenance(source.provenance);
  return {
    provenance: described.provenance,
    provenanceStatus: described.provenanceStatus,
    derivedFrom: lineageOf(source.lineage, described),
    selfDeclaredAuthors: selfDeclaredAuthorsOf(parsed.frontmatter)
  };
}

/** Build diagnostics for provenance that could not be published as received. */
export function provenanceDiagnostics(document) {
  const status = document.provenanceStatus;
  if (!status) {
    return [];
  }
  if (status.verdict === "malformed") {
    return [{
      severity: "warning",
      code: "malformed-provenance",
      sourcePath: document.sourcePath,
      message: `Provenance was rejected and not published: ${status.problems.join("; ")}`
    }];
  }
  if (status.verdict === "unsupported") {
    return [{
      severity: "warning",
      code: "unsupported-provenance-version",
      sourcePath: document.sourcePath,
      message: `Provenance uses ${status.schema}; it is published verbatim and not interpreted.`
    }];
  }
  return status.warnings.map((warning) => ({
    severity: "warning",
    code: "provenance-unknown-operation",
    sourcePath: document.sourcePath,
    message: warning
  }));
}

/**
 * The Praxis-compatible interchange block for a published record:
 * - supported: the published block tagged `praxis.provenance/1`, with the record's
 *   lineage as `derivedFrom` (every received field kept);
 * - unsupported: the received block verbatim (never merged into);
 * - otherwise `null` (unattributed or rejected).
 */
export function toInterchangeBlock(record) {
  const verdict = record.provenanceStatus?.verdict;
  if (verdict === "unsupported") {
    return clone(record.provenance);
  }
  if (verdict !== "supported") {
    return null;
  }
  const { derivedFrom: _ignored, ...block } = clone(record.provenance);
  return {
    ...block,
    schema: record.provenance.schema ?? SCHEMA_TAG,
    ...(record.derivedFrom.length > 0 ? { derivedFrom: [...record.derivedFrom] } : {})
  };
}
