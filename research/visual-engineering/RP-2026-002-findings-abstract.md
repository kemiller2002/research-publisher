---
id: RP-2026-002
title: Research Publisher Findings Abstract
artifactType: research-summary
project: research-publisher
purposes:
  - orient
  - decide
  - integrate
  - apply
audiences:
  - general
  - executive
  - practitioner
  - contributor
entryPoint: true
entryPointOrder: 5
entryPointLabel: Findings abstract
researchArea: Visual Engineering
discipline:
  - Information Architecture
  - Web Architecture
summary: A short synthesis of what the Research Publisher work established, why it matters, and how to use it.
status: active
version: "1.0"
confidence: 0.86
completion: 1
priority: high
authorAgent: codex
created: 2026-08-04
updated: 2026-08-04
tags:
  - synthesis
  - publishing
  - practical-guidance
relatedDocuments:
  - RP-2026-001
  - EV-2026-001
  - HY-2026-001
  - TH-2026-001
  - CN-2026-001
evidenceIds:
  - EV-2026-001
hypothesisIds:
  - HY-2026-001
theoryIds:
  - TH-2026-001
---

## Abstract

Research Publisher demonstrates that a version-controlled Markdown corpus can become a readable, searchable, and reusable research site without a hosted application backend. The durable system is not merely a static-site template: it separates source discovery, metadata normalization, relationship modeling, public data generation, page rendering, and search indexing so multiple repositories can share one publishing engine without copying their implementation.

The central practical finding is that readers and machines need different—but coordinated—ways into the same corpus. People need short entry documents, explicit project guides, readable pages, and links between findings. Search and downstream tools need a stable catalog that does not depend on the internal format of the search engine. The publisher therefore generates semantic HTML, a Pagefind index, a versioned JSON catalog, relationship data, collections, and ordered project guides from the same Markdown sources.

## Findings

### Static publishing is sufficient for the current problem

The corpus is read-oriented, version-controlled, and does not require authenticated editorial state. A static build is therefore simpler to operate and easier to preserve than a database-backed publishing application. This conclusion is supported by the [static-site sufficiency evidence](../evidence/EV-2026-001-static-site-sufficiency.md).

### Search and structured data are separate products

Full-text search is optimized for retrieval, while a public catalog is a stable interface for agents, dashboards, and other tools. Treating the search index as the public data contract would couple consumers to Pagefind internals. The [public catalog concept](../concepts/CN-2026-001-public-catalog.md) records this separation.

### Reuse depends on architectural separation

Repositories can share the publisher because normalization and validation do not depend on the Astro presentation layer. A second fixture repository builds through the same engine with different content and branding. This supports the [separation-of-concerns theory](../theories/TH-2026-001-separation-of-concerns.md).

### Compatibility must be visible, not silent

Legacy Markdown remains publishable, but inferred metadata produces diagnostics. This lets repositories adopt the system incrementally without pretending that inferred titles, dates, identifiers, or relationships have the same authority as explicit metadata.

### A corpus needs deliberate front doors

Individual papers are insufficient as the only reading experience. Project ownership, reader purpose, audience, and entry-point order must be represented separately from artifact type and subject tags. This enables an abstract like this one to lead readers into detailed evidence without changing the epistemic identity of the underlying documents.

## Practical Uses

- **For leaders:** begin with project abstracts and decision-oriented entry points rather than reading every source document.
- **For practitioners:** use purpose filters to find application, reference, and verification material.
- **For researchers:** follow stable identifiers, related-document links, evidence links, hypotheses, theories, and backlinks to inspect support and uncertainty.
- **For repository maintainers:** install one package, classify Markdown with explicit metadata, and generate the site and data artifacts in CI.
- **For tools and agents:** consume `research-catalog.json`, `research-graph.json`, and `research-guides.json` instead of scraping presentation HTML.

## Current Limits

The present evidence comes from a small main corpus and one fixture repository. Static generation and Pagefind remain hypotheses at much larger corpus sizes. Compatibility mode also leaves some older documents without canonical identifiers. The publisher organizes and exposes research; it cannot create a trustworthy synthesis when the source documents do not contain one.

## Reading Path

1. Read the [reusable publishing system](./RP-2026-001-research-publisher.md) for the full project context.
2. Inspect the [static-site evidence](../evidence/EV-2026-001-static-site-sufficiency.md) and [stack hypothesis](../hypotheses/HY-2026-001-astro-pagefind.md).
3. Review the [metadata schema](../../docs/research-metadata-schema.md) to classify additional documents.
4. Use the generated project, purpose, and audience collections to move from synthesis into detail.
