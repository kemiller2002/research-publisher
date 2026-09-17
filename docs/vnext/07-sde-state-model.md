# G. SDE / Ordo State Model

Follows `.sde/method/CONSTRUCTION-METHOD.md` (greenfield workflow) and
`.sde/architecture/FOUR-TIER-ARCHITECTURE.md`. Per §1.3 this is a real state
model, not enums named "state".

## G.1 Tier mapping

| SDE tier | Research Publisher vNext |
| --- | --- |
| **1 — Semantic Model** | `Artifact`, `Edge`, `Relation`, `StatusReading`, `ReferenceValue` (F) |
| **2 — State Transition / Domain Execution** | corpus lifecycle (G.2), artifact lifecycle (G.3), obligation evaluation |
| **3 — Application / Projection / Orchestration** | index generation, view projections, URL minting, search-index construction |
| **4 — Host / External Effects** | filesystem reads, `dist/` writes, GitHub Pages publication, log emission |

Dependency direction is inward: tier 4 may call tier 3, never the reverse.
The browser (Limen) consumes tier-3 projections only.

## G.2 Corpus states

Chosen to reflect the real process, which has a discovery stage the current
engine treats as implicit, and a *publishable-with-warnings* stage that the
corpus genuinely requires (9 dangling references are legitimate).

```
Unloaded
   │ discover
   ▼
RootsResolved ───(no roots match)──▶ NothingToPublish (terminal, not a failure)
   │ parse
   ▼
Parsed ──────────(any Blocking parse finding)──▶ Unpublishable
   │ resolve references
   ▼
Linked
   │ evaluate obligations
   ▼
Assessed
   ├── no findings ──────────────▶ Publishable
   ├── warnings only ────────────▶ PublishableWithWarnings
   └── any Blocking obligation ──▶ Unpublishable
   │ project
   ▼
Projected          (indexes + pages generated, nothing written yet)
   │ emit
   ▼
Emitted            (dist/ written)
   │ deploy  [external effect]
   ▼
Published   /   DeploymentFailed
```

**Invalid states — unrepresentable by construction:**

- `Linked` without `Parsed` (reference resolution needs an artifact set)
- `Published` from `Unpublishable`
- `Projected` with unresolved *blocking* obligations
- an `Edge` whose `From` is not in `Corpus.Artifacts`

## G.3 Artifact states

Per-artifact, independent of corpus state:

```
Discovered → Read → FrontMatterSplit → BodyParsed → Typed → Linked → Projected
                          │                  │         │
                          │                  │         └─▶ TypeUnknown (publishable)
                          │                  └─▶ NoFrontMatter (publishable; 1 observed)
                          └─▶ MalformedFrontMatter (quarantined, reported, not fatal)
```

An artifact that fails does **not** fail the corpus. It is quarantined and
reported — §28 forbids silently modifying malformed research, and failing the
whole build would make one bad file block all publication.

## G.4 Capabilities

A capability is available only in the states where it is meaningful:

| Capability | Requires | Notes |
| --- | --- | --- |
| `Discover` | `Unloaded` | config-driven; ROS roots are a hint, not a constraint (D.4) |
| `Parse` | `RootsResolved` | |
| `ResolveReference` | `Parsed` | needs the full artifact set to resolve IDs |
| `Validate` | `Linked` | |
| `Project` | `Publishable` \| `PublishableWithWarnings` | |
| `Emit` | `Projected` | first filesystem write |
| `Deploy` | `Emitted` | external effect |
| `Navigate` / `Search` / `InspectSource` | browser-side, on `Published` output | see H |

## G.5 Obligations

An obligation is an unmet condition attached to a state. **Which obligations
block is the key policy decision**, and the corpus dictates the answer: almost
nothing blocks, because real research is legitimately incomplete.

### Blocking

| Obligation | Rationale |
| --- | --- |
| Duplicate `ArtifactId` | two artifacts cannot share a URL; currently zero occurrences, so cost is nil |
| Duplicate published URL | same |
| Unreadable source file | cannot publish what cannot be read |
| Output path escapes the output root | path-traversal guard |
| A previously published URL disappears with no redirect | protects D.2 |

### Warning — never blocking

| Obligation | Observed count |
| --- | --- |
| Dangling reference | 9 |
| Bare-filename reference | 6 |
| Artifact with no `id` | 68 of 117 |
| Artifact with no declared type | 49 of 117 lack `document_type` |
| Orphan (no canonical edges) | large |
| `researchArea` holding a list | 1 |
| Unknown front-matter key | many — informational, never an error |

### Explicitly not an obligation

`supersedes: null`, `status: …-pending`, `status: Open`, a REP with no
experiment yet. These are research states, not defects (C.7).

## G.6 Policies

| Policy | Value | Source |
| --- | --- | --- |
| Publication inclusion | config globs; ROS registries as a cross-check | D.4 |
| Draft exclusion | `drafts: false`; frontier tooling already excludes 8 statuses | B.3 |
| Fabrication | **forbidden** — absent data stays absent | E.3 |
| Derived-relationship labelling | every derived edge tagged `derived: true` in output | §9 |
| Determinism | no wall-clock timestamps inside content-addressed artifacts | D.5 |

## G.7 Evidence required for transitions

| Transition | Evidence |
| --- | --- |
| `Parsed → Linked` | every reference classified into one `ReferenceValue` case |
| `Linked → Assessed` | every obligation evaluated and recorded, including the ones that passed |
| `Assessed → Projected` | zero blocking obligations, recorded as a signed assessment |
| `Projected → Emitted` | output manifest with a content hash per file |
| `Emitted → Published` | deployment receipt (Actions run id) |

## G.8 Version checks

- **Parser profile:** each artifact records which profile parsed it (B.5).
- **Output contract version:** each emitted index carries `schemaVersion`.
- **Engine version:** recorded once in the corpus manifest.
- On reading an artifact declaring a `ros_format` newer than the engine knows:
  **warn and parse with the newest known profile** — do not fail. A future
  authoring convention must not take the site offline.

## G.9 External effects (§13, explicit)

| Effect | Tier | When |
| --- | --- | --- |
| filesystem read of the corpus | 4 | `Discover`, `Parse` |
| read of `ros.json`, `registries/*.json` | 4 | `Discover` (read-only, D.4) |
| write to `dist/` | 4 | `Emit` only — nothing writes earlier |
| GitHub Pages publication | 4 | `Deploy` |
| log/diagnostic emission | 4 | all states |

No network access during build. No writes outside the configured output root.
