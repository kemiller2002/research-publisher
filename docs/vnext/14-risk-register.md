# N. Risk Register

Likelihood and impact are stated as judgements; the evidence column is what the
judgement rests on.

| # | Risk | Likelihood | Impact | Evidence | Mitigation | Blocks slice? |
| --- | --- | --- | --- | --- | --- | --- |
| R1 | ROS formats less consistent than expected | **Certain — already realised** | High | 37 status values; both casings of `document_type`; 5 value kinds in one field | Heterogeneous union types (F.5); `Other of string`; `UnknownKeys` | No — designed for |
| R2 | Identifiers insufficient for deep links | **High** | Medium | 68 of 117 authored docs have no `id` | Path-key URLs (J.2); never title-derived | No |
| R3 | Section anchors break on rewording | **High** | Medium | frontier records match sections by heading **text** | Degrade to document URL; never fabricate an anchor | No |
| R4 | Implicit semantics unparseable | Medium | Medium | status phrases encode stage + obligation | Parse best-effort into `StatusReading`; keep text verbatim | No |
| R5 | Giant client indexes | Low | Medium | catalog is 6.3 MB today **because it inlines HTML**, not because the corpus is large | Split metadata from content; shard per project; measure in slice (M.4.9) | No |
| R6 | URL compatibility breakage | Medium | High | 58% of authored URLs are title-derived and already unstable | `redirects.json` + stubs; **blocking** build gate (G.5) | No |
| R7 | WASM startup size hurts first paint | Medium | Medium | no measurement yet — F#/WASM payloads are not small | Static HTML is the baseline; Limen is enhancement (H.6) | No |
| R8 | GitHub Pages limits | Low | Medium | no rewrites; `dist/` already 36 MB committed | Keep `404.html` fallback; keep output deterministic | No |
| R9 | Derived relationships mistaken for research | **Medium** | **High** | this is the failure mode §9 and §28 warn about | Separate `DerivedRelation` type; `derived: true` in output; distinct UI treatment | No |
| R10 | Frontier IDs change on source edit | Medium | Medium | `RFR-<hash>` appears content-derived; unconfirmed | **OPEN QUESTION J.5** — confirm with frontier owner before advertising permanence | No |
| R11 | Embedded HTML in Markdown lost | Low | Low | `rehype-sanitize` strips it today | Preserve raw in `Section.Body`; sanitise only at render | No |
| R12 | Security — rendered research content | Low | High | corpus is trusted, but agents may add content | Keep sanitisation at render; never `eval` (Limen forbids it) | No |
| R13 | Repository history complexity | Low | Low | §19 deferred | `Provenance.Commit` reserved | No |
| R14 | **Two-population blindness** | Medium | High | 69% of published records are machine-generated frontier records | Model frontier records as a distinct type; never average them with authored research | No |
| R15 | Scope creep into inventing research objects | **Medium** | **High** | §16 lists views the corpus cannot support (findings, contradictions) | I.3 records the exclusions and the evidence for them | No |
| R16 | No requirements owner | **Realised** | Medium | no vNext requirements document exists anywhere (E.0) | Deliverable O is explicitly v0.1.0 and needs sign-off | **Yes — for scope confirmation, not for build** |

## Note on certainty

R1 and R16 are not predictions; they were observed during discovery. R5's
likelihood is downgraded from the obvious reading because the measurement shows
the size problem is a design defect (inlined HTML), not corpus scale — 756
documents build in 5.6 seconds.
