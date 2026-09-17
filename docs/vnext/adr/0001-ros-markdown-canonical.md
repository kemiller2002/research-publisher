# ADR 0001 — ROS Markdown remains canonical

**Status:** Accepted (constraint, §1.1)

## Context
The corpus is 837 Markdown files across two populations (B.0). ROS governs it
through `ros.json`, registries and the work protocol.

## Decision
ROS Markdown is the sole canonical research record. The publisher is read-only
with respect to it. Typed runtime models, derived indexes and generated HTML are
projections, never replacements.

## Evidence
OBSERVED: every relationship, status and identity in the corpus originates in
Markdown front matter or body text. No other store exists. DOCUMENTED ROS RULE:
`ros validate` and the registries already own artifact governance (D.4).

## Alternatives
Convert to JSON or a database — rejected by §1.1 and unjustified: 756 documents
build in 5.6 s, so no performance argument exists.

## Consequences
The parser must absorb all corpus heterogeneity (D.1). This is the cost of the
constraint and is accepted.

## Reversibility
Reversing would mean a migration of canonical research — expensive and
explicitly out of scope.
