# ADR 0009 — Canonical and derived relationships are distinct types

**Status:** Accepted

## Context
§9 and §28 warn that publication-time derived relationships must never be
mistaken for canonical research.

## Decision
`CanonicalRelation` and `DerivedRelation` are separate F# types unified only at
the edge boundary. Every derived edge is tagged `derived: true` in output and is
visually distinct in the UI. Derivations that are lossy (citation normalisation,
section anchoring) carry explicit confidence.

## Evidence
OBSERVED: the corpus encodes 6 canonical relation kinds and no others. Useful
navigation aids — backlinks, same-project, frontier-of — are computable but are
*not* research claims. OBSERVED: `references` is bibliography, and a parser that
treated it as linkage would generate 100% false relationships (C.3).

## Alternatives
One relation type with a flag — rejected: a flag can be forgotten; a type cannot.

## Consequences
Slightly more code at the boundary. That is the point.

## Reversibility
High cost — this is a correctness boundary, not a convenience.
