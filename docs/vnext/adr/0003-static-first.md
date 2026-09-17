# ADR 0003 — Static-first publishing to GitHub Pages

**Status:** Accepted

## Context
§1.6 prefers static output; §20 requires the decision be grounded in measured
corpus size.

## Decision
Static generation only. No server, no database, no runtime services.

## Evidence
OBSERVED: 756 documents; total build 5,592 ms (parse 2,737 / render 1,751 /
index 869); output 36 MB; no authentication or mutable state anywhere in the
corpus. The architecture document's Hypothesis 1 (confidence 0.9) is confirmed
by these numbers.

## Alternatives
A query service for bounded agent retrieval — rejected: K.3 shows two static
fetches answer the §18 example in ~5 KB.

## Consequences
All retrieval must be expressible as pre-computed files. Sharding is therefore a
first-class design concern (K.2).

## Reversibility
Low cost — re-evaluate if the corpus grows by an order of magnitude or gains
authenticated authoring.
