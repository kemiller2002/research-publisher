# D. Compatibility Report

What must keep working, what may change, and what the change costs.

## D.1 Corpus compatibility — can existing ROS research be used unchanged?

**Answer: yes, and it must be.**

**OBSERVED:** every construct in the corpus is machine-readable today; the
current engine simply declines to read most of it. Nothing in the corpus is
malformed in a way that requires editing:

- 117 authored docs: 116 have YAML front matter, 1 does not (archived duplicate)
- 49/49 authored IDs unique; 520/520 frontier IDs unique; zero collisions
- 878 canonical relationship instances resolve
- 9 dangling references — legitimate research states, to be warned not fixed

**The required change is entirely in the parser**, per §1.2. Specifically
vNext must read, without any file edits:

| Must read | Because |
| --- | --- |
| `document_type` **and** `artifactType` | 49 vs 4 files |
| both `-` and `_` casings of every type value | `experiment_report` and `experiment-report` both exist |
| `related_documents` **and** `relatedDocuments` | 23 vs 1 files |
| file-relative **and** repo-root-relative paths | 18 vs 22 values |
| free-text values in link fields, as non-links | 8 values |
| `research_area` **and** `researchArea` | 6 vs 8 files |
| `superseded_by`, `source_rep`, `evidence_level`, `author_agent`, `related_projects` | currently discarded |
| `date` / `created` / `updated` as alternates | three competing keys, 38 files each |
| `abstract` **and** `summary` | 26 vs 19 files |
| absent `id` | 68 of 117 authored docs |
| absent front matter | 1 file |

**Target: zero manual migrations.** Any valid existing artifact that vNext
cannot represent is a design defect (§1.2).

## D.2 URL compatibility

**OBSERVED** current scheme:

```
/research/<slug>/                ~700 pages
/collections/<facet>/<value>/    175 pages
```

### The uncomfortable finding

`slug = slugify(id ? `${id}-${title}` : title)`.

For the 68 of 117 authored documents with no `id`, **the URL is a function of
the title**. These URLs are already unstable: any retitling silently breaks
them, and has presumably done so historically.

**INFERENCE:** "preserve existing URLs" is therefore a weaker obligation than
it first appears. There is no guarantee that today's URLs match yesterday's for
the majority of documents.

### Proposed compatibility posture

| Class | Count | Posture |
| --- | --- | --- |
| ID-bearing documents | 49 authored + 520 frontier | **Preserve exactly.** These slugs are `${id}-${title}` derived but ID-anchored; keep a permanent alias. |
| Title-derived documents | 68 authored | **Preserve current slug as a redirect**, but mint a new stable, path-derived URL as canonical. |
| Collection pages | 175 | **Preserve `/collections/<facet>/<value>/`.** Cheap to keep, and they are the only working navigation today. |

Emit a `redirects.json` (and GitHub Pages-compatible HTML redirect stubs)
mapping every currently published URL to its vNext canonical URL. Build should
fail if a previously published URL disappears without a redirect entry
(see L, regression tests).

## D.3 Machine-readable contract compatibility

**OBSERVED** current published contracts:

| Artifact | Version | Consumers known |
| --- | --- | --- |
| `data/research-catalog.json` | `schemaVersion: 1.1` | the Astro site; "future external consumers" per the architecture doc |
| `data/research-graph.json` | `schemaVersion: 1.0` | site graph feature |
| `data/research-collections.json` | — | site |
| `data/build-diagnostics.json` | `schemaVersion: 1.0` | CI |
| `research/frontier/frontier-index.json` | — | frontier tooling |
| `research/frontier/frontier-graph.json` | — | frontier tooling |

**INFERENCE:** no external consumer is demonstrable from the repositories
inspected. The architecture document (Hypothesis 6) asserts the catalog is "a
first-class artifact and should remain versioned", which is a stated intent
rather than an observed dependency.

**PROPOSAL:** keep emitting `research-catalog.json` at `schemaVersion: 1.1`
with its current record shape for one release as a compatibility shim, while
publishing the new bounded contracts (see K) alongside. Retire it only after a
release in which nothing reports breakage.

**Note the defect to not carry forward:** the current catalog inlines 4.0 MB of
rendered HTML into a 6.3 MB metadata file. The vNext contract must separate
metadata from content.

## D.4 ROS contract compatibility

**DOCUMENTED ROS RULE:** ROS owns `ros.json`, `registries/`, `.ros/`,
`schemas/`, and the work protocol. The publisher must not write into any of
them.

**OBSERVED:** the current publisher does not read ROS at all — no registry, no
`ros.json`, no `.ros/`. vNext *should* read `ros.json` (for `canonicalRoots`
and project identity) and the registries (as an authoritative artifact list),
but must remain read-only with respect to ROS state.

**OBSERVED constraint:** the VE research corpus lives under `content/projects/**`,
which is **outside** the canonical roots ROS declares in `research-publisher`'s
own `ros.json` (`research/journals`, `research/packages`, `research/theories`,
`research/evidence`). vNext cannot assume ROS canonical roots locate the
corpus; discovery must stay configuration-driven.

## D.5 Build and deployment compatibility

**OBSERVED:** GitHub Actions → GitHub Pages; `dist/` committed to the
repository (1,776 tracked files).

Constraints vNext inherits:

- Static hosting only; no server-side request handling.
- GitHub Pages serves `/path/` → `/path/index.html`; no rewrite rules, so
  client-side routing needs a 404 fallback (one already exists: `dist/404.html`).
- A committed `dist/` means build output diffs land in review. vNext should
  keep output **deterministic** so diffs are meaningful — the current build
  stamps `generatedOn`, which the vNext contract should move out of
  content-addressed files.

## D.6 What must remain stable — summary

| Contract | Stability |
| --- | --- |
| ROS Markdown as canonical source | **Absolute.** Never written by the publisher. |
| `/collections/<facet>/<value>/` URLs | Preserve. |
| ID-anchored document URLs | Preserve, with permanent aliases. |
| Title-derived URLs | Redirect to new stable URLs. |
| `research-catalog.json` v1.1 | One-release shim, then retire. |
| `frontier-index.json` / `frontier-graph.json` | Not the publisher's to change; consume read-only. |
| GitHub Pages static hosting | Preserve. |
