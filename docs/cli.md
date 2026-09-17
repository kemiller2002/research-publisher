# Command Reference

`research-publisher` exposes one executable with two groups of commands.

| Group | Commands | Implemented in |
| --- | --- | --- |
| Lifecycle | `init`, `status`, `verify`, `upgrade`, `doctor` | F# (`src/ResearchPublisher.Lifecycle.*`) |
| Publishing | `build`, `dev`, `validate`, `inventory`, `check-links`, `clean`, `preview`, `migrate` | JavaScript (`src/`) |

Run any of them through npm:

```bash
npx @echelon-foundry/research-publisher <command> [options]
```

If the package is installed as a dependency, the same executable is on the path
as `research-publisher`.

## Global options

| Option | Meaning |
| --- | --- |
| `--help`, `-h` | Show help. `<command> --help` documents one command. |
| `--version`, `-v` | Print the package version. |
| `--json` | Emit machine-readable JSON on stdout. |
| `--verbose` | Include additional detail in human output. |
| `--repo <path>` | Operate on this repository instead of the working directory. |
| `--config <path>` | Operate on the repository containing this configuration file. |

An option a command does not support is a usage error rather than a silently
ignored argument.

## Lifecycle commands

### `init`

Brings the repository into a valid installed state. Safe to repeat: a second run
with nothing missing makes no changes at all, byte for byte.

| Option | Meaning |
| --- | --- |
| `--dry-run` | Calculate and report the plan; change nothing. |
| `--check` | Change nothing; exit `3` if changes would be needed. |
| `--json` | Emit the plan or the result as JSON. |
| `--verbose` | List unchanged paths and the reason for each. |

See [Installation](./installation.md) for exactly what `init` creates, what it
modifies and what it refuses to touch.

### `status`

Read-only. Reports the tool, the CLI version, the installed version, the
configuration version, installation state, artifact state, npm integration
state, verification state and whether an upgrade is available.

```
research-publisher (@echelon-foundry/research-publisher)

  CLI version:           0.1.0
  Installed version:     0.1.0
  Configuration:         valid (version 2 of 2)
  Installation:          installed (0.1.0, configuration 2)
  Required artifacts:    valid
  Integration:           valid
  Verification:          passed
  Upgrade:               up to date
```

`status` always exits `0` when it can read the repository; use `verify` for a
pass/fail gate.

### `verify`

Read-only. Validates that the capability is correctly installed: the manifest
parses and uses a supported schema, required files exist, the configuration
module has a default export, the configuration version matches this release, the
npm scripts are registered and the package is declared as a dependency.

| Option | Meaning |
| --- | --- |
| `--strict` | Treat warnings as failures. |
| `--json` | Emit the verification report as JSON. |
| `--verbose` | Include passing checks in human output. |

Exits `0` when valid and `3` when not.

`verify` checks the *installation*. Use `validate` to check the research corpus
itself (front matter, identifiers and links).

Strict mode only promotes warnings; it never reports a problem that the normal
mode would have hidden entirely. Today the difference is:

| Check | Normal | Strict |
| --- | --- | --- |
| The package is not a declared dependency | warning | failure |
| A script from the current configuration version is missing | warning | failure |
| The configuration module has no `export default` | warning | failure |

A shared file the repository has edited is reported, not penalised, in either
mode.

### `upgrade`

Moves an existing installation to the version of this CLI by running each
required migration in order. It refuses to create a new installation, so running
it in the wrong directory does nothing.

| Option | Meaning |
| --- | --- |
| `--dry-run` | Calculate and report the plan; change nothing. |
| `--check` | Change nothing; exit `3` if an upgrade is pending. |
| `--json` | Emit the plan or the result as JSON. |
| `--verbose` | List unchanged paths and conflicts in detail. |

See [Upgrading](./upgrading.md) for the migration model and its guarantees.

### `doctor`

Read-only. Explains what is wrong and how to fix it, separating errors from
warnings and information.

| Option | Meaning |
| --- | --- |
| `--strict` | Exit non-zero on warnings as well as errors. |
| `--json` | Emit diagnostics as JSON. |
| `--verbose` | Include informational diagnostics in human output. |

Exits `0` when there are no errors and `6` when there are.

### `install-prompt` (legacy compatibility)

Installs only the document-marking prompt. `init` does this and everything else.
Retained so repositories that scripted the older command keep working.

## Publishing commands

These run the JavaScript publishing engine and are unchanged by the lifecycle
interface.

| Command | Purpose |
| --- | --- |
| `build` | Render the corpus as a static site and build the search index. |
| `dev` | Serve the site locally with live reload. |
| `validate` | Validate the corpus: front matter, identifiers and links. |
| `inventory` | Report the documents the content globs discover. |
| `check-links` | Build and fail when a document link cannot be resolved. |
| `clean` | Remove generated output and the engine cache. |
| `preview` | Describe the built output. |
| `migrate` | Report metadata migration candidates. |

They take `--config <path>`, defaulting to `./research-publisher.config.mjs`.

## Exit codes

These are a public contract. They are pinned by a test.

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | Internal failure, including a run that stopped part-way. |
| `2` | Invalid arguments. |
| `3` | Verification failed, or `--check` found pending changes. |
| `4` | The installation exists but this release cannot work with it. |
| `5` | A migration precondition failed; nothing was changed. |
| `6` | A prerequisite or environment requirement is not met. |
| `7` | No packaged executable matches this platform. |

## Machine-readable output

With `--json`, stdout contains exactly one JSON document and nothing else.
Human-facing text moves to stderr. Every document carries a versioned `schema`
field, so a consumer can detect a schema change instead of guessing.

| Command | Schema |
| --- | --- |
| `status --json` | `research-publisher.status/1` |
| `verify --json` | `research-publisher.verify/1` |
| `doctor --json` | `research-publisher.doctor/1` |
| `init --dry-run --json`, `upgrade --dry-run --json`, `--check --json` | `research-publisher.plan/1` |
| `init --json`, `upgrade --json` | `research-publisher.result/1` |
| any usage or internal error with `--json` | `research-publisher.error/1` |

Every document also carries `tool`, `package`, `cliVersion` and `exitCode`.

### `research-publisher.status/1`

```json
{
  "schema": "research-publisher.status/1",
  "tool": "research-publisher",
  "package": "@echelon-foundry/research-publisher",
  "cliVersion": "0.1.0",
  "repositoryRoot": "/repo",
  "state": "installed",
  "stateDescription": "installed (0.1.0, configuration 2)",
  "installedVersion": "0.1.0",
  "configurationVersion": 2,
  "currentConfigurationVersion": 2,
  "configuration": "valid",
  "artifacts": "valid",
  "integration": "valid",
  "verification": { "passed": true, "strict": false, "checks": [] },
  "upgradeAvailable": null,
  "manifestPath": ".echelon/research-publisher.json",
  "exitCode": 0
}
```

`state` is one of `not-installed`, `installed`, `upgrade-required` or `invalid`.

### `research-publisher.verify/1`

`checks` is an array of `{ id, title, status, detail, path, remediation }`, where
`status` is `pass`, `warn`, `fail` or `skipped`.

### `research-publisher.doctor/1`

`diagnostics` is an array of `{ code, severity, title, detail, path, remediation }`,
where `severity` is `error`, `warning` or `information`. `counts` totals each
severity, and `healthy` reflects the requested strictness.

### `research-publisher.plan/1`

```json
{
  "schema": "research-publisher.plan/1",
  "dryRun": true,
  "executable": true,
  "operation": "init",
  "fromState": "not-installed",
  "target": { "toolVersion": "0.1.0", "configurationVersion": 2 },
  "migrations": [],
  "changeCount": 11,
  "changes": [
    {
      "kind": "create-file",
      "target": "research-publisher.config.mjs",
      "description": "Create research-publisher.config.mjs (user-owned)",
      "reason": "No publishing configuration exists yet.",
      "ownership": "user-owned"
    }
  ],
  "skipped": [],
  "conflicts": [],
  "blockers": []
}
```

`kind` is one of `create-directory`, `create-file`, `update-managed-file`,
`add-package-script`, `write-manifest` or `run-migration`. A non-empty `blockers`
array means nothing will be executed.

### `research-publisher.result/1`

The plan fields plus `succeeded`, `changed`, `failures` and `applied`, where each
applied entry has an `outcome` of `applied`, `not-attempted` or `failed`.

## Using the CLI from CI

```bash
npx @echelon-foundry/research-publisher verify --strict
```

Exit `0` means the installation is valid under strict rules; `3` means it is not.
To fail a build when an upgrade is pending without changing anything:

```bash
npx @echelon-foundry/research-publisher upgrade --check
```

To capture a report for a build artifact:

```bash
npx @echelon-foundry/research-publisher status --json > research-publisher-status.json
```

## Using the CLI from an agent

Lifecycle commands are non-interactive: none of them prompt, so none of them can
hang waiting for input. Nothing is confirmed implicitly either — a command that
would change something either changes it or, with `--dry-run` or `--check`,
reports what it would change and stops.

A safe agent loop is:

```bash
npx @echelon-foundry/research-publisher status --json          # read the state
npx @echelon-foundry/research-publisher init --dry-run --json  # inspect the plan
npx @echelon-foundry/research-publisher init --json            # apply it
npx @echelon-foundry/research-publisher verify --json          # confirm the result
```

Read `exitCode` from the JSON or the process exit code; they agree. When a
command refuses to act, `blockers` says why and each blocker carries a
`remediation` string.
