# Adding A Research Repository

The canonical setup is [Installation](./installation.md). This page covers the
repository-layout decisions that sit around it.

## Five-Minute Setup

```bash
npm install -D @echelon-foundry/research-publisher
npx @echelon-foundry/research-publisher init
npx @echelon-foundry/research-publisher verify
npm run research:build
```

`init` creates `research-publisher.config.mjs`, installs the shared
document-marking prompt, registers the `research:*` npm scripts and writes the
installation manifest. Afterwards, point `content.include` at the repository's
Markdown corpus and review `site.siteUrl` and `site.baseUrl`.

## Repository-Specific Surface

Keep repository-specific changes limited to:

- `research-publisher.config.mjs`
- Markdown content
- Optional brand variables

The core package, Astro layouts, and search integration should stay shared.

## Git Submodule Layout

Legacy compatibility. The npm package is the supported distribution; this layout
is documented for repositories that already use it.

If you keep the publisher in a submodule, treat the content repository as the project root:

- store `research-publisher.config.mjs` in the main repository
- point `content.include` at paths in the main repository
- keep `output.directory` in the main repository, for example `dist`
- invoke the publisher from the main repository while referencing the config there

In that layout, the publisher package supplies code and templates, while `dist/`, Pagefind output, catalogs, and diagnostics stay in the main repository.

## npm Dependency Layout

The recommended multi-repository setup is the public organization-scoped npm package instead of a submodule:

```bash
npm install -D @echelon-foundry/research-publisher
npx @echelon-foundry/research-publisher init
```

Then keep the consumer repository focused on:

- `research-publisher.config.mjs`
- content directories such as `research/`
- local build output such as `dist/`

Test changes locally by editing the package repository and running `npm test`, `npm run smoke:package`, `npm run smoke:consumer`, and `npm run research:build`. Publish a new semantic version through the release workflow, then update the consumer repository and run `npx @echelon-foundry/research-publisher upgrade`.
