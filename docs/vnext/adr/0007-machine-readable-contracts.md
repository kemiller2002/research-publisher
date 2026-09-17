# ADR 0007 — Sharded, versioned, HTML-free publication contracts

**Status:** Accepted

## Context
OBSERVED: `research-catalog.json` is 6.3 MB, 4.0 MB of it inline rendered HTML.
§18 requires bounded agent retrieval without a server.

## Decision
Publish `/data/v1/` with a small `manifest.json` entry point, an HTML-free
`artifacts.json`, and per-artifact shards `artifact/{key}.json` and
`edges/{key}.json`. Every index carries `schemaVersion`; every derived edge
carries `derived: true`; absent data is `null`.

## Evidence
OBSERVED: the §18 example resolves in two fetches, ~5 KB, against the real
corpus (K.3). Prior art exists in-repo: `frontier-index.json` /
`frontier-graph.json` already shard and version this way.

## Alternatives
Keep one catalog — rejected: it forces whole-corpus download for one field.

## Consequences
More output files. Acceptable: `dist/` already carries 1,776 tracked files.

## Reversibility
Low — contracts are versioned and the v1.1 catalog remains as a shim.
