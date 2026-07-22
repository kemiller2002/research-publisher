# Autonomous Prompt: Install `research-publisher` On An Existing Repository

**Purpose:** Give this prompt to Codex or another autonomous engineering agent to inspect an existing repository, install `research-publisher`, discover the real Markdown corpus, and configure a maintainable static research publishing workflow without requiring constant folder-path updates.

---

## Role

You are an autonomous senior software engineer working directly inside an existing repository.

Your job is to install and configure the `research-publisher` package so the repository can build a searchable static website from its Markdown corpus.

You are not only writing config files. You must:

- inspect the repository first
- discover where Markdown actually lives
- distinguish publishable Markdown from irrelevant or generated Markdown
- configure broad but safe content discovery
- run the installed workflow
- set up GitHub Actions when missing or insufficient
- fix practical setup issues
- leave the repository in a usable state

Do not stop at a plan. Implement the setup.

---

# Package Installation Command

Install the package using exactly this command:

```bash
npm install -D git+https://github.com/kemiller2002/research-publisher.git#main
```

Do not substitute a tag, local path, or npm registry version unless the command above is impossible and you document the reason clearly.

---

# Primary Objective

Set up this repository so it can:

1. discover relevant Markdown automatically
2. build a static site from repository Markdown
3. generate inventory, validation, catalog, diagnostics, and search artifacts
4. keep generated output inside the current repository
5. avoid requiring frequent future updates to content path lists
6. use GitHub Actions for validation and, when appropriate, publishing

When finished, the repository should support:

```bash
npm run research:inventory
npm run research:validate
npm run research:build
```

and the built site should exist in:

```text
dist/
```

---

# Key Constraint: Avoid Fragile Path Whitelists

Do not configure the publisher in a way that requires manually updating the content folder list every time the repository gains another meaningful Markdown area.

The preferred strategy is:

- broad include patterns
- carefully chosen exclude patterns
- exclusions based on repository evidence

Only narrow the include patterns if the repository structure proves that broad discovery would be unsafe or noisy.

Good default direction:

- include most Markdown
- exclude generated, vendored, archived, temporary, and clearly irrelevant areas

Bad default direction:

- only include two or three guessed folders when the repository contains relevant Markdown elsewhere

---

# Required Repository Investigation

Before editing anything, inspect the repository and determine:

## 1. Markdown distribution

Find:

- all `.md` files
- major Markdown clusters
- isolated Markdown files at root level
- nested Markdown areas

## 2. Likely publishable content

Identify folders that appear to contain actual research or knowledge content, such as:

- `research/`
- `docs/`
- `notes/`
- `journal/`
- `journals/`
- `evidence/`
- `theories/`
- `hypotheses/`
- `concepts/`
- `glossary/`
- `knowledge/`
- `content/`
- `wiki/`
- `playbooks/`
- `roadmaps/`
- `architecture/`

Do not assume these folders exist. Inspect the repo.

## 3. Likely exclusions

Identify Markdown that should probably not be published, such as content under:

- `node_modules/`
- `dist/`
- `build/`
- generated site output
- vendored dependencies
- cache directories
- package manager caches
- temporary directories
- `.git/`
- `.github/`
- `archive/`
- `archives/`
- imported raw intake folders
- machine-generated diagnostics

## 4. Existing site or docs pipeline

Check whether the repository already has:

- a static site generator
- documentation build scripts
- GitHub Pages workflow
- GitHub Actions validation workflow
- existing `package.json` scripts
- existing site output directories

Do not break existing workflows unless the user’s request clearly requires replacement.

---

# Required Changes

Complete all of the following:

1. Install `research-publisher` with the exact Git dependency command above.
2. Add the required scripts to the repository root `package.json`.
3. Create `research-publisher.config.mjs` at the repository root.
4. Configure content discovery based on actual repository evidence.
5. Configure output to write into the current repository.
6. Create or update GitHub Actions workflows if they are missing or insufficient.
7. Run inventory, validation, and build commands.
8. Fix practical configuration errors you discover.
9. Summarize the discovered content strategy and any remaining issues.

---

# Required `package.json` Scripts

Ensure the repository root `package.json` contains these scripts:

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

- do not remove unrelated existing scripts
- merge safely with the current `package.json`
- preserve existing formatting style if practical

---

# Required Root Config File

Create:

```text
research-publisher.config.mjs
```

Use this shape as the default starting point, but adapt it to the repository:

```js
export default {
  site: {
    title: "REPLACE_ME",
    description: "Searchable research repository",
    baseUrl: "/",
    language: "en",
    siteUrl: "https://example.com"
  },
  repository: {
    name: "REPLACE_ME",
    sourceUrl: "REPLACE_ME"
  },
  content: {
    include: [
      "**/*.md"
    ],
    exclude: [
      "node_modules/**",
      "dist/**",
      ".git/**",
      ".github/**",
      ".research-publisher/**",
      "coverage/**",
      "tmp/**",
      "temp/**",
      "**/archive/**",
      "**/archives/**"
    ],
    drafts: false
  },
  output: {
    directory: "dist",
    catalog: "data/research-catalog.json",
    diagnostics: "data/build-diagnostics.json"
  }
};
```

Adapt this intelligently.

For example:

- if `docs/` should be published, your exclude rules must not wipe it out accidentally
- if the repo contains generated Markdown reports, exclude those if they are not canonical content
- if the repo has raw intake folders that should not publish, exclude them

---

# Base URL Rules

If the repository appears intended for a GitHub Pages **project site**, set:

```js
baseUrl: "/<repo-name>/"
```

If the repository is intended for:

- local-only use
- root-domain hosting
- a custom domain at site root

then keep:

```js
baseUrl: "/"
```

Make the decision based on repository evidence such as:

- existing GitHub Pages workflow
- README deployment instructions
- repository name conventions
- explicit site URL configuration

If uncertain, choose the safest default and document the assumption.

---

# Content Discovery Strategy Requirements

Prefer a strategy that minimizes future maintenance.

## Preferred pattern

Use broad include globs such as:

```js
include: ["**/*.md"]
```

paired with evidence-based exclusions.

## Acceptable refinement

If broad include would definitely pull in too much irrelevant Markdown, refine the excludes or use a slightly narrower include such as:

```js
include: [
  "research/**/*.md",
  "docs/**/*.md",
  "notes/**/*.md",
  "*.md"
]
```

But only do this if repository structure makes it necessary.

## Avoid

- tiny hardcoded include lists based on guesswork
- config that will miss newly added Markdown sections next month
- silent exclusion of meaningful content

---

# Validation and Build Requirements

Do not stop after editing config.

You must actually run:

```bash
npm run research:inventory
npm run research:validate
npm run research:build
```

If the repository has severe content issues:

- do not invent research metadata
- do not silently rewrite user content unless explicitly safe and necessary
- use compatibility mode where appropriate
- surface which files are problematic

If the build works with warnings, document them clearly.

---

# GitHub Actions Requirements

If suitable workflows do not already exist, create them.

At minimum, the repository should end with:

## 1. Validation workflow

Preferred path:

```text
.github/workflows/validate-research.yml
```

Required behavior:

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

## 2. Publishing workflow

Preferred path:

```text
.github/workflows/publish-research.yml
```

Add this when repository evidence suggests the site should publish from GitHub Pages or another obvious GitHub-based deployment path.

Required behavior:

- trigger on push to the default branch and/or manual dispatch
- use Node 24
- run `npm ci`
- run `npm run research:build`
- publish `dist/`
- use official GitHub Pages deployment actions when GitHub Pages is clearly the intended target

## Workflow rules

- If equivalent workflows already exist, update them instead of duplicating them.
- Prefer official GitHub actions.
- Prefer current major versions rather than deprecated ones.
- If deployment target is ambiguous, document the assumption clearly.
- Do not force a Pages deployment workflow if the repository does not appear intended for Pages.

---

# Output Requirements

Generated output must stay in the current repository.

Required output paths:

```text
dist/
dist/data/research-catalog.json
dist/data/build-diagnostics.json
```

If generated graph output exists, it should also be produced under:

```text
dist/data/
```

Do not configure the package to write generated files back into the package dependency repository.

---

# Safety Rules

- Treat repository Markdown as canonical source material.
- Do not overwrite user content without clear justification.
- Do not invent research identifiers, confidence scores, or relationships.
- Do not delete unrelated files.
- Do not break unrelated package scripts.
- Avoid destructive Git commands.

---

# Deliverables

When you finish, provide:

## 1. Repository findings

List:

- the Markdown locations discovered
- the areas treated as publishable
- the areas excluded from publishing

## 2. Configuration rationale

Explain:

- include strategy
- exclude strategy
- why this setup minimizes future path maintenance

## 3. Files changed

List the files created or edited.

## 4. Commands run

List the install, inventory, validation, and build commands actually executed.

## 5. Remaining risks or follow-up work

Call out:

- legacy Markdown needing cleanup
- metadata gaps
- folders that may need later refinement
- Pages/base URL assumptions
- GitHub Actions assumptions or deployment decisions

---

# Working Style

- Work autonomously.
- Inspect before editing.
- Prefer practical, maintainable decisions.
- Make the setup usable, not theoretical.
- Do not stop at partial setup.

Complete the installation end to end.
```
