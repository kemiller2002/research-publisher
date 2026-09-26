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
    // A key that is present with the value null is malformed, never "absent"
    // (Praxis contract revision 1.1, "Null is not absence").
    present: Object.prototype.hasOwnProperty.call(source, "provenance"),
    provenance: source.provenance,
    lineage: lineageField === undefined ? undefined : source[lineageField]
  };
}

/**
 * A scalar is exactly one value (`author: "Doe, Jane"` is one author); only a real
 * YAML/JSON list holds several. Nothing is split on commas.
 */
function toValueList(value) {
  if (isAbsent(value)) {
    return [];
  }
  const items = Array.isArray(value) ? value : [value];
  return items
    .filter((item) => !isAbsent(item))
    .map((item) => (typeof item === "string" ? item : typeof item === "object" ? JSON.stringify(item) : String(item)).trim())
    .filter(Boolean);
}

const unique = (items) => items.filter((item, index) => items.indexOf(item) === index);

/**
 * Classifies a received provenance value with the Praxis rules and decides what is
 * published. `present` says whether the source declared the field at all (a declared
 * `null` is malformed, not absent). Returns:
 * - absent: `provenance: null`, `provenanceStatus: null`, `provenanceWithheld: false`
 *   (unattributed; nothing is inferred);
 * - supported / unsupported: the block verbatim, its verdict, `provenanceWithheld: false`;
 * - malformed: NO `provenance` key at all, `provenanceWithheld: true`, and the verdict
 *   with its problems, so a consumer can never mistake a rejected block for "unattributed".
 */
export function describeProvenance(received, present = received !== undefined) {
  if (!present) {
    return { provenance: null, provenanceWithheld: false, provenanceStatus: null };
  }
  const result = classify(received);
  if (result.verdict === "malformed") {
    return {
      provenanceWithheld: true,
      provenanceStatus: { verdict: "malformed", schema: null, warnings: [], problems: [...result.problems] }
    };
  }
  return {
    provenance: clone(received),
    provenanceWithheld: false,
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
  const fromBlock = described.provenanceStatus?.verdict === "supported" ? toValueList(described.provenance.derivedFrom) : [];
  return unique([...toValueList(lineage), ...fromBlock]);
}

/** Legacy free-text authorship claims, labelled as self-declared and unverified. */
export function selfDeclaredAuthorsOf(frontmatter) {
  return legacyAuthorFields.flatMap((field) =>
    toValueList(isObject(frontmatter) ? frontmatter[field] : undefined).map((value) => ({
      field,
      value,
      status: SELF_DECLARED_UNVERIFIED
    }))
  );
}

/** All provenance-related fields of a normalized record. */
export function provenanceFields(parsed) {
  const source = readProvenanceSource(parsed);
  const described = describeProvenance(source.provenance, source.present);
  return {
    ...described,
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
      message: `Provenance was rejected and withheld from the catalog (provenanceWithheld: true): ${status.problems.join("; ")}`
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
