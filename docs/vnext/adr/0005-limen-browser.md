# ADR 0005 — Limen engine/kernel split, with a static HTML baseline

**Status:** Accepted (constraint, §1.4)

## Context
Limen enforces a lexical boundary: engine code must not name `document`,
`window`, `fetch(`, `localStorage`, `sessionStorage`; neither side may `eval`.
The current `limen.config.json` declares an empty boundary because no split
exists yet.

## Decision
F#→WASM is the engine and owns all research interpretation. A thin TypeScript
kernel owns browser capabilities. Every artifact is *also* a static HTML page.

## Evidence
OBSERVED: Limen 0.5.1 verified green against this repository only with an empty
boundary; a realistic mapping produced three LIMEN009 false positives on the
word "document" used as a domain noun. An F# engine cannot produce that class of
false positive, because it never names the global.

## Alternatives
A framework SPA — rejected by §1.6. A pure static site — rejected: relationship
traversal and search need client interaction.

## Consequences
Two rendering paths must agree on URLs. Mitigated by generating both from the
same F# projections.

## Reversibility
Moderate — the static baseline could stand alone if WASM proved unworkable.
