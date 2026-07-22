# Research Publishing Workflow

## Local Commands

- `npm ci`
- `npm run research:inventory`
- `npm run research:validate`
- `npm run research:build`
- `npm run research:build:fixture`
- `npm test`

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

The GitHub workflows keep application logic in npm scripts. Validation runs on pull requests and pushes, while publishing deploys the built static site to GitHub Pages.

