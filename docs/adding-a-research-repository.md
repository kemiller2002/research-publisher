# Adding A Research Repository

## Five-Minute Setup

1. Install the `research-publisher` package into the repository.
2. Add a `research-publisher.config.mjs` file with site, content, output, and branding settings.
3. Point `content.include` at the repository's Markdown corpus.
4. Run `npm ci`.
5. Run `npm run research:build`.

## Repository-Specific Surface

Keep repository-specific changes limited to:

- `research-publisher.config.mjs`
- Markdown content
- Optional brand variables

The core package, Astro layouts, and search integration should stay shared.

## Git Submodule Layout

If you keep the publisher in a submodule, treat the content repository as the project root:

- store `research-publisher.config.mjs` in the main repository
- point `content.include` at paths in the main repository
- keep `output.directory` in the main repository, for example `dist`
- invoke the publisher from the main repository while referencing the config there

In that layout, the publisher package supplies code and templates, while `dist/`, Pagefind output, catalogs, and diagnostics stay in the main repository.

## Git Dependency Layout

The recommended multi-repository setup is a Git dependency instead of a submodule:

```bash
npm install -D git+https://github.com/kemiller2002/research-publisher.git#v0.1.0
```

Then keep the consumer repository focused on:

- `research-publisher.config.mjs`
- content directories such as `research/`
- local build output such as `dist/`

Test changes locally by editing the package repository, running `npm test`, `npm run smoke:consumer`, and `npm run research:build`, then tag a release and update the consumer repository to that tag.
