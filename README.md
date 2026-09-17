# Research Publisher

Publish a repository's Markdown research as a searchable static website, and keep
the publishing capability installed, verified and up to date.

`@echelon-foundry/research-publisher` is an Echelon Foundry engineering tool. It
does two things:

- **Publishing.** Discovers Markdown through configurable globs, normalises and
  validates research metadata, generates a public catalog and relationship graph,
  renders a static Astro site and builds a Pagefind full-text search index.
- **Lifecycle.** Installs itself into a repository, reports what state that
  repository is in, verifies it, diagnoses problems and migrates it forward when
  the tool changes.

## Quick start

```bash
# Install
npm install -D @echelon-foundry/research-publisher

# Bring the repository into a valid installed state
npx @echelon-foundry/research-publisher init

# Confirm what is installed
npx @echelon-foundry/research-publisher status

# Validate the installation
npx @echelon-foundry/research-publisher verify

# Build the site
npm run research:build
```

`init` is safe to run again at any time. Review `research-publisher.config.mjs`
after the first run — in particular `site.siteUrl` and `site.baseUrl`, which the
template leaves as placeholders.

Every command above is executed against the real packed npm archive in CI on
Linux, Windows and macOS before a release is published.

## Lifecycle commands

| Command | What it does | Writes? |
| --- | --- | --- |
| `init` | Brings the repository into a valid installed state. Idempotent. | Yes |
| `status` | Reports installation, artifact, integration and verification state. | No |
| `verify` | Validates that the capability is correctly installed. | No |
| `upgrade` | Moves an existing installation to this release, one migration at a time. | Yes |
| `doctor` | Explains what is wrong and how to fix it. | No |

```bash
npx @echelon-foundry/research-publisher init
npx @echelon-foundry/research-publisher status
npx @echelon-foundry/research-publisher verify
npx @echelon-foundry/research-publisher upgrade
npx @echelon-foundry/research-publisher doctor

npx @echelon-foundry/research-publisher --help
npx @echelon-foundry/research-publisher --version
```

`--help` works per command too: `npx @echelon-foundry/research-publisher init --help`.

## Publishing commands

`build`, `dev`, `validate`, `inventory`, `check-links`, `clean`, `preview` and
`migrate` render and check the corpus. They take `--config <path>`, defaulting to
`./research-publisher.config.mjs`. `init` registers npm scripts for the common
ones:

```bash
npm run research:inventory   # what the content globs discover
npm run research:validate    # front matter, identifiers and links
npm run research:build       # render the site and build the search index
npm run research:clean       # remove generated output
```

See [docs/cli.md](./docs/cli.md) for the full reference.

## Supported environments

- Node.js 24 (`engines` declares `>=24 <26`; CI tests Node 24).
- Windows x64, Linux x64, Linux ARM64, macOS x64, macOS ARM64. The package
  bundles a self-contained executable for each and the launcher picks the right
  one.
- **No .NET runtime is required.** The lifecycle CLI is written in F# and shipped
  as a self-contained binary.
- No shell is assumed. Nothing depends on Bash or PowerShell behaviour.

The lifecycle commands are exercised against the packed npm archive on Linux,
Windows and macOS in CI. The publishing commands, which render the site through
Astro and Pagefind, are exercised on Linux and macOS.

On a platform with no packaged binary, lifecycle commands fail with exit code `7`
and a clear message. The publishing commands still work.

## What `init` does, precisely

`init` means *bring this repository into a valid installed state* — not *copy
some files*. It is safe, repeatable and idempotent: running it again when nothing
is missing produces no change at all, which is checked by an automated test
against the packed archive.

It may create:

| Path | Ownership |
| --- | --- |
| `research-publisher.config.mjs` | user-owned |
| `prompts/research-publisher-mark-documents.md` | shared |
| `.echelon/research-publisher.json` | tool-owned |
| `research:*` scripts in `package.json` | shared, additive only |

It will never overwrite a file you own, never change a script you already
defined, and never replace a shared file you edited — it reports a conflict and
leaves your version alone.

Nothing happens during `npm install`. There is no `postinstall` hook, because
installing a dependency should not rewrite the repository that depends on it.

Full detail: [docs/installation.md](./docs/installation.md).

## File ownership

| Ownership | Meaning |
| --- | --- |
| **tool-owned** | Controlled by the tool; replaced by explicit version rules. |
| **generated** | Derived output; regenerated freely. |
| **user-owned** | Yours. Created once if missing, then never rewritten. |
| **shared** | Both. Refreshed only while unmodified; a local edit becomes a reported conflict. |

## Installation manifest

`.echelon/research-publisher.json` records what is installed: the package
version, the configuration version, every managed path with its ownership, and a
content hash for the paths the tool may want to refresh. It contains no
timestamps, no machine-specific values and no secrets. Commit it.

`.echelon/` is the shared root for Echelon Foundry tooling; each tool owns one
manifest inside it.

## Upgrading

```bash
npm install -D @echelon-foundry/research-publisher@latest
npx @echelon-foundry/research-publisher upgrade --dry-run
npx @echelon-foundry/research-publisher upgrade
```

Upgrades run sequential migrations between configuration versions rather than
deleting and recopying files. A repository installed before this release had no
manifest; it is detected, adopted and migrated without its content being changed.

If a migration precondition fails, nothing is written and the command exits `5`.
Guarantees, supported paths and recovery behaviour:
[docs/upgrading.md](./docs/upgrading.md).

## Dry run

```bash
npx @echelon-foundry/research-publisher init --dry-run
npx @echelon-foundry/research-publisher upgrade --dry-run --json
```

A dry run inspects the repository, calculates the full plan, validates it and
reports every change, every path left alone, every conflict and every blocker —
without writing anything. The plan is produced by the same code that executes it,
so it is exact.

`--check` is the CI form: it changes nothing and exits `3` when changes would be
needed.

## Machine-readable output

```bash
npx @echelon-foundry/research-publisher status --json
npx @echelon-foundry/research-publisher verify --json
npx @echelon-foundry/research-publisher doctor --json
npx @echelon-foundry/research-publisher init --dry-run --json
npx @echelon-foundry/research-publisher upgrade --dry-run --json
```

With `--json`, stdout carries exactly one JSON document and nothing else; human
text moves to stderr. Every document carries a versioned `schema` field. Schemas
are documented in [docs/cli.md](./docs/cli.md#machine-readable-output).

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | Internal failure |
| `2` | Invalid arguments |
| `3` | Verification failed, or `--check` found pending changes |
| `4` | Incompatible installation |
| `5` | Migration blocked; nothing was changed |
| `6` | Prerequisite or environment failure |
| `7` | Unsupported platform |

## Using it in CI

```yaml
- run: npm ci
- run: npx @echelon-foundry/research-publisher verify --strict
- run: npx @echelon-foundry/research-publisher upgrade --check
- run: npm run research:validate
- run: npm run research:build
```

`verify --strict` treats warnings as failures — an undeclared dependency, a
missing script, a configuration module without a default export. It never reports
a problem that the normal mode would have hidden.

## Using it from an agent

Lifecycle commands never prompt, so they cannot hang waiting for input, and
nothing is approved implicitly: a command either makes its change or, with
`--dry-run` or `--check`, reports what it would do and stops.

```bash
npx @echelon-foundry/research-publisher status --json          # read the state
npx @echelon-foundry/research-publisher init --dry-run --json  # inspect the plan
npx @echelon-foundry/research-publisher init --json            # apply it
npx @echelon-foundry/research-publisher verify --json          # confirm
```

Blocked commands report a `blockers` array where each entry carries a
`remediation` string.

## Configuration

`research-publisher.config.mjs` at the repository root controls the site, the
content globs, metadata rules, output paths, features and branding. It is
user-owned: the tool creates it once and never rewrites it.

Host repositories can override semantic colour roles without copying package CSS:

```js
branding: {
  cssVariables: {
    "--color-accent": "#2457a6",
    "--color-accent-strong": "#173b73",
    "--color-accent-soft": "#dce8fa"
  }
}
```

Unspecified roles keep their package defaults. See
[Theming](./docs/theming.md).

## Organising a corpus

Research Publisher keeps document type, project ownership, reader purpose,
audience and front-page placement separate, so free-form tags do not become an
unstable navigation system. See
[Document Purpose And Project Guide Architecture](./docs/document-purpose-taxonomy.md).
`init` installs the reusable corpus-classification prompt at
`prompts/research-publisher-mark-documents.md`.

## Compatibility

- The commands, flags, exit codes, JSON schemas and manifest schema are treated
  as a public API. Schemas are versioned in the documents themselves.
- Every configuration version that has shipped keeps a migration path to the
  current one.
- Commands that existed before the lifecycle interface still work.
  `install-prompt` remains as a legacy compatibility command, and the older
  JavaScript entry points still work, delegating to the same implementation.

## Development

```bash
npm ci
npm run lifecycle:build     # build the F# CLI for this platform
npm test                    # F# tests, then the vitest suites
npm run smoke:package       # pack the package and exercise the real archive
npm run research:build      # build the demo site in this repository
```

Requires Node.js 24 and the .NET SDK 8.0. The lifecycle is implemented in F#; the
Node layer is a launcher. See [docs/development.md](./docs/development.md).

## Releasing

Releases are produced by the `Publish npm Package` GitHub Actions workflow, which
builds every runtime, runs every test suite, packs the archive, exercises it and
only then publishes. See [docs/releasing.md](./docs/releasing.md).

## Troubleshooting

Start with:

```bash
npx @echelon-foundry/research-publisher doctor
```

It separates errors from warnings and information, and each finding carries a
remediation. For build-time problems in the corpus itself, see
[Troubleshooting research builds](./docs/troubleshooting-research-builds.md).

## Documentation

| Document | Contents |
| --- | --- |
| [docs/installation.md](./docs/installation.md) | `init` semantics, ownership model, manifest |
| [docs/cli.md](./docs/cli.md) | Full command reference, exit codes, JSON schemas |
| [docs/upgrading.md](./docs/upgrading.md) | Migration model and guarantees |
| [docs/development.md](./docs/development.md) | Contributor workflow |
| [docs/releasing.md](./docs/releasing.md) | Release and publishing process |
| [docs/document-purpose-taxonomy.md](./docs/document-purpose-taxonomy.md) | Corpus organisation |
| [docs/research-metadata-schema.md](./docs/research-metadata-schema.md) | Front matter fields |
| [docs/theming.md](./docs/theming.md) | Colour roles and accessibility |

## About this repository

This repository is the package itself, and also a demo research corpus that the
package publishes to GitHub Pages. That is why it contains `research/`, a second
fixture project under `fixtures/` and its own `research-publisher.config.mjs`,
and why it has `.echelon/research-publisher.json` like any other consumer.
