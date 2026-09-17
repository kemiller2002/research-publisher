# B. ROS Research Format Catalog

Source corpus: `kemiller2002/visual-engineering`, 837 Markdown files.

## B.0 The corpus has two populations, not one

This is the single most important structural fact, and it is not visible from
the publisher's output.

| Population | Files | Character |
| --- | --- | --- |
| **Authored research** under `content/**` | 117 | hand-written, heterogeneous, 27 artifact-type directories |
| **Frontier records** under `research/frontier/**` | 626 (520 published) | machine-generated, rigidly uniform, fully linked |

**OBSERVED:** 520 of the 756 published records (69%) are frontier records
(`id` prefix `RFR-`). The published site is majority machine-generated
research-opportunity records, not authored research.

Designing a parser against the authored corpus alone would mis-serve two thirds
of the published output; designing against frontier records alone would produce
a parser that cannot read the actual research.

---

## B.1 Authored research artifacts (`content/**`)

### Folder and filename convention

**OBSERVED:** `content/projects/<project>/<artifact-type>/<filename>.md`,
with 8 projects and 27 distinct artifact-type directory names.

Artifact-type directories, by number of projects using them:

```
research-execution-package 7   research-journal 5   evidence-registry 5
theory-registry 3   research-report 3   research-note 3
hypothesis-registry 3   experiment-report 3   canonical 3
research-framework 2   audit 2
template, standard, roadmap, research-plan, phase-report, machine-readable,
knowledge-model, experiments, evidence-gallery, evidence-collection,
design-specification, decision-record, crosswalk, comparative-study,
checklist, case-study, architecture   (1 each)
```

Filenames are **not** uniform: `rep-wc-0001-cross-project-web-component-framework.md`
(kebab, ID-prefixed) sits beside `REP_Visual_Engineering_Johannes_Itten_Modern_Color_Theory.md`
(SCREAMING_SNAKE, no ID) and `2026-07-21-semantic-durability-research-execution-package.md`
(date-prefixed).

**INFERENCE:** filename is not a reliable identity source. §28 explicitly warns
against assuming filenames are stable IDs; the corpus confirms the warning.

### Front matter: measured key frequency (of 117 files)

| Key | Count | Note |
| --- | --- | --- |
| `purposes` | 112 | **most universal key — more common than `title`** |
| `audiences` | 112 | |
| `project` | 94 | |
| `status` | 77 | 37 distinct values |
| `title` | 64 | |
| `llm_ingest` | 56 | machine-consumption hint |
| `machine_readable` | 56 | |
| `authors` | 52 | |
| `id` | 49 | **only 42% coverage** |
| `document_type` | 49 | snake_case; engine expects `artifactType` |
| `version` | 42 | |
| `date` / `created` / `updated` | 38 / 38 / 38 | three competing date keys |
| `confidence` | 36 | |
| `tags` | 35 | |
| `abstract` / `summary` | 26 / 19 | two competing summary keys |
| `related_documents` | 23 | snake_case |
| `canonical` | 21 | boolean flag, not a link |
| `concepts` | 21 | |
| `references` | 16 | **bibliographic citations, not internal links** |
| `entryPoint`/`entryPointOrder`/`entryPointLabel` | 12 each | camelCase island |
| `researchArea` (8) vs `research_area` (6) | | **both spellings coexist** |
| `evidence_level` | 8 | |
| `supersedes` / `superseded_by` | 4 / 4 | all `null` in current corpus |
| `source_rep` | 4 | provenance to a REP; all resolve |
| `artifactType` | **4** | the key the engine actually reads |

One file has no front matter at all
(`content/archive/duplicates/composition-science/Composition_Science_Research_Library_v0.2.md`).

### `document_type` values (49 files)

```
experiment_report 10   research-report 7   evidence-registry 4
research-journal 4   research-package 3   research_execution_package 3
hypothesis-registry 2  journal-entry 2
design-specification, research-roadmap, standard, theory-registry,
specification, ontology, governance, research_framework, research_report,
theory_registry, decision-record, experiment-report, audit, index   (1 each)
```

**OBSERVED:** the same concept appears in both casings —
`experiment_report`/`experiment-report`, `research-report`/`research_report`,
`theory-registry`/`theory_registry`. A vNext parser must normalise separator
and case before matching.

### `status` is free text, not an enum

**OBSERVED:** 37 distinct values across 77 files, including
`verified`, `active`, `proposed`, `complete`, `working`, `draft`,
`canonical-candidate`, and full English phrases:

```
"Applied Analysis — Working Draft"
"Verified Source Register and Applied Evidence Review"
computational-pilot-complete-human-study-pending
model-simulation-complete-gaze-study-pending
power-pilot-complete-field-study-pending
```

**INFERENCE:** `status` cannot be modelled as a closed discriminated union
without data loss. It must be a free-text value with an optional derived
classification. The status phrases encode a *research stage*
(`computational-pilot-complete`) plus an *outstanding obligation*
(`human-study-pending`) — genuinely useful, but only if parsed as structure
rather than an enum.

### Body structure

**OBSERVED:** heading vocabulary is per-artifact-type and not enforced.
`RP-VE-2026-0001` uses `Executive Summary`, `Repository Context`,
`Current Understanding`, `Key Discoveries`, `Open Questions`. Other REPs use
different sets. There is no repository-wide required heading.

---

## B.2 Frontier records (`research/frontier/records/*.md`)

**OBSERVED:** 626 files, rigidly uniform, machine-generated.

```yaml
---
id: RFR-B186B831
title: "Create a shared benchmark and decision threshold: …"
document_type: research_frontier_record
status: Open
category: Tooling
frontier_score: 392
generated: 2026-07-29
immutable: true
---
```

Required sections, in order: `Research opportunity`, `Background`,
`Evidence trace`, `Unknowns`.

The `Evidence trace` section is the important part — it carries an explicit,
resolvable provenance chain:

```markdown
- Origin document: [content/projects/project-atlas/research-report/….md](../../../content/…)
- Section: `The Relational Legibility Model`
- Specific assumption challenged: …
- Supporting evidence excerpt: "…"
```

**OBSERVED:** `id` is a content hash (`RFR-<8 hex>`), not a sequence.
`immutable: true` is declared. `status` is `Open` for all 520 published.

**INFERENCE:** frontier records are a *derived publication artifact* that has
been materialised into the canonical tree. They are regenerable from their
origin documents, which means vNext may treat them as derived without risking
canonical research.

---

## B.3 Pre-existing machine-readable indexes

**OBSERVED:** the frontier tooling already emits, under `research/frontier/`:

| File | Size | Content |
| --- | --- | --- |
| `frontier-index.json` | 1.31 MB | 104 documents, 520 records, each with 26 fields including `originDocuments`, `section`, `dependencies`, `suggestedRep`, `evidenceExcerpt`, `scoreComponents` |
| `frontier-graph.json` | 267 KB | **624 nodes, 832 edges**, `directed: true` |

Edge types: `originates` (520), `prerequisite` (312).
Node types: `frontier` (520), `document` (104).

`frontier-index.json` also declares an explicit publication **policy**:

```json
{"included": "publishable source",
 "excludedStatuses": ["draft","proposed","candidate","working",
   "working-draft","research-draft","applied-analysis-working-draft",
   "evidence-review-working-draft"]}
```

**This is the strongest single piece of evidence in the discovery.** A second
tool in the same repository already derives an 832-edge graph from the same
corpus. The publisher's own graph has 6 edges. The relationships are not
missing from the research — they are missing from the publisher.

---

## B.4 §6 — Formal vs conventional vs accidental

### Formally required (DOCUMENTED ROS RULE)

Enforced by `./ros validate` and the ROS schemas in
`repository-operating-system/schemas/`:

- artifact metadata conforms to `schemas/artifact-metadata.schema.json`
- canonical roots declared in `ros.json` (`journals`, `packages`, `theories`,
  `evidence`)
- registries under `registries/` are regenerated by `ros registry build`
- work-item attribution for meaningful changes (`docs/work-protocol.md`)

Note: **ROS does not enforce the `content/projects/**` layout at all.** The VE
corpus lives outside ROS's declared canonical roots.

### Conventional (widely followed, unenforced)

- `content/projects/<project>/<artifact-type>/<file>.md`
- `purposes` + `audiences` on nearly every document (112/117)
- `document_type` naming the artifact kind
- ID prefix families (`REP-`, `EVR-`/`EVREG-`, `HYR-`/`HYREG-`, `THY-`, `JR-`,
  `EX-`, `DF-`, `RFR-`)
- `related_documents` for internal links, `references` for bibliography

### Accidental — must NOT enter the parser contract

- **Specific casing** of `document_type` values. Both casings exist; matching
  literally would silently drop one.
- **The constant `2026-07-22`.** It is a source default, not data.
- **`researchArea: "General Research"`.** Also a default (98.1% of records).
- **Digit width in IDs** (`-001` vs `-0001`) and prefix length
  (`EVR-` vs `EVREG-` for the same concept).
- **`related_documents` path base.** Both file-relative and repo-root-relative
  appear; neither is "the" convention.
- **`entryPoint*` camelCase.** Present in only 12 files, all from the
  research-publisher seed corpus, not the wider research.

---

## B.5 §7 — Versioning

**OBSERVED:** there is **no artifact format version field** anywhere in the
corpus. `version` (42 files) is the *research document's* own version
(`"1.0"`, `"v0.1"`, `"0.4"`), not a schema version.

Format versions exist only in generated output: `research-catalog.json`
`schemaVersion: "1.1"`, `research-graph.json` `schemaVersion: "1.0"`, and
per-record `schemaVersion: "1.1"` inside the catalog.

**PROPOSAL — minimum viable versioning, no file rewrites:**

1. Treat absence of a format version as **format version 0** (the current
   heterogeneous state). No existing file changes.
2. Publish a `rosArtifactFormat` field in the *derived* indexes, set by the
   parser, recording which parse profile produced the record.
3. Only when a genuinely breaking authoring change is needed, introduce an
   opt-in `ros_format: 1` front-matter key. Files without it stay on profile 0
   indefinitely.

This satisfies §1.2 (no forced migration) and §7 (don't automatically require
rewriting existing files).

---

## B.6 §8 — Stable IDs and addressable objects

| Object | ID present | Unique | Derived from filename | Safe for a public URL |
| --- | --- | --- | --- | --- |
| Authored document | 49/117 (42%) | **yes — 49/49, zero duplicates** | no | yes, where present |
| Frontier record | 626/626 (100%) | yes (content hash) | yes (`RFR-x.md`) | yes |
| Project | implicit via `project` key (94/117) | yes | no | yes |
| Sub-document object (finding, evidence item, claim) | **none** | — | — | **no** |
| Section | none, but frontier records reference sections **by heading text** | not guaranteed | — | only as a slug, unstable |

**OBSERVED:** all 49 authored IDs are unique; there are no duplicate IDs in the
corpus. The `duplicate-id` rule has never had cause to fire.

**INFERENCE:** document-level deep links are safe today for 100% of frontier
records and 42% of authored documents. Sub-document deep links are **not**
safe: no sub-document object carries an ID, and the only sub-document handle in
use (frontier records' `section:` field, matched by heading text) breaks the
moment a heading is reworded.

**PROPOSAL:** vNext should mint stable URLs only for objects with a declared
`id`, and fall back to a **content-derived, stable** slug (hash of the
repository-relative source path, not the title) for the rest — which removes
the retitling hazard identified in A.8 without inventing IDs (§28).
