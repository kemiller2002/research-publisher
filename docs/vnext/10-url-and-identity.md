# J. URL and Identity Proposal

## J.1 Constraints

- **OBSERVED:** current scheme is `/research/<slug>/` and
  `/collections/<facet>/<value>/` (175 facet pages).
- **OBSERVED:** slug derives from title when `id` is absent — true for 68 of
  117 authored documents, so those URLs are already unstable (A.8, D.2).
- **OBSERVED:** 569 artifacts carry a declared id (49 authored + 520 frontier).
- GitHub Pages: no rewrites; `/x/` serves `/x/index.html`; `404.html` fallback.

## J.2 Proposed scheme

```
/                                           corpus landing
/p/{project}                                project overview
/p/{project}/frontier                       frontier view
/p/{project}/frontier/{RFR-id}              one frontier record
/a/{id}                                     artifact by declared id      (569 today)
/a/{id}#{section-slug}                      section anchor               (best effort)
/s/{path-key}                               artifact with no declared id (68 today)
/rel/{id}                                   relationship view for an artifact
/integrity                                  integrity view
/search?q=…&type=…&project=…                search
/collections/{facet}/{value}/               PRESERVED verbatim
```

**Why `/a/{id}` rather than `/research/{slug}/`:** the id is the only stable
handle the corpus offers. Putting the title in the URL is what made the current
scheme fragile.

**`/s/{path-key}`** is a hash of the repository-relative source path. It is
stable across retitling (the current scheme's failure mode) and across content
edits, and changes only if the file moves — which is a real identity change.
It does **not** invent an id: nothing is written back to the corpus, and the
artifact continues to report "no declared id" in the UI and in the indexes.

## J.3 Compatibility and redirects

| Current URL class | Count | Disposition |
| --- | --- | --- |
| `/research/<id-anchored-slug>/` | 569 | permanent alias → `/a/{id}` |
| `/research/<title-derived-slug>/` | ~68 | permanent alias → `/s/{path-key}` |
| `/collections/<facet>/<value>/` | 175 | **kept as-is**, no redirect needed |

Implementation on GitHub Pages: emit an HTML redirect stub
(`<meta http-equiv="refresh">` + `<link rel="canonical">`) at every legacy path,
plus a machine-readable `redirects.json` in the published contracts.

**Regression gate (see L):** the build compares the emitted URL set against the
previously published set and **fails** if any previously published URL is
neither emitted nor redirected. This is the one URL-related blocking obligation
(G.5).

## J.4 Identity rules

1. Never invent an `id` (§28).
2. Never derive a public URL from a title.
3. A declared `id` is authoritative and permanent.
4. A `path-key` URL is stable but explicitly second-class; the UI should invite
   the author to add an `id`.
5. Section anchors are best-effort. If a heading is reworded, the anchor
   degrades to the document URL — it must never 404 and never silently point at
   the wrong section (C.5).

## J.5 Open question

**OPEN QUESTION:** the 520 frontier records are regenerable and content-hashed
(`RFR-B186B831`). If a source document is edited, does the hash change, and do
existing frontier URLs break? This is a property of the frontier generator, not
of the publisher, and it should be confirmed with its owner before those URLs
are advertised as permanent.
