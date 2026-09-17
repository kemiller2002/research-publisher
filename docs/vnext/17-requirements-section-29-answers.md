# Answers to §29 of the Requirements Draft

The draft lists 25 questions to "become explicit decisions rather than
assumptions". Discovery answers 21 from evidence. Four need a decision from you.

| # | Question | Answer |
| --- | --- | --- |
| 1 | What artifact types does ROS currently define for research? | **OBSERVED.** ROS *schemas* define evidence, experiment, hypothesis, journal, mission, rep, theory. The *corpus* uses 22 `document_type` values and 27 artifact-type directories — a much wider, unenforced set (B.1) |
| 2 | Which relationships are explicit in Markdown today? | **OBSERVED.** Six: `related_documents` (untyped, 5 value kinds), `source_rep`, `supersedes`, `superseded_by` (both null in all 8 uses), plus machine-generated `originates` (520) and `prerequisite` (312) (C.1) |
| 3 | Which can be derived deterministically? | **OBSERVED/PROPOSAL.** Totally: backlinks, same-project, shared-concept, frontier-of, supersession chains. Partially, with confidence: section anchors, cites-same-source (C.5) |
| 4 | What does the existing publisher generate today? | **OBSERVED.** 956 HTML pages, 36 MB; `research-catalog.json` 6.3 MB (4.0 MB inline HTML), collections 2.2 MB, graph 349 KB, diagnostics 21 KB, Pagefind 5.3 MB / 813 files (A.7) |
| 5 | Which current URLs must remain stable? | **OBSERVED/PROPOSAL.** `/collections/{facet}/{value}/` (175 pages) and all `/research/{slug}/`. But 58% are title-derived and **already unstable** — redirect them to stable keys (J.3) |
| 6 | Repository-local, or aggregate multiple repos? | **OPEN QUESTION — yours.** Nothing in the corpus settles it. §30.1 says design identifiers to permit it later; the proposed `ArtifactKey` does |
| 7 | How large are the largest repositories? | **OBSERVED.** `visual-engineering`: 837 Markdown files, 756 published, 5.6 s full build. Small |
| 8 | Does ROS have a formal schema/version marker? | **OBSERVED. No.** No artifact format version exists anywhere. `version` is the research document's own version. Format versions appear only in generated output. Minimum viable scheme proposed in B.5 |
| 9 | Fail on broken internal references, or publish with warnings? | **PROPOSAL: warn.** 9 dangling references are legitimate research states — documents referencing work that does not exist yet. Only duplicate ids, duplicate URLs, unreadable files, path escapes and lost published URLs should block (G.5) |
| 10 | Which research objects already have stable IDs? | **OBSERVED.** Authored documents: 49 of 117 (all unique). Frontier records: 520 of 520 (content hashes). **No sub-document object has an ID** (B.6) |
| 11 | Should search be entirely static/client-side initially? | **PROPOSAL: yes**, but built from the typed model rather than rendered HTML, and sharded per project (ADR 0008) |
| 12 | Which current capabilities are mandatory for parity? | **PROPOSAL.** Full-text search, facet collections, per-artifact pages, the JSON catalog contract. Not mandatory: the current graph feature — it has 6 edges |
| 13 | What parts of the current UI are specifically ineffective? | **OBSERVED, and it is not really the UI.** 99.1% of documents have no relationships to show; 93.7% share one type; 98.1% share one research area; 97% share one date. Every facet is degenerate because the data feeding it was discarded (A.14) |
| 14 | Does research need access controls? | **OPEN QUESTION — yours.** `visual-engineering` is public; static hosting assumes public |
| 15 | Private Pages or other deployment targets later? | **OPEN QUESTION — yours.** Static output is portable; nothing in the design blocks it |
| 16 | Can one object appear in multiple projections without duplication? | **PROPOSAL: yes.** Projections are views over one `Corpus`; per-artifact shards are referenced, not copied (K.2) |
| 17 | Does ROS define citations separately from evidence? | **OBSERVED: yes, implicitly.** `references:` is bibliographic (16 files, e.g. "Shannon, C. E. (1948)…") and is **not** an internal link. Evidence is a document type. Conflating them would generate 100% false relationships (C.3) |
| 18 | How are contradictory findings represented today? | **OBSERVED: they are not.** No `contradicts` relation exists anywhere. §6.3/6.4/6.7 all depend on it |
| 19 | How are superseded findings represented? | **OBSERVED: declared but unused.** `supersedes`/`superseded_by` appear in 8 places, **all null**. The vocabulary exists; nothing has been superseded yet |
| 20 | Is a journal chronological source, a first-class object, or both? | **OBSERVED: a document type** (`research-journal`, 5 projects). Nothing distinguishes journal entries within it. **Today it is a document, not a timeline** — and with 735/756 fabricated dates, chronology is not available (A.5) |
| 21 | Do generated diagrams have canonical meaning? | **OPEN QUESTION — yours.** No diagrams were found in the sampled corpus |
| 22 | PDF/print exports? | **PROPOSAL: separate concern.** §9.7 asks only that print not be broken; static semantic HTML satisfies that |
| 23 | How are attachments/images/datasets handled? | **OBSERVED, partially.** The corpus references `../machine-readable/*.json` from `related_documents` — non-Markdown assets are already being linked, and currently resolve as unclassified. They need a first-class `Asset` reference kind (§30.2) |
| 24 | Relationship between the publisher and ROS tooling? | **OBSERVED/PROPOSAL.** Today: **none** — the publisher reads no registry, no `ros.json`, no `.ros/`. Proposed: read-only consumer of `ros.json` and registries, never a writer (D.4) |
| 25 | Should publication state be written back into ROS? | **PROPOSAL: no.** §30.10 and §20.1 both point the same way; the publisher stays a reader/interpreter (ADR 0001) |

## The four that need you

- **Q6** cross-repository aggregation
- **Q14** access controls
- **Q15** deployment targets beyond GitHub Pages
- **Q21** canonical meaning of generated diagrams

None blocks the first vertical slice.

## One question the draft does not ask, and should

**Will ROS gain a sub-document research object model?**

§6.3, §6.4, §6.7, §30.5 and §30.6, and one clause of the §31 success definition,
all require findings and evidence items to be independently identified research
objects. **No such object exists in the corpus.** The publisher cannot create
them without inventing identifiers, which §3.3, §20.1 and the task's own §28
forbid.

This is an authoring/ROS decision, not a publisher decision, and it gates
roughly half of §6.
