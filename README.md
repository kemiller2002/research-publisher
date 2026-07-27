# Research Publisher

Reusable Markdown-to-static-web publishing package for research repositories.

## What This Repo Is

This repository is the installable publisher package. The repository root is the package root, which makes it consumable directly from Git.

Consumer repositories should keep:

- `research-publisher.config.mjs`
- `research/`
- `input-documents/`
- generated `dist/`

This repo also contains a demo research corpus and GitHub Pages site at the root so the package can be exercised locally before release.

## Responsibilities

- Discover Markdown content through config globs
- Normalize and validate research metadata
- Generate a public catalog and relationship graph
- Generate project guides from explicit entry-point metadata
- Render a static Astro site
- Generate a Pagefind full-text search index

## Commands

- `research-publisher inventory --config ./research-publisher.config.mjs`
- `research-publisher validate --config ./research-publisher.config.mjs`
- `research-publisher build --config ./research-publisher.config.mjs`
- `research-publisher build --config ./fixtures/alt-research/research-publisher.config.mjs`
- `research-publisher install-prompt --config ./research-publisher.config.mjs`
- `npm run smoke:consumer`

## Local Testing

- `npm test` runs unit and integration tests
- `npm run research:build` builds the demo site in this repo
- `npm run smoke:consumer` packs the package and installs it into a temporary consumer project to verify package-style usage

## Using From Git

In a consuming repository:

```bash
npm install -D git+https://github.com/kemiller2002/research-publisher.git#v0.1.0
```

Then add scripts like:

```json
{
  "scripts": {
    "research:build": "research-publisher build --config ./research-publisher.config.mjs",
    "research:validate": "research-publisher validate --config ./research-publisher.config.mjs",
    "research:inventory": "research-publisher inventory --config ./research-publisher.config.mjs"
  }
}
```

The engine resolves Astro and Pagefind from the installed package, while content discovery, diagnostics, and generated search files stay in the consuming repository.

## Organizing Different Kinds Of Reading

Research Publisher keeps document type, project ownership, reader purpose, audience, and front-page placement separate. This prevents free-form tags from becoming an unstable navigation system.

See [Document Purpose And Project Guide Architecture](./docs/document-purpose-taxonomy.md). Install the reusable corpus-classification prompt in a consumer repository with:

```bash
npx research-publisher install-prompt --config ./research-publisher.config.mjs
```
