# I. Proposed Research Experience

Every element below is justified from the actual corpus. Anything the corpus
cannot support is marked and excluded.

## I.1 The landing problem

**OBSERVED:** the current landing experience is a document list plus facet
collections, where 742 of 756 documents share one `researchArea`, 708 share one
`artifactType`, and 735 share one `created` date. Three of the four facets are
degenerate, so the landing page is effectively an undifferentiated list of ~700
links.

## I.2 What a project landing page should show

Answering §15's seven questions, with the corpus evidence that each element is
actually available.

| Element | Answers | Source | Available? |
| --- | --- | --- | --- |
| **Project purpose** | "What is this about?" | REP with `entryPoint: true` (12 declared), else the project's earliest REP | yes |
| **Current position** | "What do we know?" | artifacts with `status` classed Verified/Complete, plus `confidence` (36 files) | yes |
| **Evidence posture** | "Why do we think that?" | evidence-registry artifacts (5 projects) + `evidence_level` (8 files) | partial — surface what exists, do not fabricate a score |
| **Open frontier** | "What is uncertain?" | **520 frontier records**, each with an origin document and a `frontier_score` | **yes — the richest signal in the corpus** |
| **Active experiments** | "What is being tested?" | experiment-report artifacts (10 `experiment_report`) + status phrases ending `-pending` | yes |
| **Recent activity** | "What changed?" | **NOT AVAILABLE** from front matter (735/756 share a fabricated date). Must come from Git (§19) or be omitted | **omit in slice 1** |
| **Source inspection** | "Where do I look?" | `Provenance.Span` on every projected fact | yes |
| **Integrity** | "What is inconsistent?" | 9 dangling refs, 6 bare filenames, orphans | yes |

**PROPOSAL — landing composition, in priority order:**

1. **Orientation strip** — project purpose from the entry-point REP.
2. **Open frontier** — top frontier records by `frontier_score`, each showing
   its origin document and challenged assumption. This is the corpus's
   strongest, densest, best-linked data and it is currently invisible as
   anything but 520 undifferentiated pages.
3. **Research position** — verified/complete artifacts with confidence.
4. **In flight** — artifacts whose status encodes a pending obligation
   (`…-human-study-pending`), which is a genuinely useful derived reading.
5. **Integrity** — counts, linking to the integrity view.

**Deliberately excluded from slice 1:** "recent activity". The data to support
it does not exist without Git history, and presenting the `2026-07-22` constant
as a date would be fabrication (§28).

## I.3 Views (§16)

Only views the corpus justifies.

| View | Purpose | Source objects | Justified by |
| --- | --- | --- | --- |
| **Project Overview** | orient | REP entry points, frontier summary, integrity counts | I.2 |
| **Frontier** | "what is open" | 520 frontier records; filter by `category` (observed), `frontier_score` | 520 records with structured fields |
| **Artifact** | read one document + its real links | `Artifact`, `Section`, `Edge` | every document |
| **Relationships** | traverse | `Edge` (canonical + derived, visually distinct) | 878 canonical instances |
| **Integrity** | find what is broken | `Finding` | 9 dangling, 6 bare, 1 malformed facet |
| **Search** | find by text + type + project | search index | 756 documents |

**Not built in slice 1, and why:**

- **Findings view** — there is no `Finding` object in the corpus (C.4). Building
  one would require inventing IDs (§28).
- **Evidence → dependent findings** — the relationship does not exist in the
  data (C.4).
- **Timeline** — no trustworthy dates (I.2).
- **Contradictions** — no `contradicts` relation is encoded anywhere.

These are listed so their absence is a recorded decision rather than an
oversight. Each becomes buildable the moment the corpus encodes the relation.

## I.4 The navigation gain, stated concretely

| Task | Today | vNext |
| --- | --- | --- |
| From a document, find work it spawned | impossible — no edge exists | one click (`FrontierOf`, 520 edges) |
| From a frontier record, reach its origin section | manual — read the text, then search | one click (`Originates` + section anchor) |
| From an artifact, find its REP | impossible — `source_rep` discarded | one click (4 edges, all resolving) |
| Find everything in a project | facet page works | unchanged |
| See what references this document | empty for 99.1% of documents | backlinks over 878 canonical edges |
