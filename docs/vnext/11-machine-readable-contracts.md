# K. Machine-Readable Contract Proposal

## K.1 The problem being fixed

**OBSERVED:** `research-catalog.json` is **6.3 MB**, of which **4.0 MB is
inline rendered HTML** (median 3,383 bytes per record across 756 records). Any
consumer wanting a single field must download the whole site's body text.

§18 asks whether bounded retrieval can be supported statically. It can — by
splitting metadata from content and sharding by object.

## K.2 Proposed artifacts

All under `/data/v1/`, all carrying `schemaVersion`, all deterministic (no
wall-clock stamps inside content-addressed files).

| Artifact | Shape | Est. size | Purpose |
| --- | --- | --- | --- |
| `manifest.json` | engine version, contract versions, corpus digest, project list, counts, link to every other index | ~4 KB | entry point; one fetch tells an agent what exists |
| `artifacts.json` | one row per artifact: key, id, type + typeSource, project, status text + class, purposes, audiences, summary, provenance, url. **No HTML.** | ~400 KB for 756 | listing, filtering, faceting |
| `artifact/{key}.json` | full artifact: sections, unknown keys, references, edges, provenance | ~4 KB each | **bounded retrieval — the point of the design** |
| `edges.json` | every edge with `relation`, `derived: true|false`, `confidence`, `evidence` span | ~250 KB | relationship traversal |
| `edges/{key}.json` | in- and out-edges for one artifact, pre-resolved | ~1 KB each | one-fetch neighbourhood |
| `findings.json` | validation findings with severity, span, remedy | ~30 KB | integrity view, CI |
| `search-index.json` | typed index: text + type + project + purposes | see K.4 | search |
| `redirects.json` | legacy URL → canonical URL | ~40 KB | D.2 / J.3 |
| `provenance.json` | artifact key → repo, path, commit | ~80 KB | source inspection |

## K.3 The §18 worked example, statically

> "Give me finding F-27 plus supporting evidence, contradicting evidence,
> related experiments, unresolved questions, and source locations."

Against the real corpus, for an artifact `RP-COMP-005`:

```
GET /data/v1/artifact/RP-COMP-005.json      →  the artifact + provenance
GET /data/v1/edges/RP-COMP-005.json         →  in/out edges, typed
   → source-rep in:  EX-COMP-011, EX-COMP-012, DF-COMP-002, TH-COMP-005
   → frontier-of:    the RFR records originating from it
```

**Two fetches, ~5 KB**, versus 6.3 MB today. No server.

**Honest limitation:** "supporting evidence" and "contradicting evidence" are
**not answerable**, because the corpus does not encode those relations (C.4).
The contract returns the relations that exist and names the ones it cannot
supply, rather than inferring them — §28 forbids using AI to infer canonical
research relationships without explicit evidence.

## K.4 Search index sizing

**OBSERVED:** Pagefind currently produces 5.3 MB across 813 files, indexing
rendered HTML.

**PROPOSAL:** build the index in F# from the typed model, not from HTML, so it
can carry `type`, `project`, `purposes` and `status` as real fields. Sharded by
project (8 projects) so a project view fetches only its own shard.

Rough sizing from measured content: 756 documents, 4.0 MB of rendered text
overall. A term index over title + summary + headings + body, sharded, should
land well under the current 5.3 MB, and the largest single shard far below it.
**This must be measured in the slice, not assumed** (see M's exit criteria).

## K.5 Contract rules

1. Every index carries `schemaVersion`; breaking changes bump the major.
2. `derived: true` on every non-canonical edge — a consumer must never mistake
   a publication convenience for research (§9).
3. Absent data is `null`, never a default. A consumer can tell "unknown" from
   "declared".
4. Every fact carries provenance sufficient to locate it in the source.
5. `manifest.json` is the only entry point a consumer needs to memorise.
