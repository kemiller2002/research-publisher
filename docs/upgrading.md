# Upgrading

```bash
npm install -D @echelon-foundry/research-publisher@latest
npx @echelon-foundry/research-publisher upgrade --dry-run
npx @echelon-foundry/research-publisher upgrade
```

## The migration model

An installation has a **configuration version**: the shape of the repository, not
the version of the package. Moving between shapes is done by sequential
migrations, never by deleting old files and copying new ones.

| Version | Shape |
| --- | --- |
| 0 | Installed before `.echelon/` manifests existed. |
| 1 | Configuration, shared prompt and the four `research:*` engine scripts, recorded in a manifest. |
| 2 | Adds the `research:status`, `research:verify` and `research:doctor` scripts, and keeps the shared prompt in step with the release. |

Each migration declares its source version, its destination version, its
preconditions and its changes. Reaching version 2 from version 0 runs `0 -> 1`
and then `1 -> 2`; there are no N-to-N shortcuts, so each step only has to
understand the shape immediately before it.

If a precondition fails, the chain stops **before anything is written** and the
command exits `5`. A blocked upgrade never leaves a repository half-migrated.

## Supported upgrade paths

All of these are covered by automated tests against fixtures of each historical
shape:

| From | Result |
| --- | --- |
| Not installed | Refused, exit `4`. Run `init` instead. |
| Pre-manifest installation (version 0) | Adopted, then migrated to the current version. |
| Version 1 | Migrated to the current version. |
| Current version, older package release | Manifest re-recorded with the new version; no other change. |
| Current version, same release | No changes. |
| Configuration version newer than the CLI | Refused, exit `4`. Install a newer release. |
| Damaged installation (missing required file, unreadable manifest) | Refused, exit `4`, with a pointer to `init`, which repairs. |

## What upgrade guarantees

- **User-owned files are never touched.** `research-publisher.config.mjs` and any
  script the repository already defines survive every upgrade unchanged.
- **Shared files are only refreshed when they are unmodified.** The manifest
  records the hash the tool last wrote. If the current content matches, the file
  is refreshed to the packaged version. If it does not, the upgrade reports a
  conflict and leaves your version in place.
- **A conflict is remembered.** When a shared file is in conflict, the manifest
  keeps the hash the tool last wrote rather than adopting the local content, so a
  future release cannot silently overwrite the edit.
- **Additive script changes only.** Upgrade adds scripts; it never rewrites or
  removes one.
- **Nothing is written before the whole plan is validated.** Plan, precondition
  check and conflict detection all happen first.
- **The manifest is written last.** If a run is interrupted, the repository is
  left in a state `init` can finish rather than in an inconsistent one recorded
  as complete.

These are the guarantees the tests prove. Nothing stronger is promised: there is
no transactional rollback of a partially applied plan. If a write fails part-way,
the command exits `1`, reports exactly which changes were applied, which failed
and which were not attempted, and re-running `init` converges the repository.

## Resolving a shared-file conflict

```
Conflicts (nothing was overwritten):
  prompts/research-publisher-mark-documents.md
    The repository edited this shared file after the tool wrote it, so the newer
    packaged version was not applied.
    Keep the local version, or delete the file and re-run `init` to take the
    packaged version.
```

Either outcome is supported. Keeping your version is not an error, and `verify`
does not penalise it in any mode; `doctor` reports it as information so you know
the file may drift from the packaged one.

## Dry run

```bash
npx @echelon-foundry/research-publisher upgrade --dry-run --json
```

The plan is calculated by the same code that executes it, so a dry run is exact
rather than an approximation. It lists the migrations that will run, every change
with its reason and ownership, everything deliberately left unchanged, every
conflict and every blocker. It writes nothing.

## Checking without changing

```bash
npx @echelon-foundry/research-publisher upgrade --check
```

Exits `3` when an upgrade is pending and `0` when it is not. Useful as a CI gate.

## Compatibility policy

- The command contract (`init`, `status`, `verify`, `upgrade`, `doctor`, the
  flags, the exit codes and the JSON schemas) is treated with the same discipline
  as a code API.
- JSON schemas are versioned in the document itself. A breaking change gets a new
  schema identifier rather than a silent change of shape.
- The installation manifest schema is versioned. A manifest from a newer schema
  is refused with a clear message rather than misinterpreted.
- Every configuration version that has ever shipped keeps a migration path to the
  current one. Versions are not dropped.
- Commands that existed before this release still work. `install-prompt` remains
  as a legacy compatibility command, and the older JavaScript entry points
  (`src/cli/index.mjs`, `initializeProject`, `installMarkingPrompt`) still work,
  delegating to the same F# implementation.

## If something goes wrong

```bash
npx @echelon-foundry/research-publisher doctor
```

`doctor` explains what is wrong and how to fix it. For a damaged installation,
`init` repairs what is missing without touching what you own.
