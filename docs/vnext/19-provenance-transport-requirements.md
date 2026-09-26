---
id: REQ-RP-PROV
title: Research Publisher Provenance Transport Requirements
version: 1.0.0
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
derived_from: [RQ-ROS-2026-A015]
---

# Provenance Transport Requirements (R14)

Research Publisher publishes Praxis-governed artifacts. It **transports**
provenance; it does not own, define, or reinterpret it. The canonical model is
Praxis `docs/agent-provenance.md` at commit
`a42c44e8ae0e6e16fdd513141460b700e5fa6648`, with these Praxis records as the
authority for every rule below (they are cited, not restated):

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
that classifies as `malformed` (including any credential-like value anywhere)
MUST NOT be published, MUST leave `provenanceStatus.verdict = "malformed"` with
its problems on the record, and MUST produce a `malformed-provenance` build
diagnostic. The diagnostic is a **warning**, not an error: under R5.2 only
identity, URL, readability, and output-safety defects block a build, and a
repository's research must remain publishable. Diagnostics and status never echo
credential values. *(A015, A019)*

**R14.4 — Absent provenance stays absent.** A document without provenance
publishes `provenance: null` and `provenanceStatus: null` (unattributed), with
no diagnostic. Nothing is inferred from authors, file names, dates, or Git.
Builds of repositories that have no provenance are unaffected. *(A009;
Praxis legacy rules)*

**R14.5 — Lineage is not authorship.** Front-matter `derived_from` (or
`derivedFrom`), plus `derivedFrom` inside a supported block, MUST be published
as the record's `derivedFrom` list (foreign, namespaced references kept
verbatim) and MUST produce typed `derived-from` edges in `graph.json` /
`research-graph.json` for references that resolve to published documents.
Lineage MUST NOT change either record's contributors. *(A008)*

**R14.6 — Legacy author fields are self-declared and unverified.** Free-text
authorship fields (`authorAgent`, `author`, `author_agent`, `created_by_agent`,
`owner_agent`, `source_author`) MUST be published in `selfDeclaredAuthors` as
`{field, value, status: "self-declared-unverified"}`. `author_agent` is an alias
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

## Public catalog decision

The public `research-catalog.json` includes `provenance`, `provenanceStatus`,
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
| R14.10 | No layout change | `tests/integration/build.test.mjs` (existing HTML assertions unchanged) |

`toInterchangeBlock(record)` returns the Praxis-compatible
`praxis.provenance/1` block for a published record (supported: tagged, with the
record's lineage; unsupported: verbatim; otherwise `null`).
