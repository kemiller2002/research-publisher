# A. Current Publisher Inventory

Every statement here was verified against source code or committed build
output. Nothing is taken from documentation claims.

## A.1 Location and identity

**OBSERVED.** The publisher is `kemiller2002/research-publisher`, published to
npm as `@echelon-foundry/research-publisher`. It is both the engine and its own
first consumer.

| Aspect | Value |
| --- | --- |
| Engine source | `src/**/*.mjs` — 16 modules, **1,335 lines** total |
| Entry points | `bin/research-publisher.js` (lifecycle), `src/cli/index.mjs` (build) |
| Build command | `node ./src/cli/index.mjs build --config ./research-publisher.config.mjs` |
| Config | `research-publisher.config.mjs` (per consuming repo) |
| Renderer | Astro (`site/`), post-processed by Pagefind |
| Output | `dist/` |
| Deployment | GitHub Actions → GitHub Pages |

There is no ambiguity about which repo is authoritative: `visual-engineering`
consumes the package via its own `research-publisher.config.mjs` and commits
the result to `dist/`.

## A.2 Inputs

**OBSERVED.** Inputs are Markdown files with YAML front matter, selected by
glob. `visual-engineering` uses `include: ["**/*.md"]` with a 20-entry exclude
list (`node_modules`, `dist`, `docs`, `src`, `prompts`, `packages`,
`**/archive/**`, …).

The engine consumes **no ROS registry, no `ros.json`, and no `.ros/` state**.
This is significant: the publisher and ROS are not integrated. ROS's own
registries (`registries/*.json`) are invisible to it.

## A.3 Discovery

**OBSERVED.** `src/content/discover.mjs` (17 lines) is pure glob traversal via
`fast-glob`. Not manifest-driven, not registry-driven, not Git-aware.

## A.4 Parsing

**OBSERVED.** `src/content/parse-document.mjs` (74 lines):

- `gray-matter` splits front matter from body.
- `remark-parse` + `remark-gfm` build an AST; `remark-rehype` →
  `rehype-sanitize` → `rehype-slug` → `rehype-stringify` renders HTML.
- Headings are collected as `{depth, text}` — **for a table of contents only**.
- Links are collected as raw URL strings.

**Critical:** no heading is semantically interpreted. There is no notion of an
"Evidence" section, a "Findings" section, or a "Falsification log". The body is
one opaque HTML blob per file.

**INFERENCE:** Therefore no sub-document research object can be addressed,
linked, or indexed. A finding inside a research report is invisible to the
system. This is the structural reason the publisher can only ever offer
page-level navigation.

`rehype-sanitize` is applied, so embedded raw HTML in Markdown is stripped.
Good security posture; also means HTML-bearing research loses content silently.

## A.5 Normalization — where the corpus is lost

**OBSERVED.** `src/metadata/normalize.mjs` (222 lines) is the real contract.

Aliases it understands (`legacyAliases`, lines 16–27):
`identifier`, `stableId`, `research_area`, `artifact_type`, `updated_at`,
`created_at`, `author`, `documentPurpose`, `purpose`, `audience`, `projectId`.

Keys the real corpus uses that are **not** aliased, and are therefore silently
discarded:

| Corpus key | Files using it | Expected by engine |
| --- | --- | --- |
| `document_type` | 49 | `artifactType` |
| `related_documents` | 23 | `relatedDocuments` |
| `superseded_by` | 4 | `supersededBy` |
| `related_projects` | 5 | `relatedProjects` |
| `author_agent` | 5 | `authorAgent` (only `author` is aliased) |
| `source_rep` | 4 | *(no equivalent)* |
| `evidence_level` | 8 | *(no equivalent)* |

**OBSERVED consequence**, from `dist/data/research-catalog.json`:

- **708 of 756 records (93.7%)** carry `artifactType: "research-document"` —
  the fallback, because `document_type` was dropped and `inferArtifactType`
  matches neither the ID shapes nor the directory names used by the corpus
  (it tests for `/evidence/`, `/hypotheses/`, `/theories/`; the corpus uses
  `evidence-registry/`, `hypothesis-registry/`, `theory-registry/`).
- **742 of 756 (98.1%)** carry `researchArea: "General Research"` — the
  hardcoded default.

### Fabricated defaults

**OBSERVED.** `normalizeDocument` substitutes values where data is absent:

```js
created: normalizeDate(frontmatter.created) ?? "2026-07-22",
updated: normalizeDate(frontmatter.updated) ?? "2026-07-22",
researchArea: frontmatter.researchArea ? … : "General Research",
status:       frontmatter.status       ? … : "draft",
authorAgent:  frontmatter.authorAgent  ? … : "unknown",
version:      frontmatter.version      ? … : "0.1",
```

**735 of 756 records have `created: "2026-07-22"`** — a literal constant in the
source. Any "recent activity" or timeline view built on this data is
meaningless. This is not a missing feature; it is fabricated provenance.

One record carries `researchArea: "color,contrast,low-vision,color-vision-deficiency"`
— a tag list that leaked into a single-value field and then became a facet.

## A.6 Relationships

**OBSERVED.** `src/relationships/graph.mjs` (65 lines) reads exactly four
normalized fields — `relatedDocuments`, `evidenceIds`, `hypothesisIds`,
`theoryIds` — and emits edge types `related-document`, `evidence`,
`hypothesis`, `theory`.

Two defects compound:

1. It consumes only camelCase ID lists. The corpus predominantly uses
   `related_documents` with **file paths**, which normalization already dropped.
2. `if (byId.has(relatedId))` — an edge whose target is not a known ID is
   **silently discarded**. Dangling references produce no diagnostic.

Backlinks *are* computed (`backlinks` map keyed by target), so reverse
navigation is architecturally present — it simply has almost no data.

**OBSERVED result:** `dist/data/research-graph.json` — 756 nodes, **6 edges**,
all `related-document`. 7 nodes (0.9%) have any edge at all.

## A.7 Generated output

**OBSERVED**, from the committed `dist/`:

| Artifact | Size / count |
| --- | --- |
| Total `dist/` | **36 MB**, 1,776 tracked files |
| HTML pages | **956** |
| `data/research-catalog.json` | **6.3 MB** |
| — of which inline rendered HTML | **4.0 MB** (median 3,383 bytes/record) |
| `data/research-collections.json` | 2.2 MB |
| `data/research-graph.json` | 349 KB |
| `data/research-guides.json` | 11 KB |
| `data/build-diagnostics.json` | 21 KB |
| `pagefind/` | **5.3 MB** across 813 files |

The catalog embeds every document's rendered HTML. A consumer wanting only
metadata must still download 6.3 MB.

## A.8 URLs

**OBSERVED.** Two shapes:

```
/research/<slug>/                     ~700 document pages
/collections/<facet>/<value>/         175 facet pages
```

Facets observed: `tag` (99), `status` (34), `discipline` (16), `artifact-type`
(10), `project` (9), `research-area` (8), `purpose` (8), `audience` (5).

**Slug derivation** (`normalize.mjs`):
`slug = frontmatter.slug ?? slugify(id ? `${id}-${title}` : title)`.

**INFERENCE — URL instability:** for the 68 of 117 content documents with no
`id`, the URL is derived from the **title**. Editing a title silently changes
the published URL and breaks every inbound link. This is a latent defect in the
current system, not merely a vNext design concern.

## A.9 Search

**OBSERVED.** Pagefind, run over rendered HTML after the Astro build
(`searchIndexTimeMs: 869`). 5.3 MB / 813 index files.

Because it indexes *rendered output*, search has no concept of research object
type; filters in the UI are client-side refinements over Pagefind result
metadata (stated in `docs/research-publisher-implementation-report.md` and
consistent with the generated bundle). Searching for evidence supporting a
finding is not expressible.

## A.10 Navigation

**OBSERVED.** Navigation is one page per file plus facet collections. With a
6-edge graph, "related documents" and backlink affordances render empty for
99.1% of documents.

**INFERENCE:** users are therefore forced to navigate by *file* and by *tag*,
never by research relationship — which is precisely the complaint.

## A.11 Validation

**OBSERVED.** `src/validation/validate.mjs` (103 lines) defines nine rules:

| Code | Severity |
| --- | --- |
| `missing-title` | error |
| `duplicate-id` | error |
| `duplicate-url` | error |
| `missing-id` | warning |
| `empty-relation` | warning |
| `unknown-purpose` | warning |
| `unknown-audience` | warning |
| `entry-point-without-project` | warning |
| `entry-point-without-purpose` | warning |

**OBSERVED:** in the real build, exactly **one** code fired — `missing-id`, 75
times. No rule exists for dangling references, orphaned artifacts,
contradictions, or superseded-but-still-referenced artifacts.

## A.12 Publishing path

**OBSERVED.**

```
Markdown (glob)
  → discover.mjs
  → parse-document.mjs      (gray-matter + remark/rehype)
  → normalize.mjs           (aliases, defaults, slug/url)
  → validate.mjs            (9 rules)
  → graph.mjs               (4 edge types)
  → project.mjs             writes dist/data/*.json
  → Astro                   renders HTML from the JSON
  → Pagefind                indexes the rendered HTML
  → GitHub Actions          → GitHub Pages
```

## A.13 Measured performance

**OBSERVED**, `dist/data/build-diagnostics.json`:

| Phase | ms |
| --- | --- |
| parse | 2,737 |
| render | 1,751 |
| search index | 869 |
| **total** | **5,592** |

756 documents in 5.6 seconds.

## A.14 Weaknesses, each tied to evidence

| # | Weakness | Evidence |
| --- | --- | --- |
| W1 | Research relationships are effectively absent | 6 edges / 756 nodes (`research-graph.json`) |
| W2 | Artifact typing collapsed | 708/756 are `research-document` (`research-catalog.json`) |
| W3 | Provenance fabricated | 735/756 `created: "2026-07-22"`, a source constant |
| W4 | Faceting degenerate | 742/756 `researchArea: "General Research"` |
| W5 | Snake_case corpus silently dropped | `legacyAliases` omits `document_type`, `related_documents`, `superseded_by`, … |
| W6 | Dangling references invisible | `if (byId.has(...))` drops edges with no diagnostic |
| W7 | No sub-document objects | parser records headings for ToC only |
| W8 | Metadata payload bloated | catalog 6.3 MB, 4.0 MB of it inline HTML |
| W9 | URLs unstable under retitling | slug derived from title when `id` absent (68/117 content docs) |
| W10 | Validation blind to integrity | 9 rules, none about references; only `missing-id` ever fires |
| W11 | ROS not integrated | engine reads no registry, no `ros.json`, no `.ros/` |

W1–W6 and W10–W11 are **contract/parser defects**. Only W7–W9 are partly
presentation concerns. That distribution is the core finding of this discovery.
