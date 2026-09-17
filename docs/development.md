# Development

## What lives where

```
bin/                                  Node bootstrap (launcher only, no logic)
src/                                  JavaScript publishing engine
src/ResearchPublisher.Lifecycle.Core/ F# lifecycle domain and services
src/ResearchPublisher.Lifecycle.Cli/  F# command-line adapter
tests/                                vitest suites and F# test projects
scripts/                              build and smoke-test scripts
site/                                 Astro templates rendered by the engine
```

The lifecycle (what "installed" means, what to create, what is valid, what must
change between versions) is owned by F#. The Node bootstrap may detect the
platform, find the executable, report environment facts, forward arguments and
streams and return the exit code — nothing else. An architecture test fails the
build if the bootstrap starts mentioning lifecycle concepts.

Inside the F# core the flow is one direction only:

```
inspect -> determine desired state -> calculate transition -> validate -> execute -> verify
```

Inspection never mutates. Planning is pure with respect to an inspection, which
is why `--dry-run` is exact rather than approximate.

## Prerequisites

- Node.js 24 (`.nvmrc` pins the major version)
- .NET SDK 8.0

## Restore and build

```bash
npm ci
npm run lifecycle:restore
npm run lifecycle:build          # stage the host platform binary into runtimes/
npm run lifecycle:build:all      # stage every supported platform
```

`runtimes/` is generated and git-ignored. The JavaScript engine needs no build
step.

## Test

```bash
npm test                 # F# tests, then the vitest suites
npm run lifecycle:test   # F# unit and architecture tests only
npm run research:test    # vitest only
```

`npm test` runs `npm run lifecycle:build -- --if-missing` first, because the
vitest suites exercise the compatibility wrappers, which delegate to the F# CLI.

```bash
npm run smoke:package    # pack the package and exercise the real archive
npm run smoke:consumer   # install the tarball into a temporary consumer project
```

`npm run smoke:package` is the one that proves distribution works. `dotnet test`
proves the F# code is correct; it says nothing about whether the npm artifact
does.

## Running the CLI from a checkout

```bash
node ./bin/research-publisher.js status
node ./bin/research-publisher.js init --dry-run --json
```

To run a debug build without staging it into `runtimes/`:

```bash
dotnet build src/ResearchPublisher.Lifecycle.Cli/ResearchPublisher.Lifecycle.Cli.fsproj
RESEARCH_PUBLISHER_LIFECYCLE_PATH=$PWD/src/ResearchPublisher.Lifecycle.Cli/bin/Debug/net8.0/linux-x64/research-publisher-lifecycle \
  node ./bin/research-publisher.js status
```

## Environment variables

These exist for the bootstrap and for development. None of them is required in
normal use.

| Variable | Set by | Purpose |
| --- | --- | --- |
| `RESEARCH_PUBLISHER_PACKAGE_ROOT` | bootstrap | Where the package's runtime assets live. |
| `RESEARCH_PUBLISHER_LIFECYCLE_PATH` | developer | Use a specific lifecycle executable. |
| `RESEARCH_PUBLISHER_NODE_VERSION` | bootstrap | Reported to `doctor`, which judges it. |
| `RESEARCH_PUBLISHER_PLATFORM`, `RESEARCH_PUBLISHER_ARCH` | bootstrap | Reported to `doctor`. |

## Adding a configuration version

1. Add the new shape to `Desired.forConfigurationVersion`.
2. Raise `Identity.CurrentConfigurationVersion`.
3. Add one migration to `Migrations.all` moving exactly one version forward, with
   its preconditions and its changes.
4. Add a fixture for the previous shape in `tests/ResearchPublisher.Lifecycle.Core.Tests/UpgradeTests.fs`
   and assert that user-owned content survives it.

Tests already enforce that every migration advances exactly one version and that
the chain covers every version up to the current one.

## Adding a CLI option

1. Add it to the command's allow-list in `Args.extraFlags` and to the option
   record.
2. Document it in `Help.forCommand`.
3. Document it in [docs/cli.md](./cli.md) and, if it is part of the quick start,
   in the README.

`HelpTests` fails if a documented topic has no help; `ArgsTests` fails if an
option is accepted where it should not be.

## Dependency policy

The F# core references only the standard library and `FSharp.Core`; an
architecture test fails if a third-party runtime dependency appears. JSON is
written through explicit field lists rather than reflection-based serialization,
which keeps the published schemas deliberate and keeps the binary trimmable.
