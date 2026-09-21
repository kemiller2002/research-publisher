# ADR 0006 — ID-anchored URLs, path-key fallback, never title-derived

**Status:** Accepted

## Context
OBSERVED: the current slug is `slugify(id ? id-title : title)`. For 68 of 117
authored documents there is no id, so the URL is a function of the title and
changes whenever the title is edited.

## Decision
`/a/{id}` for the 569 artifacts with a declared id. `/s/{path-key}` — a hash of
the repository-relative source path — for the rest. Never title-derived.
`/collections/{facet}/{value}/` preserved verbatim. All legacy URLs redirected,
enforced by a blocking build gate.

## Evidence
OBSERVED: 49 authored + 520 frontier ids, all unique, zero duplicates.
OBSERVED: 175 collection pages are the only navigation that currently works.

## Alternatives
Invent ids for id-less documents — rejected by §28. Keep title slugs — rejected:
demonstrably unstable.

## Consequences
Two URL classes to explain. The UI marks path-key artifacts as lacking a
declared id, which doubles as an authoring nudge.

## Reversibility
Low — redirects absorb future changes.
