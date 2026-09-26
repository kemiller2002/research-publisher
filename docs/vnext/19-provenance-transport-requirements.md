---
id: REQ-RP-PROV
title: Research Publisher Provenance Transport Requirements
version: 1.1.0
status: accepted
created: 2026-09-26
updated: 2026-09-26
related_documents:
  - docs/vnext/15-requirements-v0.2.0.md
  - docs/vnext/adr/0010-no-fabricated-data.md
  - docs/research-metadata-schema.md
tags: [provenance, lineage, echelon, requirements]
provenance:
  contributions:
    EXE-20260926T081158655Z-e537f193:
      operations: [created]
      at: 2026-09-26T08:16:39.549Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Provenance transport requirements (work item FEAT-ECHELON-PROVENANCE)"
    EXE-20260926T085445210Z-730229d0:
      operations: [modified]
      at: 2026-09-26T08:56:51.783Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Adopt Praxis contract revision 1.1 and review findings (work item FEAT-ECHELON-PROVENANCE-R2)"
derived_from: [RQ-ROS-2026-A015]
---

# Provenance Transport Requirements (R14)

Research Publisher publishes Praxis-governed artifacts. It **transports**
provenance; it does not own, define, or reinterpret it. The canonical model is
Praxis `docs/agent-provenance.md` at commit
`a42c44e8ae0e6e16fdd513141460b700e5fa6648`, with these Praxis records as the
authority for every rule below (they are cited, not restated). Version 1.1.0
of this document adopts Praxis contract revision 1.1 (commit
`c2657efb4d54f11d0fd0617cc1bcd5b8418601d5`, section "Contract revision 1.1" of
`docs/agent-provenance.md`):

| Praxis record | Subject |
| --- | --- |
| `RQ-ROS-2026-A008` | Derivation lineage is recorded separately from authorship |
| `RQ-ROS-2026-A009` | Provenance survives integration and export boundaries |
| `RQ-ROS-2026-A015` | Versioned interchange block (`praxis.provenance/1`) with supported / unsupported / malformed receiving rules |
| `RQ-ROS-2026-A019` | Provenance never substitutes for authentication, authorization, or evidence |
| `DF-ROS-2026-A037` | Echelon provenance interchange decision |

These requirements extend `REQ-RP-VNEXT` as section R14 and apply to the
current publisher (`src/`) as well as to vNext.

## Requirements

**R14.1 — Supported provenance is carried verbatim.** A document's front-matter
`provenance` block that classifies as `supported` MUST appear on its record in
every machine-readable catalog (`catalog.json`, `research-catalog.json`) as the
received block, JSON-equivalent including unknown fields, extension fields, and
the exact serialized timestamps. The record MUST also carry
`provenanceStatus` (`verdict`, `schema`, `warnings`, `problems`). Unknown
operation codes are kept and reported as warnings. *(A009, A015)*

**R14.2 — Another major version is carried verbatim and flagged.** A block
tagged with a different `praxis.provenance/<major>` MUST be published verbatim
with `provenanceStatus.verdict = "unsupported"`, MUST NOT be interpreted
(its lineage is not read), merged into, or rewritten, and MUST produce an
`unsupported-provenance-version` warning. *(A015)*

**R14.3 — Malformed provenance is rejected visibly, never silently.** A block
that classifies as `malformed` (including any credential-like value anywhere,
and any field present with the value `null`, including a declared
`provenance: null`) MUST NOT be published. The document itself stays published,
and its record MUST be unambiguous: it carries **no `provenance` key**,
`provenanceWithheld: true`, and `provenanceStatus.verdict = "malformed"` with
its problems. `provenance: null` is reserved for "unattributed" (R14.4), so a
consumer that ignores `provenanceStatus` still cannot read a rejected block as
"no provenance". Every record carries `provenanceWithheld` (`false` unless
withheld). A malformed block also MUST produce a `malformed-provenance` build
diagnostic. The diagnostic is a **warning**, not an error: under R5.2 only
identity, URL, readability, and output-safety defects block a build, and a
repository's research must remain publishable. Diagnostics and status never echo
credential values. *(A015, A019)*

**R14.4 — Absent provenance stays absent.** A document whose front matter has
no `provenance` key publishes `provenance: null`, `provenanceStatus: null` and
`provenanceWithheld: false` (unattributed), with no diagnostic. Nothing is inferred from authors, file names, dates, or Git.
Builds of repositories that have no provenance are unaffected. *(A009;
Praxis legacy rules)*

**R14.5 — Lineage is not authorship.** Front-matter `derived_from` (or
`derivedFrom`), plus `derivedFrom` inside a supported block, MUST be published
as the record's `derivedFrom` list (foreign, namespaced references kept
verbatim; a scalar is exactly one reference and only a real YAML/JSON list holds
several — values are never split on commas) and MUST produce typed `derived-from` edges in `graph.json` /
`research-graph.json` for references that resolve to published documents.
Lineage MUST NOT change either record's contributors. *(A008)*

**R14.6 — Legacy author fields are self-declared and unverified.** Free-text
authorship fields (`authorAgent`, `author`, `author_agent`, `created_by_agent`,
`owner_agent`, `source_author`) MUST be published in `selfDeclaredAuthors` as
`{field, value, status: "self-declared-unverified"}`. A scalar value is one
claim (`author: "Doe, Jane"` is one author); only a real YAML/JSON list yields
several claims. `author_agent` is an alias
of `authorAgent` (R1.2). They MUST NEVER be converted into structured
provenance, and structured provenance MUST NEVER be projected onto them.
*(A019; Praxis legacy rules)*

**R14.7 — Provenance is not authority.** The publisher MUST NOT filter, rank,
order, weight, badge, or hide any artifact because of a recorded actor.
*(A019)*

**R14.8 — The Praxis contract is vendored unchanged.** Classification MUST use
the Praxis reference library vendored unchanged at
`src/vendor/praxis/provenance-interchange.mjs`, and the conformance fixtures
MUST be vendored unchanged at `tests/fixtures/praxis-provenance/`. Each
directory's `SOURCE.json` records the Praxis commit and SHA-256 of every file,
and a test recomputes them. Every conformance case MUST reach its pinned
verdict and warning count. *(A015, DF-A037)*

**R14.9 — Dates are not fabricated.** Absent `created` / `updated` MUST be
`null` rather than a substituted date. *(R2.1, ADR 0010)*

**R14.10 — Presentation is unchanged.** HTML pages MUST NOT be required to show
contributors. Provenance is a machine-readable concern; the public catalog
already carries it. A future "provenance recorded" indicator is optional and
MUST follow R14.7.

**R14.11 — Contract versions.** Adding provenance fields and making `created` /
`updated` nullable changes the published shape, so the catalog and record
`schemaVersion` is **1.2** (was 1.1) and the relationship graph's is **1.1**
(was 1.0; adds the `derived-from` edge type). Under the repository's contract
rule (K.5 in `11-machine-readable-contracts.md`: breaking changes bump the
major) these are minor bumps: every addition is optional to read, and `null`
for absent data was already the declared contract (R2.1, K.5.3, ADR 0010) — the
fabricated date was a defect. Consumers that sort or format dates MUST handle
`null`.

## Public catalog decision

The public `research-catalog.json` includes `provenance`, `provenanceStatus`, `provenanceWithheld`,
`derivedFrom`, and `selfDeclaredAuthors`. The same provenance is already public
in the source repository's front matter, so publishing it discloses nothing new,
while omitting it would strip traceability at the publishing boundary (A009).
Credential-like material cannot reach the catalog because such a block is
malformed and rejected (R14.3).

## Traceability

| Requirement | Implementation | Tests |
| --- | --- | --- |
| R14.1 | `src/metadata/provenance.mjs` (`readProvenanceSource`, `describeProvenance`), `src/metadata/normalize.mjs`, `src/content/parse-document.mjs` (`frontmatterText`) | `tests/unit/provenance.test.mjs` — "keeps a Praxis front-matter block exactly…", "keeps multiple contributors…", "reports unknown operation codes…", "round-trips to a Praxis-compatible interchange block", "carries provenance and lineage into the catalog and graph…" |
| R14.2 | `describeProvenance`, `lineageOf`, `provenanceDiagnostics` | "carries another major version verbatim…", conformance boundary cases |
| R14.3 | `describeProvenance`, `provenanceDiagnostics`, `src/validation/validate.mjs` | "rejects malformed provenance…", "never publishes a credential-like value", build-pipeline test |
| R14.4 | `describeProvenance` | "leaves an artifact without provenance unattributed…", "is unaffected for a repository with no provenance", `tests/unit/normalize.test.mjs` |
| R14.5 | `lineageOf`, `src/relationships/graph.mjs` | "turns derived_from into derivedFrom…", "replays the Echelon chain…", build-pipeline test |
| R14.6 | `selfDeclaredAuthorsOf`, alias in `normalize.mjs` | "publishes free-text author fields as self-declared…", "keeps a Praxis front-matter block exactly…" |
| R14.7 | No actor-dependent code path in `src/build`, `src/validation`, `site/src` | Review; chain test asserts no author projection |
| R14.8 | `src/vendor/praxis/`, `tests/fixtures/praxis-provenance/` | "fixtures are byte-identical…", "the reference library is byte-identical…", 40 × conformance + 40 × publisher-boundary cases |
| R14.9 | `normalize.mjs`, `site/src/pages/index.astro` (null-safe sort) | `normalize.test.mjs`, "is unaffected for a repository with no provenance", `tests/integration/build.test.mjs` |
| R14.3 (rev. 1.1) | `describeProvenance` (`provenanceWithheld`, declared-null detection in `readProvenanceSource`) | "a declared `provenance: null` is malformed…", "null inside a block is malformed…", "exact matching and calendar-valid timestamps…", "a withheld block is distinguishable…", "every record states provenanceWithheld explicitly" |
| R14.5/R14.6 (rev. 1.1) | `toValueList` | "a scalar author field is one self-declared author…", "a real YAML list of authors…", "a scalar derived_from is one lineage reference…" |
| R14.11 | `normalize.mjs`, `project.mjs`, `graph.mjs` | "the catalog and record schema versions are 1.2 and the graph is 1.1", "is unaffected for a repository with no provenance" |
| R14.10 | No layout change | `tests/integration/build.test.mjs` (existing HTML assertions unchanged) |

`toInterchangeBlock(record)` returns the Praxis-compatible
`praxis.provenance/1` block for a published record (supported: tagged, with the
record's lineage; unsupported: verbatim; otherwise `null`).
