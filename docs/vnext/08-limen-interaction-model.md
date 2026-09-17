# H. Limen Interaction Model

## H.1 What Limen actually requires

**OBSERVED** (this session, verifying Limen 0.5.1 against this repository):
Limen enforces a **lexical** browser-capability boundary. Engine code must not
name `document`, `window`, `fetch(`, `localStorage`, `sessionStorage`; neither
side may use `eval`. Violations are `LIMEN009`; a declared path that does not
exist is `LIMEN010`.

**OBSERVED:** `limen.config.json` currently declares an empty boundary
(`engine: []`, `kernel: []`) because the present codebase has no engine/kernel
split to describe — `src/` contains 121 occurrences of the word `document`,
none of them the DOM global.

**INFERENCE:** vNext is the first time this repository will have a real Limen
boundary, and the F#-to-WASM core is a natural engine: it cannot name browser
globals even accidentally.

## H.2 The boundary

```
┌──────────────────────── kernel (TypeScript, thin) ────────────────────────┐
│ owns: document, window, history, location, fetch, clipboard,              │
│       localStorage/sessionStorage, DOM events                             │
│ contains: NO research semantics, NO relationship logic, NO parsing        │
└───────────────────────────────┬───────────────────────────────────────────┘
                                │  typed messages only
┌───────────────────────────────▼───────────────────────────────────────────┐
│ engine (F# → WASM)                                                        │
│ owns: route interpretation, filter/search semantics, relationship         │
│       traversal, lens selection, result ranking, citation formatting      │
│ names no browser capability                                               │
└───────────────────────────────────────────────────────────────────────────┘
```

Per §1.4, research interpretation lives in the engine. The kernel is a
capability adapter, deliberately boring.

## H.3 Application state, and where each piece lives

The decisive question per §14 is *which store owns each piece of state*.

| State | Owner | Rationale |
| --- | --- | --- |
| active project | **URL** | shareable, and the corpus is project-partitioned (8 projects) |
| current route | **URL** | back/forward must work (§22.11) |
| current artifact | **URL path** | deep-linkable for the 569 ID-bearing artifacts |
| selected view/lens | **URL path** segment | `/findings` vs `/evidence` are different pages conceptually |
| search query | **URL query** `?q=` | shareable search is a primary research act |
| active filters | **URL query** | same; also makes filter state reproducible in a citation |
| search result selection | **engine transient** | ephemeral; restoring it on reload would be surprising |
| expanded/collapsed related objects | **engine transient** | per-visit, high-churn |
| source panel open/closed | **engine transient**, last value mirrored to `localStorage` via kernel | a preference, not a location |
| navigation history | **browser history**, via kernel `pushState`/`popState` | never re-implemented in the engine |
| clipboard (copy citation) | **kernel capability**, text composed by engine | engine formats, kernel writes |
| the corpus itself | **static JSON**, fetched by kernel, parsed by engine | see K |

**Rule:** anything a researcher would reasonably paste into a message belongs
in the URL. Everything else is transient.

## H.4 Message contract

Engine and kernel exchange typed messages; no shared mutable object.

```fsharp
type FromKernel =
    | RouteChanged of path: string * query: (string * string) list
    | PopState     of path: string * query: (string * string) list
    | IndexLoaded  of name: string * payload: string
    | IndexFailed  of name: string * reason: string
    | UserIntent   of Intent

and Intent =
    | OpenArtifact of ArtifactKey
    | FollowEdge   of from: ArtifactKey * relation: Relation
    | SetQuery     of string
    | ToggleFilter of facet: string * value: string
    | SelectLens   of Lens
    | CopyCitation of ArtifactKey
    | ToggleSource

type ToKernel =
    | Render        of ViewModel
    | PushUrl       of path: string * query: (string * string) list
    | ReplaceUrl    of path: string * query: (string * string) list
    | RequestIndex  of name: string
    | WriteClipboard of text: string
    | Persist       of key: string * value: string
```

`Render` carries a fully-resolved `ViewModel` — the kernel never decides what a
relationship means, only how to paint what it is handed.

## H.5 Routing and history

- The engine maps URL → intent and produces the view model; the kernel applies
  `pushState` and reports `popState`.
- Back/forward work because every navigation that changes the URL goes through
  `PushUrl`, and `PopState` is fed straight back to the engine.
- GitHub Pages has no rewrite rules, so deep links need the existing
  `404.html` fallback to bootstrap the app and re-dispatch the path. This
  already exists in the current output (`dist/404.html`) and is preserved.

## H.6 Progressive enhancement — what works without WASM

**PROPOSAL:** every artifact is *also* a static HTML page generated by the F#
core at build time (see I and J). The Limen app is an enhancement layer for
cross-cutting navigation, search and relationship traversal.

Consequences:
- Search engines and agents get real HTML with real links.
- WASM startup size (risk R7 in N) cannot make research unreadable.
- The static page and the app share one URL scheme.

## H.7 What must NOT be in the kernel

Stated explicitly because §1.4 requires it:

- interpreting `related_documents` value kinds
- deciding what a status phrase means
- ranking search results
- deriving backlinks
- formatting a citation
- distinguishing canonical from derived relationships

All of these are research interpretation and belong to the F# engine.
