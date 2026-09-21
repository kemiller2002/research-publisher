# E. Requirements Gap Analysis

Target: `input-documents/research-publisher-vnext-requirements.txt` (1,021
lines, 31 sections), committed as `550b1b9`.

**Note on sequencing:** this document did not exist when discovery began. My
search for it ran at ~19:40 UTC and found nothing; the file was committed at
19:49 UTC. The evidence gathered was therefore genuinely independent of the
requirements, which makes the agreements below more meaningful and the conflicts
harder to dismiss.

## E.0 Overall assessment

The draft is careful. Most view requirements are **conditionally phrased** —
"where possible", "where present", "where ROS provides them", "when timestamps
or journals permit". That matters: my central finding is not that the draft is
wrong, but that **most of those conditions are not met by the corpus today**.

The one genuinely unsupported area is §3.3's relationship vocabulary.

## E.1 CONFIRMED

| Req | Requirement | Evidence |
| --- | --- | --- |
| 2.2 | Existing ROS Markdown publishable without manual conversion | Achievable: 116/117 authored docs parse; all 878 relationship instances resolvable; zero duplicate IDs (D.1) |
| 2.4 | Unknown sections preserved, warning only where appropriate | Modelled as `UnknownKeys` + verbatim `Section.Body` (F.8) |
| 2.5 / 2.6 | Parse errors located; one bad artifact must not fail the site | `ParseState`, per-artifact quarantine (G.3) |
| 3.3 | "shall not invent relationships that are not represented in or deterministically derivable from ROS content" | **Strongly confirmed and independently arrived at.** `references` is bibliography, and inferring linkage from it would produce 100% false relationships (C.3) |
| 3.5 | Derived values clearly distinguishable from source-authored | Separate `DerivedRelation` type + `derived: true` (ADR 0009) |
| 6.8 | Question View, "where ROS provides them" | **ROS provides them abundantly:** 520 frontier records are structured open research questions with origin document, challenged assumption and evidence excerpt (B.2) |
| 6.10 | Inspect relationships without opening every Markdown file | This is precisely the 6-edge failure; 878 instances are available |
| 6.11 | Research Health View | Supported: 9 dangling refs, 6 bare filenames, 68 missing ids, orphan counts (C.7) |
| 10.1 / 10.2 | Static-first, GitHub Pages | Measured: 756 docs in 5.6 s; 36 MB output; no mutable state (A.13) |
| 11.4 | No incremental publishing until correctness established | Confirmed and strengthened: at 5.6 s a full rebuild is cheaper than dependency tracking |
| 14.2 | HTML sanitisation | Already present (`rehype-sanitize`); keep at render, preserve raw in the model (R11) |
| 20.1–20.5 | Research integrity principles | Adopted wholesale; ADR 0010 makes 20.5 ("missing information") enforceable |
| 26.1 | No JavaScript requirement for core reading | Static HTML baseline (H.6, ADR 0005) |
| 30.7 | Bidirectional navigation as derived index, not mutation | Backlinks already computed by the current engine — they simply have no data |
| 30.9 | Deterministic derived data | Adopted; `generatedOn` stamps must leave content-addressed files (D.5) |
| 30.10 | Publisher never mutates research | Read-only w.r.t. corpus and ROS state (D.4) |

## E.2 NEEDS REFINEMENT

| Req | Issue | Proposed refinement |
| --- | --- | --- |
| 3.2 | "Every research object that can be referenced independently shall have a stable identifier." **68 of 117 authored documents have no `id`.** The requirement states a desired end-state as though it were a fact | Specify behaviour when identity is absent: a path-derived key, explicitly second-class, never invented, never title-derived (J.2, ADR 0006) |
| 7.3 | "Stable URLs where practical" — too weak given that 58% of current URLs are **title-derived and already unstable** | Make it a hard rule: a public URL must never be a function of a title. Add the blocking build gate on lost URLs (J.3) |
| 7.4 | Deep linking to finding / evidence item / experiment / theory / question | Only the artifact and (best-effort) section levels are addressable today. Frontier records are the exception: 520 are individually addressable |
| 15.1 | "should not require loading the full corpus into browser memory" | Right instinct, wrong diagnosis. The 6.3 MB catalog is large because it **inlines 4.0 MB of rendered HTML**, not because 756 documents are many. Fix by splitting metadata from content (K.2) |
| 15.4 | "shall not assume the corpus will remain small… measured rather than guessed" | Measured: 756 docs, 5.6 s, 36 MB. Small. Architecture stays static; sharding keeps headroom |
| 2.1 | "discover artifacts according to ROS conventions" | ROS conventions **do not locate this corpus**: VE research lives under `content/projects/**`, outside the canonical roots declared in `ros.json` (D.4). Discovery must stay config-driven with ROS as a cross-check |
| 12.6 / 19.3 | Contract stability | Needs an explicit statement on the existing `research-catalog.json` v1.1: shim for one release, then retire (D.3) |

## E.3 CONTRADICTED BY CURRENT ROS

These are the requirements the corpus cannot currently satisfy. Each is
conditionally phrased in the draft, so the honest reading is "the condition is
not met", not "the requirement is wrong".

| Req | Contradiction | Evidence |
| --- | --- | --- |
| **6.3 Finding View** | **There is no finding object.** No sub-document research object carries an identity anywhere in the corpus | B.6, C.4 |
| **6.4 Evidence View** | Evidence *registry documents* exist (5 projects), but no evidence **item** is independently identified or referenced | B.1, B.6 |
| 6.6 Experiment View — "hypothesis, method, result, linked findings, evidence, follow-up" | Experiment reports exist (10 `experiment_report`), but none of those links is encoded. The only typed link out of an experiment is `source_rep` | C.1 |
| 6.7 Theory View — "supporting findings, contradicting findings" | Neither relation exists in the corpus | C.4 |
| **6.9 Timeline View** | **Dates are fabricated.** 735 of 756 records carry `created: "2026-07-22"`, a string constant in `normalize.mjs`. A timeline today would be fiction | A.5 |
| 30.5 Evidence dependency inspection — "if EV-017 is removed, which findings depend on it?" | Not answerable: neither evidence items nor findings are objects, and no dependency relation exists | C.4 |
| 30.6 Traceability `source → evidence → finding → theory` | No link in that chain is encoded | C.4 |
| **31** success: "findings, evidence, experiments, theories, questions, and sources can be navigated as research objects" | Achievable today for **experiments, theories and questions** (documents with ids). **Not** for findings, evidence items or sources | B.6 |

**This is the most consequential result of the discovery.** Roughly half of §6
depends on a sub-document object model that ROS does not currently express.
Either ROS gains one — an authoring change — or those views wait. The publisher
cannot conjure them without inventing IDs, which §3.3, §20.1 and §28 all forbid.

## E.4 UNSUPPORTED ASSUMPTION

| Req | Assumption | Reality |
| --- | --- | --- |
| **3.3** | A 15-relation vocabulary: supports, contradicts, derived from, produced by, references, supersedes, superseded by, answers, raises, validates, invalidates, relates to, depends on, evidence for, evidence against | **Zero of these appear in the corpus.** What exists: `related_documents` (untyped), `source_rep`, `supersedes`/`superseded_by` (present but **null in all 8 occurrences**), plus machine-generated `originates` and `prerequisite`. The draft's list is aspirational vocabulary, not observed structure |
| 2.3 | The parser "shall preserve … findings, open questions, experiments, theories, journal entries, decisions" as distinct things | Those are *document types*, not extractable sub-document structures. The parser can preserve typed **documents**; it cannot preserve findings that are not marked up |
| 6.2 | "Each canonical ROS artifact shall have a readable representation" | True, but under-specifies the hard part: 93.7% of artifacts currently render as one generic type because `document_type` is dropped (A.5) |

## E.5 MISSING FROM REQUIREMENTS

| Missing | Why it matters |
| --- | --- |
| **The frontier pipeline** | `research/frontier/` already produces **624 nodes and 832 edges** plus a 26-field record index — unmentioned anywhere in 1,021 lines. It is the richest research structure in the corpus |
| **The two-population structure** | 69% of published records are machine-generated frontier records, 31% authored research. Every requirement is written as though there is one homogeneous corpus |
| **A prohibition on fabricated defaults** | §20.5 says missing information must be visible, but nothing forbids substituting defaults — which is exactly how 735 fake dates got published |
| **A relationship-recall target** | §31 asks that relationships be understandable; nothing makes recall measurable. Proposed: ≥ 850 edges from the VE corpus (baseline 6) |
| **snake_case reality** | §2.3 lists what to preserve but never says the corpus is predominantly snake_case, which is the single defect that discards most of it |
| **Dangling references must surface** | §5.2 covers reference validation, but the live failure mode is *silent dropping* (`if (byId.has(...))`), which no requirement forbids |
| **Determinism of `dist/`** | `dist/` is committed (1,776 files); §10.4 mentions reproducibility but not that timestamps inside content-addressed files defeat it |

## E.6 Verdict

- **Confirmed:** 16 requirement areas, including every architectural constraint
  (static-first, F#, Limen, no invented research).
- **Needs refinement:** 7, mostly where the draft states a desired end-state as
  fact (§3.2 identity, §7.3 URL stability).
- **Contradicted by the current corpus:** 8, concentrated in §6's
  sub-document-centred views.
- **Unsupported:** 3, chiefly §3.3's relationship vocabulary.
- **Missing:** 7, chiefly the frontier pipeline.

Nothing here argues for weakening the draft. It argues that **half of §6 is
blocked on a ROS authoring decision**, and that decision belongs to the ROS
owner, not the publisher.
