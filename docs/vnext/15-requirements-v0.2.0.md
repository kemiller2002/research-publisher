---
id: REQ-RP-VNEXT
title: Research Publisher vNext Requirements
version: 0.2.0
status: draft
created: 2026-09-17
updated: 2026-09-17
supersedes: input-documents/research-publisher-vnext-requirements.txt (unversioned draft, 550b1b9)
---

# Research Publisher vNext — Requirements v0.2.0

## Change summary

This **revises** the draft at
`input-documents/research-publisher-vnext-requirements.txt` (1,021 lines, 31
sections, commit `550b1b9`). **That file is unmodified.** This is a separate,
versioned document, per the instruction not to overwrite the prior requirements.

The prior draft arrived after discovery had run (see
`00-discovery-summary.md`), so the evidence below was gathered independently of
it. Full classification of all 31 sections is in
`05-requirements-gap-analysis.md`; answers to its §29 questions are in
`17-requirements-section-29-answers.md`.

### What changed from the prior draft

| Area | Change | Why |
| --- | --- | --- |
| §3.3 relationship vocabulary (15 relations) | **Removed as a requirement; retained as a target vocabulary for ROS authoring** | Zero of the 15 appear in the corpus. What exists is `related_documents` (untyped), `source_rep`, `supersedes`/`superseded_by` (null in all 8 uses), `originates`, `prerequisite` |
| §6.3 Finding View, §6.4 Evidence View, §30.5, §30.6 | **Deferred, blocked on a ROS authoring decision** | No sub-document research object carries an identity. Building them requires inventing IDs, which §3.3, §20.1 and §28 forbid |
| §6.9 Timeline View | **Deferred** | 735 of 756 records carry a fabricated `created` date |
| §3.2 stable identity | **Refined** — defines behaviour when identity is absent | 68 of 117 authored documents have no `id` |
| §7.3 stable URLs | **Strengthened** from "where practical" to a hard prohibition on title-derived URLs | 58% of current URLs are title-derived and already unstable |
| §15.1 initial load | **Re-diagnosed** | The 6.3 MB catalog is large because it inlines 4.0 MB of HTML, not because 756 documents is many |
| §2.1 ROS-convention discovery | **Refined** to config-driven with ROS as cross-check | The VE corpus lives outside the canonical roots declared in `ros.json` |
| — | **Added:** no-fabricated-data rule, relationship-recall target, dangling-reference visibility, frontier-pipeline consumption, two-population awareness, snake_case parsing, determinism | None appear in the prior draft; each addresses a measured defect |

### Carried forward unchanged

Every architectural constraint: ROS Markdown canonical, no forced migration,
SDE/Ordo, Limen, F# core, dependency discipline, static-first GitHub Pages,
integrity principles (§20), agent-friendly contracts (§21), graceful
degradation (§30.8), determinism (§30.9), no hidden mutation (§30.10).

---

## R1 — Corpus compatibility (blocking)

R1.1 Every valid existing ROS artifact **must** publish with **zero** manual
edits. An artifact that cannot be represented is a design defect.

R1.2 The parser **must** accept, at minimum: `document_type` and `artifactType`;
both `-` and `_` casings of type values; `related_documents` and
`relatedDocuments`; file-relative and repo-root-relative paths; free text in
link fields; `research_area` and `researchArea`; `superseded_by`, `source_rep`,
`evidence_level`, `author_agent`, `related_projects`; `date`/`created`/`updated`;
`abstract`/`summary`; absent `id`; absent front matter.

R1.3 Unknown front-matter keys and unknown body sections **must** be preserved
and exposed, never dropped.

R1.4 The publisher **must not** write to any canonical research file, nor to
ROS-owned state (`ros.json`, `registries/`, `.ros/`, `schemas/`).

## R2 — Fidelity (blocking)

R2.1 No value may be fabricated. Absent data is `null` in all output.

R2.2 The relationship graph **must** derive ≥ 850 canonical edges from the
`visual-engineering` corpus. *(Baseline: the current publisher derives 6.)*

R2.3 Every reference that fails to resolve **must** appear as a warning in the
output. Silent dropping is forbidden.

R2.4 Canonical and derived relationships **must** be distinguishable in every
published artifact and in the UI.

## R3 — Identity and URLs

R3.1 IDs **must never** be invented.

R3.2 A public URL **must not** be derived from a title.

R3.3 Artifacts with a declared id get `/a/{id}`; others get a stable
path-derived URL.

R3.4 Every previously published URL **must** be emitted or redirected. Build
fails otherwise. *(blocking)*

R3.5 `/collections/{facet}/{value}/` is preserved.

R3.6 Section anchors are best-effort and **must** degrade to the document URL
rather than 404 or mislead.

## R4 — Architecture

R4.1 F# implements discovery, parsing, domain modelling, validation,
relationship resolution, projection, index generation and tests.

R4.2 The browser application follows Limen: an F#→WASM engine that names no
browser capability, and a thin kernel that owns them.

R4.3 Every artifact is also a static HTML page, readable without WASM.

R4.4 Static output only. No server, no database, no runtime services.

R4.5 Minimal dependencies; no large frontend framework.

R4.6 Output **must** be deterministic: two builds byte-identical; no wall-clock
stamps inside content-addressed artifacts.

## R5 — SDE / Ordo

R5.1 Corpus and artifact lifecycles are modelled as explicit states with legal
transitions; invalid states are unrepresentable.

R5.2 Obligations are classified blocking vs warning. **Only** these block:
duplicate id, duplicate URL, unreadable source, output path escape, lost
published URL.

R5.3 Legitimate incomplete research states (`supersedes: null`, `…-pending`,
`Open`, orphan artifacts) **must never** block or be presented as defects.

R5.4 External effects are explicit and confined to tier 4; no writes before
`Emit`; no network during build.

## R6 — Machine-readable contracts

R6.1 Publish versioned indexes under `/data/v1/` with a `manifest.json` entry
point.

R6.2 Metadata indexes **must not** contain rendered HTML.

R6.3 Bounded retrieval: full context for one artifact in ≤ 2 fetches.

R6.4 Every published fact carries provenance sufficient to locate its source.

R6.5 `research-catalog.json` v1.1 remains as a compatibility shim for one
release.

## R7 — Experience

R7.1 A project landing page shows purpose, open frontier, research position,
in-flight work, and integrity — not a file tree.

R7.2 Views are built **only** where the corpus supports them. Findings,
contradictions and timeline views are excluded until the corpus encodes them.

R7.3 Relationship traversal is reachable in one interaction from any artifact.

R7.4 Source provenance is reachable from every artifact.

R7.5 Shareable state (project, route, artifact, lens, query, filters) lives in
the URL; browser back/forward work.

## R8 — Testing

R8.1 Fixtures are derived from real research and preserve real edge cases;
canonical research is not modified for testing.

R8.2 The golden-corpus test asserts artifact count, edge count by relation,
finding count by code, output size and URL set.

R8.3 Determinism, Limen boundary and URL preservation are CI gates.

## Open questions requiring an owner's decision

| # | Question | Blocks |
| --- | --- | --- |
| Q1 | Are `evidenceIds`/`hypothesisIds`/`theoryIds` the intended canonical typed-link vocabulary? They exist in the engine and seed corpus but **zero** times in real research (C.4) | view design beyond slice 1 |
| Q2 | Do `RFR-` content-hash ids change when a source document is edited? (J.5) | advertising frontier URLs as permanent |
| Q3 | Should the publisher consume `frontier-graph.json` directly, or re-derive from Markdown? | slice 2 |
| Q4 | Is there a requirements draft outside the three inspected repositories? (E.0) | sign-off on this document |
