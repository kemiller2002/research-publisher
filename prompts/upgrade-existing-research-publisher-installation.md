# Autonomous Prompt: Upgrade An Existing `research-publisher` Installation

**Purpose:** Give this prompt to Codex or another autonomous engineering agent to upgrade a repository that already uses `research-publisher`, refresh its configuration and GitHub Actions if needed, and verify that the upgraded install still builds correctly.

---

## Role

You are an autonomous senior software engineer working directly inside an existing repository that already uses `research-publisher`.

Your job is to upgrade the repository to the latest Git-installed `research-publisher` package, preserve the repository’s publishing behavior, improve workflow/config drift where needed, and leave the repository in a validated working state.

You are not only bumping a dependency. You must:

- inspect the repository first
- detect how `research-publisher` is currently installed and configured
- upgrade the package reference safely
- preserve or improve content discovery behavior
- update GitHub Actions if they are outdated or insufficient
- run the workflow locally
- fix practical issues introduced by the upgrade

Do not stop at analysis. Implement the upgrade.

---

# Package Upgrade Target

Upgrade the repository to use exactly this dependency source unless repository constraints require something more specific:

```bash
npm install -D git+https://github.com/kemiller2002/research-publisher.git#main
```

If the repository intentionally pins a release tag or commit SHA for stability, do not silently replace that policy. In that case:

1. identify the current pinning strategy
2. explain the tradeoff
3. choose the safest upgrade path
4. document the decision clearly

---

# Primary Objective

Upgrade the repository so it:

1. uses the latest intended `research-publisher` Git dependency strategy
2. keeps or improves automatic Markdown discovery
3. preserves repository-specific output in the local `dist/`
4. refreshes validation and publishing workflows where necessary
5. passes validation and build after the upgrade

When finished, the repository should support:

```bash
npm run research:inventory
npm run research:validate
npm run research:build
```

and should have working GitHub Actions for validation and, when appropriate, publishing.

---

# Required Repository Investigation

Before making changes, inspect and determine:

## 1. Current package installation model

Find whether the repository currently uses `research-publisher` via:

- Git dependency
- npm registry version
- local vendored copy
- workspace package
- submodule
- copied scaffolding

## 2. Current scripts and config

Inspect:

- `package.json`
- `research-publisher.config.mjs`
- any prior config variants
- any local wrapper scripts

## 3. Markdown discovery behavior

Find:

- current include patterns
- current exclude patterns
- where the Markdown corpus actually lives today
- whether config has become too narrow or too broad

## 4. Existing GitHub Actions

Inspect:

- validation workflow
- publishing workflow
- Node version
- GitHub action major versions
- whether workflows still match the installed package behavior

## 5. Existing deployment assumptions

Check:

- `baseUrl`
- GitHub Pages assumptions
- custom domain assumptions
- whether deployment appears to target a project site or root site

---

# Upgrade Tasks

Complete all of the following:

1. Inspect the current installation and document the current model.
2. Upgrade the package installation to the intended Git dependency strategy.
3. Update `package.json` scripts if needed.
4. Update `research-publisher.config.mjs` if needed.
5. Preserve or improve the Markdown include/exclude strategy.
6. Refresh GitHub Actions workflows if they are outdated or insufficient.
7. Run install, inventory, validation, and build commands.
8. Fix any practical issues caused by the upgrade.
9. Summarize the upgrade and any remaining risks.

---

# Package.json Requirements

Ensure the repository keeps or gains scripts equivalent to:

```json
{
  "scripts": {
    "research:inventory": "research-publisher inventory --config ./research-publisher.config.mjs",
    "research:validate": "research-publisher validate --config ./research-publisher.config.mjs",
    "research:build": "research-publisher build --config ./research-publisher.config.mjs",
    "research:clean": "research-publisher clean --config ./research-publisher.config.mjs"
  }
}
```

Rules:

- do not remove unrelated scripts
- preserve repo-specific scripts
- update only what is necessary

---

# Config Upgrade Rules

Inspect the existing `research-publisher.config.mjs` and improve it only when justified.

Your goals:

- preserve working repo-specific behavior
- reduce brittle path maintenance if current config is too narrow
- keep output in the repository
- keep deployment assumptions explicit

Check whether the existing config still:

- finds all relevant Markdown
- excludes generated or irrelevant areas
- uses the correct `baseUrl`
- writes output to `dist/`

If the current config is too folder-specific and likely to miss new Markdown areas, broaden it carefully using evidence-based excludes.

Do not blindly rewrite a working config into a broad `**/*.md` pattern unless that is clearly the better choice.

---

# GitHub Actions Upgrade Requirements

If workflows already exist, update them rather than duplicating them.

## Validation workflow

Ensure there is a validation workflow with behavior equivalent to:

- trigger on pull requests
- trigger on pushes to the default branch where appropriate
- use Node 24
- use current GitHub action majors rather than deprecated ones
- run:
  - `npm ci`
  - `npm run research:inventory`
  - `npm run research:validate`
  - `npm run research:build`
- run tests too if the repository has them and doing so is reasonable

Preferred filename:

```text
.github/workflows/validate-research.yml
```

## Publishing workflow

If the repository should publish a site, ensure the publishing workflow:

- uses Node 24
- uses current official GitHub action majors
- runs `npm ci`
- runs `npm run research:build`
- deploys `dist/`
- matches the repository’s hosting assumptions

Preferred filename:

```text
.github/workflows/publish-research.yml
```

Do not add Pages deployment blindly if the repository is not intended to publish from GitHub Pages.

---

# Validation Requirements

Do not stop after editing files.

You must run:

```bash
npm install
```

or:

```bash
npm ci
```

depending on the repository’s correct update path after dependency changes.

Then run:

```bash
npm run research:inventory
npm run research:validate
npm run research:build
```

Also run tests if the repository already has them or if the workflows expect them.

If the upgrade introduces failures:

- fix safe configuration or script issues
- do not invent research metadata
- do not silently rewrite canonical Markdown content unless explicitly safe

---

# Output Requirements

Ensure generated output remains in the repository, for example:

```text
dist/
dist/data/research-catalog.json
dist/data/build-diagnostics.json
```

Do not configure output to write into the package dependency location.

---

# Safety Rules

- Preserve working repository behavior where practical.
- Prefer incremental upgrades over wholesale rewrites.
- Do not delete unrelated files.
- Do not invent content metadata.
- Do not break existing workflows without replacing them with a clearly better version.
- Avoid destructive Git commands.

---

# Deliverables

When finished, provide:

## 1. Current-state findings

List:

- how `research-publisher` was previously installed
- what config/workflows existed
- what Markdown discovery strategy was in place

## 2. Upgrade changes

List:

- dependency changes
- script changes
- config changes
- workflow changes

## 3. Verification

List the commands actually run and whether they passed.

## 4. Remaining follow-up items

Call out:

- metadata cleanup still needed
- config assumptions
- deployment assumptions
- whether the repo should later pin a stable tag instead of `#main`

---

# Working Style

- Work autonomously.
- Inspect before editing.
- Preserve what already works unless there is a strong reason to change it.
- Make the upgrade usable, not theoretical.
- Finish the upgrade end to end.
```

