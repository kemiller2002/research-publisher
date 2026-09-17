# ADR 0008 — Search built from the typed model, not from rendered HTML

**Status:** Accepted

## Context
OBSERVED: Pagefind indexes rendered HTML after the build (5.3 MB, 813 files) and
therefore cannot express research structure. Filters are client-side refinements
over result metadata.

## Decision
Build the index in F# from `Artifact`, carrying `type`, `project`, `purposes`
and `status` as real fields. Shard per project.

## Evidence
OBSERVED: with 708/756 documents typed `research-document`, a type filter over
today's index is meaningless — fixing the parser is a precondition for useful
search either way.

## Alternatives
Keep Pagefind — rejected: it cannot answer type-aware queries, and it adds a
second toolchain alongside the F# core.

## Consequences
Ranking must be implemented rather than inherited. Slice 1 defers search, so
this is proven before it is depended upon.

## Reversibility
Moderate — Pagefind could be reinstated over the generated HTML.
