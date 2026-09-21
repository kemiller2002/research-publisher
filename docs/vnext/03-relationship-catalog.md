# C. Relationship Catalog

Built from the actual corpus, not from speculation. Relationships the corpus
does not encode are listed in C.4 as *absent*, not invented.

## C.1 Canonical relationships (encoded in the research itself)

| Relationship | Source → Target | How encoded | Count | Reverse derivable | Dangling legal | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| **related-document (path)** | document → document | `related_documents:` list of **file paths** | 40 resolving | yes | yes | Two bases in use: 18 file-relative, 22 repo-root-relative |
| **related-document (id)** | document → document | `relatedDocuments:` list of **IDs** | 6 resolving | yes | yes | camelCase variant, 1 file |
| **source-rep** | artifact → research-execution-package | `source_rep: RP-COMP-005` | 4, all resolve | yes | no | Provenance; strongest typed link in the authored corpus |
| **supersedes / superseded-by** | document → document | front-matter keys | 8 occurrences, **all `null`** | yes | n/a | Declared vocabulary, zero live data |
| **originates** | document → frontier record | `frontier-graph.json`; in Markdown, the `Evidence trace` → `Origin document` link | **520** | yes | no | Machine-generated, fully resolving |
| **prerequisite** | frontier record → frontier record | `frontier-graph.json` edges; `dependencies` field | **312** | yes | no | Machine-generated |
| **section-of** | frontier record → heading within origin document | `section:` field, matched by heading **text** | 520 | no | yes | **Fragile**: breaks on rewording |

**OBSERVED total:** 878 canonical relationship instances resolve across the
corpus (40 + 6 + 4 + 520 + 312 minus overlap). The current publisher derives
**6**.

## C.2 Value kinds inside `related_documents` — a parser constraint

**OBSERVED**, resolving all 63 values across both spellings:

| Kind | Count | Example |
| --- | --- | --- |
| repo-root-relative path | 22 | `content/projects/design-library/architecture/….md` |
| file-relative path | 18 | `../research-execution-package/rep-bde-0001-….md` |
| free-text title (not a link at all) | 8 | `Product Genome: Project Atlas Research Framework v1.0` |
| ID that resolves | 6 | `EVR-CCE-0001` |
| unresolved path | 6 | `component-library-foundations-research-report.md` (bare filename) |
| ID-shaped but dangling | 3 | `PRM-VE-COL-001`, `RDM-VE-COL-001`, `DF-ATLAS-COLOR-005` |

**INFERENCE:** `related_documents` is a *heterogeneous union*, not a list of
references. A vNext parser must classify each value — path (two bases), ID, or
prose — and must model "this value is not a link" as a legitimate outcome, not
an error. Treating the field as a list of IDs (what the current engine does)
discards 40 of 63 values.

## C.3 `references` is bibliography, not linkage

**OBSERVED**, 16 files:

```yaml
references:
- Shannon, C. E. (1948), A Mathematical Theory of Communication
- Legge et al. (1997-2007), visual span and reading research
```

**INFERENCE:** any parser that treats `references` as internal references — a
natural reading of the name — would generate 100% false relationships. This is
recorded explicitly so the vNext contract does not repeat the mistake.

## C.4 Relationships the corpus does NOT encode

Per §9 and §28, these are **not** introduced into the canonical model:

- finding → evidence (no finding is a distinct object; see B.6)
- evidence → finding
- hypothesis → experiment → result
- contradicts / refutes
- theory → supporting findings

**OBSERVED:** `RP-VE-2026-0001` carries `evidenceIds`, `hypothesisIds`,
`theoryIds` — but only in the 12-file research-publisher seed corpus, not in
the 117-file VE research corpus, where those keys appear **zero** times.

**OPEN QUESTION:** whether ROS intends `evidenceIds`/`hypothesisIds`/
`theoryIds` to become the canonical typed-link vocabulary. They are in the
engine and in the seed corpus but absent from real research. This should be
decided by the ROS owner, not inferred by the publisher.

## C.5 DERIVED PUBLICATION RELATIONSHIPS

Deterministically computable, useful for navigation, **not canonical research**.
Each must be labelled as derived in the published output.

| Derived relationship | Derivation | Determinism |
| --- | --- | --- |
| `backlink-of` | invert any canonical edge | total |
| `same-project` | equal `project` value | total |
| `same-concept` / `same-tag` | set intersection on `concepts` / `tags` | total |
| `cites-same-source` | shared normalised `references` entry | total after citation normalisation — **normalisation is lossy; mark low confidence** |
| `frontier-of` | invert `originates` — "what open questions came out of this document" | total |
| `supersession-chain` | transitive closure over `supersedes` | total, but **zero data today** |
| `section-anchor` | `section:` text → heading slug | **partial** — fails on rewording; must degrade to document-level link, never fabricate an anchor |

## C.6 Cardinality, cycles, dangling

- **Cardinality matters** for `originates` (1 document → many frontier records;
  observed max 104 documents producing 520 records) and for `source_rep`
  (many artifacts → 1 REP).
- **Cycles are legal** and present: `web-component-framework-architecture-recommendation-v1.md`
  and `web-component-framework-adrs-v1.md` reference each other through
  `related_documents`. A vNext graph walker must be cycle-safe.
- **Dangling targets are legal** in authored research (9 unresolved of 63) and
  must produce a *warning*, never a build failure — these are real research
  documents referencing work that does not exist yet.

## C.7 Integrity findings (§10)

Classified per §10's required distinction.

### Invalid ROS — none found

`./ros validate` passes on the research-publisher corpus. No duplicate IDs
anywhere (49/49 unique authored, 520 unique frontier).

### Valid but suspicious — warn, do not block

| Finding | Count | Evidence |
| --- | --- | --- |
| Dangling `related_documents` reference | 9 | C.2 |
| Bare-filename reference with no directory | 6 | `component-library-foundations-research-report.md` |
| Free-text title in a link field | 8 | C.2 |
| Document with no `id` | 68 of 117 authored | B.1 |
| `researchArea` holding a tag list | 1 | `color,contrast,low-vision,color-vision-deficiency` |
| Orphaned document (no in/out canonical edges) | large; exact count depends on parser | A.6 |

### Legitimate research states — never flag

- `supersedes: null` / `superseded_by: null` — nothing has been superseded yet.
- `status: computational-pilot-complete-human-study-pending` — an honest
  statement of an incomplete research programme.
- 520 frontier records with `status: Open` — open questions are the point.
- A REP with no experiment report yet.

### Cross-corpus integrity defect found during this discovery

**OBSERVED:** `research/packages/RP-VE-2026-0001--research-publisher.md` in
*this* repository declares `relatedDocuments: [DF-2026-001, HY-VE-2026-0001]`.
`DF-2026-001` does not exist — the corpus migration renamed `CN-2026-001` to
`DF-VE-2026-0001`. The reference was not updated, and neither the publisher
(which silently drops unresolvable edges) nor `ros validate` reported it.

This is a concrete instance of weakness W6 and is exactly the class of defect
the vNext integrity view should surface.
