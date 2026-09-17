# Research Publishing Workflow

This page describes the publishing pipeline. For installing, verifying and
upgrading the capability itself, see [the command reference](./cli.md).

## Local Commands

- `npm ci`
- `npm run research:inventory`
- `npm run research:validate`
- `npm run research:build`
- `npm run research:build:fixture`
- `npm test`

In a consumer repository the same stages run through the package executable:

```bash
npx @echelon-foundry/research-publisher inventory --config ./research-publisher.config.mjs
npx @echelon-foundry/research-publisher validate --config ./research-publisher.config.mjs
npx @echelon-foundry/research-publisher build --config ./research-publisher.config.mjs
```

## Build Stages

1. Discover Markdown according to config include and exclude globs.
2. Parse front matter and Markdown structure.
3. Normalize metadata into a stable catalog record.
4. Validate duplicate identifiers and URLs.
5. Build relationship graph and collection groupings.
6. Render the Astro site from generated JSON.
7. Generate Pagefind search assets.
8. Verify required output artifacts.

## Deployment

The GitHub workflows keep application logic in npm scripts. `Validate Research` runs the lifecycle and publishing checks on pull requests and pushes, `Lifecycle CLI` packs the npm archive and exercises it on Linux, Windows and macOS, `Publish Research` deploys the built static site to GitHub Pages, and `Publish npm Package` releases the package.

