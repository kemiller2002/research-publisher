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
// - lineage (`derived_from`) is recorded separately from authorship, and every
//   reference goes through the vendored `addLineage` check (contract 1.2): a refused
//   lineage is withheld visibly (`lineageWithheld`, diagnostic), never dropped silently;
// - JSON front matter is classified as text (`classifyText`), so a repeated member name
//   or an unpaired surrogate is malformed, never last-one-wins (contract 1.2);
// - "blank" means blank after trimming ASCII whitespace only (contract 1.2);
// - legacy free-text author fields are published only as self-declared,
//   unverified claims and never converted into structured provenance.
import YAML from "yaml";
import { SCHEMA_TAG, addLineage, classify, classifyText, emptyBlock } from "../vendor/praxis/provenance-interchange.mjs";
import { memberTexts, topLevelMembers } from "./json-text.mjs";

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
const hasOwn = (value, key) => isObject(value) && Object.prototype.hasOwnProperty.call(value, key);
/** Contract 1.2: trim only tab, LF, VT, FF, CR and space; every other character is content. */
export const asciiTrim = (value) => value.replace(/^[\t\n\v\f\r ]+|[\t\n\v\f\r ]+$/g, "");
const repeated = (name) => `${name}: member name repeated within one object`;

/**
 * Re-reads the front matter with a YAML 1.2 core-schema reader so provenance keeps
 * its exact serialized values. gray-matter's YAML 1.1 reader turns ISO timestamps
 * into Date objects, which would change `at`/`last` and break byte equivalence.
 * Returns `undefined` when there is no raw front matter or it cannot be re-read.
 */
function faithfulFrontmatter(parsed) {
  if (typeof parsed.frontmatterText !== "string" || asciiTrim(parsed.frontmatterText) === "") {
    return undefined;
  }
  try {
    const value = YAML.parse(parsed.frontmatterText);
    return isObject(value) ? value : undefined;
  } catch {
    return undefined;
  }
}

/**
 * JSON front matter (`---json`): the provenance member is taken from the text as raw
 * JSON text, so it is classified with `classifyText`; a repeated `provenance` or lineage
 * member is malformed instead of the last one silently winning.
 */
function readJsonSource(parsed) {
  const members = topLevelMembers(parsed.frontmatterText);
  if (members === undefined) {
    const present = hasOwn(parsed.frontmatter, "provenance");
    return {
      present,
      ...(present ? { provenanceProblems: ["front matter is not a valid JSON object"] } : {}),
      lineage: undefined,
      lineageProblems: lineageFields.some((field) => hasOwn(parsed.frontmatter, field)) ? ["front matter is not a valid JSON object"] : []
    };
  }
  const provenanceTexts = memberTexts(members, "provenance");
  const lineageField = lineageFields.find((field) => memberTexts(members, field).some((text) => text !== "null"));
  const lineageTexts = lineageField === undefined ? [] : memberTexts(members, lineageField);
  return {
    present: provenanceTexts.length > 0,
    ...(provenanceTexts.length === 1 ? { provenanceText: provenanceTexts[0] } : {}),
    ...(provenanceTexts.length > 1 ? { provenanceProblems: [repeated("provenance")] } : {}),
    lineage: lineageTexts.length === 1 ? JSON.parse(lineageTexts[0]) : undefined,
    lineageProblems: lineageTexts.length > 1 ? [repeated(lineageField)] : []
  };
}

/** The received provenance value and lineage, read as faithfully as the input allows. */
export function readProvenanceSource(parsed) {
  if (parsed.frontmatterLanguage === "json") {
    return readJsonSource(parsed);
  }
  const faithful = faithfulFrontmatter(parsed);
  const source = faithful ?? parsed.frontmatter ?? {};
  const lineageField = lineageFields.find((field) => !isAbsent(source[field]));
  return {
    // A key that is present with the value null is malformed, never "absent"
    // (Praxis contract revision 1.1, "Null is not absence").
    present: Object.prototype.hasOwnProperty.call(source, "provenance"),
    provenance: source.provenance,
    lineage: lineageField === undefined ? undefined : source[lineageField],
    lineageProblems: []
  };
}

/**
 * A scalar is exactly one value (`author: "Doe, Jane"` is one author); only a real
 * YAML/JSON list holds several. Nothing is split on commas. Used for self-declared
 * authors only; lineage goes through `lineageReferences` and `addLineage`.
 */
function toValueList(value) {
  if (isAbsent(value)) {
    return [];
  }
  const items = Array.isArray(value) ? value : [value];
  return items
    .filter((item) => !isAbsent(item))
    .map((item) => asciiTrim(typeof item === "string" ? item : typeof item === "object" ? JSON.stringify(item) : String(item)))
    .filter((item) => item.length > 0);
}

/**
 * Lineage references exactly as received: a scalar is one reference, a list holds
 * several. Nothing is converted, trimmed or filtered: a non-string (number, null,
 * object) or blank reference reaches `addLineage`, which refuses it visibly instead of
 * it being dropped silently (contract 1.2).
 */
function lineageReferences(value) {
  if (value === undefined) {
    return [];
  }
  return Array.isArray(value) ? [...value] : [value];
}

const withoutLineage = ({ derivedFrom: _ignored, ...block }) => block;

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
  return fromVerdict(classify(received), received);
}

/**
 * Classifies provenance received as JSON text with the vendored `classifyText`
 * (contract 1.2): invalid JSON, a repeated member name in any object, or an unpaired
 * surrogate is malformed, whatever the major version. Never throws.
 */
export function describeProvenanceText(text) {
  const result = classifyText(text);
  return result.verdict === "malformed" ? withheld(result.problems) : fromVerdict(result, JSON.parse(text));
}

const withheld = (problems) => ({
  provenanceWithheld: true,
  provenanceStatus: { verdict: "malformed", schema: null, warnings: [], problems: [...problems] }
});

function fromVerdict(result, received) {
  if (result.verdict === "malformed") {
    return withheld(result.problems);
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
 *
 * Every reference goes through the vendored `addLineage` (contract 1.2): onto the
 * supported block, or onto an empty block when there is none, so the same rules hold
 * for every record. Duplicates are dropped keeping the first. When the check refuses,
 * nothing is published as lineage: `derivedFrom` is empty, `lineageWithheld` is true
 * and `lineageProblems` says why (a `rejected-lineage` diagnostic follows). Lineage is
 * never dropped silently.
 */
export function describeLineage(lineage, described, problems = []) {
  const refuse = (reasons) => ({ derivedFrom: [], lineageWithheld: true, lineageProblems: [...reasons] });
  if (problems.length > 0) {
    return refuse(problems);
  }
  const supported = described.provenanceStatus?.verdict === "supported";
  const base = supported ? withoutLineage(described.provenance) : emptyBlock();
  const fromBlock = supported ? described.provenance.derivedFrom ?? [] : [];
  const result = addLineage(base, [...lineageReferences(lineage), ...fromBlock]);
  return result.ok
    ? { derivedFrom: [...(result.block.derivedFrom ?? [])], lineageWithheld: false, lineageProblems: [] }
    : refuse([result.error]);
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
  const described = source.provenanceProblems !== undefined
    ? withheld(source.provenanceProblems)
    : source.provenanceText !== undefined
      ? describeProvenanceText(source.provenanceText)
      : describeProvenance(source.provenance, source.present);
  return {
    ...described,
    ...describeLineage(source.lineage, described, source.lineageProblems),
    selfDeclaredAuthors: selfDeclaredAuthorsOf(parsed.frontmatter)
  };
}

/** Build diagnostics for provenance or lineage that could not be published as received. */
export function provenanceDiagnostics(document) {
  const lineage = document.lineageWithheld
    ? [{
      severity: "warning",
      code: "rejected-lineage",
      sourcePath: document.sourcePath,
      message: `Lineage was refused and withheld from the catalog and graph (lineageWithheld: true): ${document.lineageProblems.join("; ")}`
    }]
    : [];
  return [...lineage, ...blockDiagnostics(document)];
}

function blockDiagnostics(document) {
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
 * The Praxis-compatible interchange block for a published record, as a result in the
 * shape of the vendored `addLineage`: `{ ok: true, block }` or `{ ok: false, error }`.
 * - supported: the published block tagged `praxis.provenance/1`, with the record's
 *   lineage added through `addLineage` (every received field kept);
 * - unsupported: the received block verbatim (never merged into);
 * - otherwise `block: null` (unattributed or rejected).
 * A record whose lineage was withheld, or whose lineage the check refuses, gives
 * `ok: false`: a block without its lineage is never produced silently.
 */
export function toInterchangeBlock(record) {
  const verdict = record.provenanceStatus?.verdict;
  if (verdict === "unsupported") {
    return { ok: true, block: clone(record.provenance) };
  }
  if (verdict !== "supported") {
    return { ok: true, block: null };
  }
  if (record.lineageWithheld) {
    return { ok: false, error: `lineage was withheld: ${record.lineageProblems.join("; ")}` };
  }
  const result = addLineage(withoutLineage(record.provenance), record.derivedFrom ?? []);
  return result.ok
    ? { ok: true, block: { ...result.block, schema: record.provenance.schema ?? SCHEMA_TAG } }
    : { ok: false, error: result.error };
}
