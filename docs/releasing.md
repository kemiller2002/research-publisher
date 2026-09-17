# Releasing

The supported release path is the `Publish npm Package` GitHub Actions workflow
(`.github/workflows/publish.yml`), which runs on every push to `main`. A
developer machine is not the authoritative release process.

## What the workflow does

1. Installs Node 24 and the .NET 8 SDK.
2. `npm ci`.
3. `npm run lifecycle:test` — F# unit and architecture tests.
4. `npm run lifecycle:build:all` — publishes every supported runtime into
   `runtimes/`.
5. `npm run research:test` — the publishing engine's vitest suites.
6. `npm run research:build` — builds the demo site.
7. `npm pack --dry-run` — reviews the package contents.
8. `npm run smoke:package` — packs the real archive, installs it into a clean
   temporary repository and runs every documented command against it.
9. Chooses the next version: the local version if it is ahead of the published
   one, otherwise a patch bump.
10. Confirms `research-publisher --version` reports the version being published.
11. `npm publish`.

Publishing happens only after all of that passes.

## Version source

`package.json` is the single authoritative version. The CLI reads it from the
package it ships inside, so `--version` cannot drift from the published release
and no version constant is maintained by hand. The workflow's bump therefore
takes effect without rebuilding the F# binaries, and step 10 proves it.

## Releasing by hand

Only for an emergency, and it still runs the same gates:

```bash
npm ci
npm run lifecycle:test
npm run lifecycle:build:all
npm run research:test
npm pack --dry-run
npm run smoke:package
npm version <version> --no-git-tag-version
npm publish
```

## Reviewing what gets published

```bash
npm pack --dry-run     # the file list
npm pack               # the real archive
tar -tzf echelon-foundry-research-publisher-*.tgz
```

The package's contents are controlled by the `files` allow-list in
`package.json`. There is no `.npmignore`; a single allow-list avoids two sources
of truth disagreeing about what ships. `npm run smoke:package` asserts that the
launcher, the runtime assets and at least one platform executable are present,
and that build reports, coverage, test results and the engine cache are not.

Published contents:

| Path | Why |
| --- | --- |
| `bin/` | The Node launcher. |
| `runtimes/<rid>/` | The F# executable for each supported platform. |
| `src/` | The JavaScript publishing engine. |
| `site/src`, `site/astro.config.mjs`, `site/package.json` | Astro templates rendered at build time. |
| `prompts/mark-research-documents.md` | Runtime template written by `init`. |
| `docs/*.md` (selected) | User-facing documentation linked from the README. |
| `README.md` | The npm package page. |

## Supported platforms

`npm run lifecycle:build:all` publishes self-contained, trimmed, single-file
executables for `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`.
They are all bundled in one package and the launcher picks the matching one, so
no `os` or `cpu` restriction is set — that would stop the multi-platform package
from installing.

Each executable is 12-13 MiB, so the complete package is about 35 MB packed and
65 MB unpacked — larger than a pure JavaScript one. In exchange the consumer
needs no .NET runtime: Node alone is enough.

The alternative is per-platform optional dependencies, which would download only
the matching binary at the cost of publishing five extra packages on every
release. That trade is deliberately not taken yet: one package keeps the release
process that already exists, and the launcher already resolves the right binary,
so moving to optional dependencies later would not change the command contract.

A platform with no packaged binary fails with exit code `7` and a message naming
the runtimes that are available. Only lifecycle commands fail; the JavaScript
publishing commands keep working.

## Cross-platform coverage

`.github/workflows/lifecycle.yml` packs and exercises the archive on
`ubuntu-latest`, `windows-latest` and `macos-latest`, and separately builds every
runtime identifier. A change that breaks packaging on one operating system fails
there before it can be published.

## Licensing

`package.json` does not declare a `license` field and the repository has no
licence file. npm will publish without one, but consumers cannot tell what terms
apply. Choosing a licence is an owner decision and is deliberately not made here.
