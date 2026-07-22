# Research Publisher Architecture

## Repository Baseline

The repository contained only the engineering prompt on July 22, 2026. That meant there was no incumbent build stack, no legacy routing scheme, and no existing content taxonomy beyond what the prompt required. The first implementation therefore optimized for a clean reusable boundary rather than migration compatibility with an existing renderer.

## Hypothesis 1: A static site is sufficient

- Hypothesis: The research corpus can be published entirely as static HTML with no backend.
- Why it might be correct: Markdown is the canonical source, the corpus is read-heavy, and the deployment target is GitHub Pages.
- Evidence: The repository content is file-based, version-controlled, and does not require authentication or mutable state.
- Failure modes: Search performance could degrade if the corpus becomes extremely large, or future workflows could require authenticated editorial features.
- Test: Built the system around a static HTML pipeline with generated JSON artifacts and Pagefind search.
- Conclusion: Static generation is the correct version 1 architecture.
- Confidence: 0.9
- Could change if: The corpus outgrows static search bundles or introduces authenticated authoring features.

## Hypothesis 2: Astro is preferable to a custom HTML generator

- Hypothesis: Astro offers the best tradeoff between static rendering, reusable layouts, and low-JavaScript pages.
- Why it might be correct: It provides routing, layout composition, and static output without forcing client-side hydration.
- Evidence: The site uses Astro layouts and catch-all static paths while keeping document content server-rendered.
- Failure modes: Astro adds framework dependency weight and a build boundary between normalization and rendering.
- Test: The reusable package writes normalized JSON, then Astro consumes it to render the site.
- Conclusion: Astro is a good fit because it reduces template and routing maintenance while keeping the core package framework-neutral from the data-contract perspective.
- Confidence: 0.81
- Could change if: Future reuse requires rendering to non-Astro targets from the same package.

## Hypothesis 3: The reusable system should be a package, not copied scaffolding

- Hypothesis: Reuse should happen through a package boundary and configuration, not through copied files.
- Why it might be correct: Copying scaffolding creates drift and makes upgrades expensive.
- Evidence: Both the main project and the fixture project use the same package root with different config files.
- Failure modes: Repo-local packaging can still couple consumers to workspace structure if the interfaces are not kept stable.
- Test: Built the fixture project under `fixtures/alt-research/` without duplicating the core pipeline.
- Conclusion: A repo-local package is the right stage-A implementation.
- Confidence: 0.88
- Could change if: Additional repositories require independent npm distribution.

## Hypothesis 4: Pagefind should power full-text search

- Hypothesis: Pagefind is a strong fit for full-text search over static output.
- Why it might be correct: It indexes rendered HTML after build and does not require a hosted service.
- Evidence: The build invokes Pagefind after Astro generation and the search page imports the generated client bundle.
- Failure modes: Very large corpora, advanced ranking controls, or language-specific stemming could require a different search layer.
- Test: Implemented client-side search using the generated Pagefind bundle and metadata filters.
- Conclusion: Pagefind is the right default, paired with a separate public catalog.
- Confidence: 0.84
- Could change if: Search requirements become more specialized than Pagefind can support statically.

## Hypothesis 5: Metadata must be normalized through a schema

- Hypothesis: Markdown front matter will drift and needs normalization plus compatibility handling.
- Why it might be correct: Autonomous authors and legacy documents rarely stay consistent over time.
- Evidence: The repository includes both canonical front matter documents and a legacy Markdown file with no front matter.
- Failure modes: Overly permissive compatibility mode can hide data quality problems.
- Test: Implemented normalization, legacy aliases, compatibility inference, duplicate detection, and explicit diagnostics.
- Conclusion: Normalization is necessary and should remain strict enough to surface drift.
- Confidence: 0.92
- Could change if: A stricter authoring contract is enforced across all repositories.

## Hypothesis 6: The public catalog should be generated independently

- Hypothesis: The JSON metadata catalog must exist independently from the search index.
- Why it might be correct: Search internals are not a stable public API for downstream tools.
- Evidence: The build writes `research-catalog.json`, `research-graph.json`, and collection JSON separately from Pagefind output.
- Failure modes: Schema sprawl if the catalog is allowed to drift without documentation.
- Test: The site and future external consumers can read the catalog independently from search.
- Conclusion: The public catalog is a first-class artifact and should remain versioned.
- Confidence: 0.94
- Could change if: A stronger external contract supersedes the current catalog schema.

## Hypothesis 7: Templates should use semantic slots, not document-specific HTML

- Hypothesis: Layouts should be reusable by artifact semantics instead of filename-specific logic.
- Why it might be correct: It keeps repository-specific assumptions out of the engine.
- Evidence: The site uses `BaseLayout`, `ResearchDocumentLayout`, and `IndexLayout`, with content grouped by normalized metadata.
- Failure modes: Artifact-specific needs may eventually require more layout branches.
- Test: Rendered both projects with the same layout set and different branding variables.
- Conclusion: Semantic layouts are the right version 1 template model.
- Confidence: 0.8
- Could change if: Additional artifact classes require materially different content presentation.
