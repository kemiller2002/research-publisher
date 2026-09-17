# L. Regression Test Plan

Per §21, fixtures are derived from real research and preserve real edge cases.
Canonical research is **not** modified to make testing easier.

## L.1 Fixture strategy

`visual-engineering` is public, so faithful minimised fixtures may be committed.
Each fixture is a reduced copy that preserves the *structural* edge case while
trimming body prose.

| Fixture | Derived from | Edge case preserved |
| --- | --- | --- |
| `snake-typed.md` | any of 49 `document_type` files | snake_case type key |
| `casing-variants/` | `experiment_report` + `experiment-report` | same concept, two casings |
| `no-frontmatter.md` | the archived duplicate | zero front matter |
| `no-id.md` | one of 68 | absent id → path-key URL |
| `path-rel-refs.md` | `rep-bde-0001` | file-relative `related_documents` |
| `root-rel-refs.md` | `web-component-framework-architecture-…` | repo-root-relative refs |
| `prose-in-link-field.md` | `product-genome-research-execution-package-run-02` | free text in `related_documents` |
| `dangling-ref.md` | `rep-ve-col-001` | `PRM-VE-COL-001` dangling |
| `bare-filename-ref.md` | `2026-07-21-semantic-durability-…` | filename with no directory |
| `cyclic-pair/` | the web-component architecture ↔ ADR pair | reference cycle |
| `status-phrase.md` | `…-human-study-pending` | free-text status with obligation |
| `tags-in-researcharea.md` | the `color,contrast,…` record | malformed facet value |
| `frontier-record.md` | `RFR-B186B831` | frontier shape + evidence trace |
| `unknown-keys.md` | any with `llm_ingest`, `machine_readable` | unmodelled keys round-trip |

## L.2 Assertions

### Discovery
- exact artifact count per configured root
- excluded globs are excluded (`**/archive/**`, `node_modules`, `dist`)
- a file with no front matter is discovered, not skipped

### Parsing
- `document_type` and `artifactType` both yield the same `ArtifactType`
- `experiment_report` ≡ `experiment-report`
- unknown front-matter keys appear in `UnknownKeys`, none dropped
- unknown body sections are preserved verbatim
- absent `created` stays `None` — **explicit assertion that no default date appears anywhere in the output**

### Identity
- 49 authored + 520 frontier ids parse; zero duplicates
- an artifact with no id gets a path-key URL
- **retitling a fixture does not change its URL** (the current system's defect)

### Relationships
- all five `related_documents` value kinds classify correctly
- file-relative and repo-root-relative both resolve
- prose values classify as `NotALink` and raise **no** warning
- dangling refs produce a warning and **appear in the output**, not dropped
- cyclic references terminate
- backlinks are the exact inverse of canonical edges
- every derived edge carries `derived: true`
- **corpus-level:** ≥ 850 canonical edges on the full VE corpus (vs 6 today)

### Validation
- duplicate id → blocking
- dangling ref → warning, non-blocking
- orphan → warning, non-blocking
- `supersedes: null` → **no finding at all**

### Output
- every previously published URL is emitted or redirected (blocking)
- `artifacts.json` contains no HTML
- `artifact/{key}.json` resolves for every artifact
- two consecutive builds are byte-identical (determinism)

### Limen
- engine sources name no browser capability (`limen verify --strict` in CI)
- `limen.config.json` declares a real boundary, not `[]`

## L.3 Golden-corpus test

Run the full `visual-engineering` corpus in CI as a nightly/PR job and assert:
artifact count, edge count by relation, finding count by code, total output
size, and the URL set. Any movement is a deliberate, reviewable change.
