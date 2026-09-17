# E. Requirements Gap Analysis

## E.0 The requirements document does not exist

**OBSERVED.** Searched `research-publisher`, `repository-operating-system` and
`visual-engineering` for any vNext requirements draft: all `*.md`, any filename
matching `*requirement*`, and content matching `vnext` / `v-next` /
`next generation` / `redesign` near `research publisher`. **Zero matches.**

§11 assumes a draft to challenge. There is none. So this deliverable instead
challenges the **implicit requirements** — the design intent actually encoded in
the shipped system and in `docs/research-publisher-architecture.md`, which is
structured as seven hypotheses with stated confidences.

That document is the closest thing to a requirements record that exists, and it
is explicitly falsifiable, so it is the right target.

## E.1 The seven architecture hypotheses, tested against evidence

| # | Hypothesis (stated confidence) | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | A static site is sufficient (0.9) | **CONFIRMED** | 756 docs build in 5.6 s; total output 36 MB; no mutable state or auth anywhere in the corpus |
| 2 | Astro is preferable to a custom generator (0.81) | **CONTRADICTED BY THE NEW CONSTRAINTS** | Not contradicted by the corpus — Astro works. Contradicted by §1.5/§1.6 (F# core, minimal dependencies, no large frontend frameworks). Also the "build boundary between normalization and rendering" it names as a failure mode is where the 6-edge graph is lost |
| 3 | Reuse via package, not copied scaffolding (0.88) | **CONFIRMED** | `visual-engineering` and `fixtures/alt-research` both consume the same engine via config |
| 4 | Pagefind should power full-text search (0.84) | **NEEDS REFINEMENT** | Works, but indexes *rendered HTML*, so search cannot express research semantics — cannot ask "evidence supporting X". 5.3 MB / 813 files for 756 docs |
| 5 | Metadata must be normalized through a schema (0.92) | **CONFIRMED IN PRINCIPLE, FAILED IN PRACTICE** | Normalisation exists but its alias table omits the corpus's dominant keys, so 93.7% of records collapse to `research-document` |
| 6 | Public catalog generated independently of search (0.94) | **CONFIRMED, WITH A DEFECT** | Separation is right; but the catalog inlines 4.0 MB of rendered HTML into a 6.3 MB file, so it is not usable as a metadata contract |
| 7 | Templates use semantic slots, not document-specific HTML (0.8) | **UNSUPPORTED ASSUMPTION** | Semantic layouts require semantics. With 708/756 typed `research-document`, every document renders through the same generic slot; the hypothesis has never actually been exercised |

## E.2 Implicit requirements that were wrong

| Assumption | Why it was wrong |
| --- | --- |
| "The repository began empty … no legacy rendering stack to preserve" (architecture doc, Repository Baseline) | True for `research-publisher` in July 2026, **false for the consuming corpus**. `visual-engineering` carries 837 files, 27 artifact-type conventions, two ID grammars and a pre-existing frontier pipeline. The engine was designed against an empty repo and then pointed at a mature one |
| Front matter is camelCase | The corpus is predominantly snake_case (`document_type` 49 vs `artifactType` 4) |
| `status` is a small vocabulary | 37 distinct values, including full English phrases |
| Filenames or titles are adequate identity | 68 of 117 authored docs have no `id`; titles then determine URLs |
| Relationships are ID lists | 40 of 63 relationship values are file paths |
| `references` means internal references | It is bibliography in all 16 files |
| One document = one research object | Frontier records already address *sections* of documents |

## E.3 Requirements missing entirely

| Missing requirement | Evidence it is needed |
| --- | --- |
| **Relationship fidelity target** | 6 edges derived from 878 available. Nothing in the current design states that relationship recall is a success criterion |
| **No fabricated data** | 735/756 records carry a hardcoded `created` date; 742/756 a default `researchArea`. Defaults are indistinguishable from real values in the output |
| **Dangling references must be visible** | `if (byId.has(...))` silently drops them; a live dangling reference in this repo (`DF-2026-001`) went unreported |
| **Bounded agent retrieval** | §18. Today an agent must ingest a 6.3 MB catalog to answer any question |
| **Consume the frontier pipeline** | 832 edges already exist in `frontier-graph.json`; the publisher ignores them |
| **URL stability guarantee** | Title-derived URLs for 58% of authored docs |
| **Sub-document addressability** | Frontier records already reference sections; nothing can link to one |
| **Integrity as a first-class view** | Nine validation rules, none about reference integrity |
| **Two-population awareness** | 69% of published records are machine-generated frontier records; the design does not distinguish them from authored research |
| **Deterministic output** | `generatedOn` timestamps in content-addressed files defeat meaningful diffs on a committed `dist/` |

## E.4 Requirements confirmed and worth carrying forward

- Static-first, GitHub Pages (E.1 #1) — strongly confirmed by measurement.
- Markdown stays canonical (§1.1) — no evidence contradicts it.
- Reuse through a package boundary with per-repo config (E.1 #3).
- Catalog separate from search index (E.1 #6), with the inlining defect fixed.
- Normalisation with explicit diagnostics (E.1 #5) — the idea is right; the
  alias table is the defect.
