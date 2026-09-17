# Research Publisher vNext — Discovery Summary

Status: discovery complete
Date: 2026-09-17
Corpus inspected: `kemiller2002/visual-engineering` @ shallow clone of `main`
Engine inspected: `kemiller2002/research-publisher` @ `10aa01f`

Every claim below carries an evidence class: **OBSERVED**, **DOCUMENTED ROS
RULE**, **INFERENCE**, **PROPOSAL**, or **OPEN QUESTION**.

---

## The headline number

> **OBSERVED:** The published relationship graph for the real research corpus
> contains **6 edges across 756 nodes**. 749 of 756 documents (99.1%) are
> completely isolated.
>
> Source: `dist/data/research-graph.json` in `visual-engineering`, fields
> `nodes` (756) and `edges` (6, all `type: "related-document"`).

The corpus is not the problem. The corpus encodes **63 relationship values** in
`related_documents` / `relatedDocuments`, of which **54 resolve** to real
targets. The publisher derives 6 of them.

This single measurement explains most of what is meant by "the current
publishing experience is ineffective", and it is a parser/contract defect, not
a presentation defect.

---

## The vNext requirements draft arrived mid-discovery

**OBSERVED:** when discovery began, no requirements draft existed in any
reachable repository. I searched `research-publisher`,
`repository-operating-system` and `visual-engineering` — every `*.md`, any
filename matching `*requirement*`, and content matching `vnext` / `v-next` /
`next generation` / `redesign` near `research publisher`. Zero matches, at
~19:40 UTC.

`input-documents/research-publisher-vnext-requirements.txt` (1,021 lines, 31
sections) was committed at 19:49 UTC as `550b1b9`, after that search.

This sequencing is worth stating plainly: **the evidence in these documents was
gathered independently of the requirements.** Where the two agree, it is
convergence rather than confirmation bias; where they conflict, the conflict was
not manufactured to fit a conclusion.

Deliverable E was rewritten against the real document once it arrived, and
deliverable O is now a revision of it rather than an original. Nothing in
`input-documents/` was modified.

## What was actually inspected

| Source | What it gave |
| --- | --- |
| `research-publisher/src/**/*.mjs` (1,335 lines) | the real parsing, normalization, relationship and validation contract |
| `research-publisher/research-publisher.config.mjs` | engine configuration surface |
| `visual-engineering/content/**` (117 docs) | authored research artifacts |
| `visual-engineering/research/frontier/**` | frontier records (the bulk of the 837 files) |
| `visual-engineering/dist/**` (1,776 tracked files, 36 MB) | **the real generated output**, committed |
| `visual-engineering/dist/data/*.json` | catalog, graph, collections, diagnostics — measured, not estimated |
| `research-publisher/.sde/**` | SDE four-tier doctrine and construction method |
| `.echelon/limen.json`, `limen.config.json` | Limen installation state |

Measurements come from the committed build output, so they are what the system
actually produced, not a re-run under different conditions.

---

## Document index

| # | Deliverable | File |
| --- | --- | --- |
| A | Current Publisher Inventory | `01-current-publisher-inventory.md` |
| B | ROS Research Format Catalog | `02-ros-format-catalog.md` |
| C | Relationship Catalog | `03-relationship-catalog.md` |
| D | Compatibility Report | `04-compatibility-report.md` |
| E | Requirements Gap Analysis | `05-requirements-gap-analysis.md` |
| F | Proposed F# Domain Model | `06-fsharp-domain-model.md` |
| G | SDE State Model | `07-sde-state-model.md` |
| H | Limen Interaction Model | `08-limen-interaction-model.md` |
| I | Proposed Research Experience | `09-research-experience.md` |
| J | URL and Identity Proposal | `10-url-and-identity.md` |
| K | Machine-Readable Contract Proposal | `11-machine-readable-contracts.md` |
| L | Regression Test Plan | `12-regression-test-plan.md` |
| M | First Vertical Slice Plan | `13-vertical-slice.md` |
| N | Risk Register | `14-risk-register.md` |
| O | Updated Requirements (v0.2.0, revises the prior draft) | `15-requirements-v0.2.0.md` |
| — | ADRs | `adr/` |
| — | Final Analysis (task §29) | `16-final-analysis.md` |
| — | Answers to the requirements draft's §29 | `17-requirements-section-29-answers.md` |
