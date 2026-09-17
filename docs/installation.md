# Installation

## Recommended

```bash
npm install -D @echelon-foundry/research-publisher
npx @echelon-foundry/research-publisher init
```

`init` is the canonical way to install the capability into a repository. Nothing
happens during `npm install`: the package has no `preinstall`, `install` or
`postinstall` script, because installing a dependency should never rewrite the
repository that depends on it.

The repository must already be an npm project. If there is no `package.json`,
`init` refuses to run and says to run `npm init -y` first.

## What `init` means

> Bring this repository into a valid installed state for research publishing.

It is not "copy some files". Each run:

1. inspects the repository,
2. determines the current installation state,
3. determines the desired state for this release,
4. validates the prerequisites,
5. calculates the changes,
6. detects conflicts,
7. applies the changes,
8. writes the installation manifest,
9. reports what it did.

`init` is safe to repeat. Running it when nothing is missing produces no changes
at all — not "harmless changes", none. The repository is byte-for-byte identical,
which is checked by an automated idempotency test against the packed npm
artifact.

Use `--dry-run` to see the plan first, and `--check` in CI to fail when the
repository has drifted:

```bash
npx @echelon-foundry/research-publisher init --dry-run
npx @echelon-foundry/research-publisher init --check
```

## What `init` creates

| Path | Ownership | Created when |
| --- | --- | --- |
| `research-publisher.config.mjs` | user-owned | Missing |
| `prompts/research-publisher-mark-documents.md` | shared | Missing |
| `.echelon/research-publisher.json` | tool-owned | Always kept current |
| `package.json` scripts `research:inventory`, `research:validate`, `research:build`, `research:clean`, `research:status`, `research:verify`, `research:doctor` | shared | Any are missing |

## What `init` will not do

- It never overwrites `research-publisher.config.mjs`. Once the file exists it
  belongs to the repository.
- It never changes a script the repository already defines, even if the command
  differs from the one the tool would have written.
- It never replaces a shared file the repository has edited. It reports a
  conflict instead and leaves the file alone.
- It never deletes generated output.
- It never runs a script from the repository, downloads an executable, or writes
  a credential anywhere.

## File ownership model

Every managed path has an explicit classification, recorded in the manifest.

| Ownership | Meaning | Tool behaviour |
| --- | --- | --- |
| **tool-owned** | Controlled by the tool. | Replaced according to explicit version rules. |
| **generated** | Derived from authoritative inputs. | May be regenerated; never hand-edited. |
| **user-owned** | Controlled by the repository. | Created once if missing, then never rewritten. |
| **shared** | Managed by both. | Refreshed only when unmodified; a local edit is a reported conflict. |

Current classifications:

| Path | Ownership | Notes |
| --- | --- | --- |
| `research-publisher.config.mjs` | user-owned | You are expected to edit this. |
| `prompts/research-publisher-mark-documents.md` | shared | Edit it if you want; upgrades will then leave it alone. |
| `.echelon/research-publisher.json` | tool-owned | Do not edit by hand. |
| `package.json` | shared | Only additive script changes are made. |
| `dist/` | generated | Rendered site output. |
| `.research-publisher/` | generated | Engine cache. |
| `build-reports/` | generated | Diagnostics from validate and build runs. |

A file created by `init` does not stay tool-owned forever. The configuration is
user-owned from the moment it exists, which is why `init` will not rewrite it
even when the template changes.

## Installation manifest

`.echelon/research-publisher.json` is the machine-readable record of what is
installed. The presence of arbitrary files is not used as the source of truth.

```json
{
  "schema": "echelon.tool-installation/1",
  "tool": "research-publisher",
  "package": "@echelon-foundry/research-publisher",
  "installedVersion": "0.1.0",
  "configurationVersion": 2,
  "managedArtifacts": [
    {
      "id": "marking-prompt",
      "path": "prompts/research-publisher-mark-documents.md",
      "ownership": "shared",
      "hash": "sha256:…"
    }
  ],
  "managedScripts": [
    { "name": "research:build", "command": "research-publisher build --config ./research-publisher.config.mjs" }
  ]
}
```

- `schema` is versioned; an unrecognised schema is refused rather than guessed at.
- `configurationVersion` is the repository shape this release understands. It is
  what `upgrade` migrates.
- `hash` is recorded only for paths the tool may later want to refresh, and line
  endings are normalised first so a CRLF checkout is not mistaken for an edit.
- There are no timestamps, no absolute paths, no machine identifiers and no
  secrets. Nothing in the file is time-sensitive, which is what makes `init`
  idempotent.

Commit the manifest. It is how the tool, your CI and an agent all agree on what
state the repository is in.

## Shared `.echelon/` directory

`.echelon/` is the common root for Echelon Foundry tooling. Each tool owns one
manifest inside it and stays authoritative for its own lifecycle state, so tools
coexist without a shared configuration format to fight over.

## After `init`

1. Review `research-publisher.config.mjs` — in particular `site.siteUrl` and
   `site.baseUrl`, which the template leaves as placeholders.
2. Point `content.include` at the Markdown you want published.
3. Run `npm run research:inventory` to see what the globs discover.
4. Run `npm run research:build`.

## Legacy compatibility

Repositories installed before this release have no `.echelon/` manifest. They are
recognised as pre-manifest installations (configuration version 0) and adopted by
`init` or `upgrade` without their content being changed. See
[Upgrading](./upgrading.md).

The older `install-prompt` command still works and now delegates to the same F#
implementation. Prefer `init`.
