# ADR 0002 — Typed runtime model, not a generalised knowledge graph

**Status:** Accepted

## Context
The corpus has relationships, which invites a generic graph abstraction. §28
forbids creating one merely because relationships exist.

## Decision
Model concrete domain types (F). Relations are a closed set of observed cases
plus an explicit `Other`. No triple store, no schemaless node/edge soup.

## Evidence
OBSERVED: only 6 canonical relation kinds exist in the entire corpus
(`related_documents`, `source_rep`, `supersedes`, `superseded_by`,
`originates`, `prerequisite`). A general graph engine would be built to carry
six edge types.

## Alternatives
RDF/property graph — rejected: unjustified by data volume or variety.

## Consequences
Adding a relation kind is a code change. Given six kinds have appeared over the
corpus's life, that is an acceptable cadence.

## Reversibility
Moderate — the typed edges could be projected into a general graph later.
