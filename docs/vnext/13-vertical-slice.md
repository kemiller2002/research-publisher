# M. First Vertical Slice

Per §22 and the SDE greenfield workflow: the smallest implementation that
proves the architecture, not a feature-complete publisher.

## M.1 Chosen corpus

**`visual-engineering`, project `composition-science`.**

Why this one, from measurement:

- 23 published documents — small enough to reason about end to end
- contains the **only fully-resolving typed relationship in the authored
  corpus**: 4 artifacts linked by `source_rep: RP-COMP-005`
- spans four artifact types: `research-execution-package`, `experiment-report`
  (`EX-COMP-011`, `EX-COMP-012`), `research-framework` (`DF-COMP-002`),
  `theory-registry` (`TH-COMP-005`)
- its documents originate frontier records, exercising `Originates`
- IDs are declared, so `/a/{id}` is exercised — and some sibling documents lack
  ids, exercising `/s/{path-key}`

This gives two research object types to navigate between (§22.9) with real,
non-synthetic data.

## M.2 Scope

| § | Requirement | Slice implementation |
| --- | --- | --- |
| 22.1 | discover a real corpus | glob discovery over `content/projects/composition-science/**` |
| 22.2 | parse in F# | front matter + body sections; snake/camel; unknown keys preserved |
| 22.3 | typed domain model | `Artifact`, `Section`, `ReferenceValue`, `StatusReading` (F) |
| 22.4 | validate | duplicate id (blocking); dangling ref, missing id, orphan (warnings) |
| 22.5 | resolve relationships | `SourceRep`, `RelatedDocument` (all 5 value kinds), `Originates`; backlinks derived |
| 22.6 | machine-readable indexes | `manifest`, `artifacts`, `artifact/{key}`, `edges`, `edges/{key}`, `findings` |
| 22.7 | static baseline page | F#-generated HTML per artifact + project overview; readable with JS disabled |
| 22.8 | interactive Limen view | engine (F#→WASM) + thin kernel; real declared boundary |
| 22.9 | navigate ≥2 object types | experiment-report → its REP → back; REP → frontier records |
| 22.10 | source provenance | every artifact page links to its exact source path |
| 22.11 | back/forward | `pushState`/`popState` through the kernel |
| 22.12 | build + publish | GitHub Actions → Pages, `404.html` fallback |
| 22.13 | automated tests | L's fixtures + a composition-science golden test |

## M.3 Explicitly deferred

- other seven projects (config change only, no new architecture)
- search (slice proves retrieval via indexes; search is additive)
- Git-history timeline (§19) — see M.5
- frontier view beyond the `Originates` link
- collection/facet pages — the current ones keep working, untouched
- retiring `research-catalog.json` v1.1 — shim remains

## M.4 Exit criteria — measurable

1. All 23 composition-science documents parse; **zero** unknown keys dropped.
2. The 4 `source_rep` edges resolve — the current engine derives **0**.
3. Zero fabricated values: no `2026-07-22`, no `"General Research"`, no
   `"unknown"` author anywhere in the output.
4. Round trip: experiment report → REP → back, with correct browser history.
5. Two consecutive builds byte-identical.
6. `limen verify --strict` passes with a **non-empty** boundary.
7. Bounded retrieval: full context for `RP-COMP-005` in ≤ 2 fetches, ≤ 20 KB.
8. Every legacy URL for these 23 documents emits or redirects.
9. **Measured** search-index and payload sizes recorded, to validate K.4.

## M.5 Not blocked later

Per §19, Git history is not required now but must not be designed out.
`Provenance` already carries an optional `Commit`, and artifacts are keyed
independently of content, so a later pass can attach history without
re-modelling. Slice 1 leaves the field present and unpopulated.
