# ADR 0004 — F# owns parsing, domain, validation, relationships and projection

**Status:** Accepted (constraint, §1.5)

## Context
The current core is 1,335 lines of JavaScript whose defects are type-shaped:
silently dropped keys, silently dropped edges, defaults indistinguishable from
data.

## Decision
F# implements discovery, parsing, the domain model, validation, relationship
resolution, projections, index generation and tests. JavaScript/TypeScript is
limited to the Limen kernel.

## Evidence
OBSERVED: the three largest defects (708/756 mistyped, 6 edges, 735 fabricated
dates) are all cases where a value silently became something else. Discriminated
unions and explicit `option` make each of them unrepresentable rather than
merely unlikely.

## Alternatives
Keep JS with stricter tests — rejected: the failures are contract failures, and
§1.5 sets F# as the constraint.

## Consequences
.NET toolchain in CI (already present — the lifecycle CLI is F#/net8.0). WASM
payload becomes a real concern (R7), mitigated by the static HTML baseline.

## Reversibility
High cost. This is a foundational choice.
