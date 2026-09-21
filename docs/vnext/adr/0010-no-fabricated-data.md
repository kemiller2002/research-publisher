# ADR 0010 — Absent data stays absent

**Status:** Accepted

## Context
OBSERVED: the current normaliser substitutes `created`/`updated` with the
literal `"2026-07-22"`, `researchArea` with `"General Research"`, `status` with
`"draft"`, `authorAgent` with `"unknown"`, `version` with `"0.1"`.

## Decision
No defaulting. Absent values are `None` in the model and `null` in output.
Derived readings (e.g. `StatusClass`) are published alongside the verbatim
source value, never instead of it.

## Evidence
OBSERVED: 735 of 756 records carry the fabricated date and 742 of 756 the
default research area. Any timeline or facet built on them is misleading, and a
consumer cannot distinguish a real value from a substituted one.

## Alternatives
Keep defaults for convenience — rejected: it makes the output untrustworthy,
which is fatal for a research record.

## Consequences
Views must handle missing data — e.g. "recent activity" is omitted from slice 1
rather than faked (I.2).

## Reversibility
Low cost.
