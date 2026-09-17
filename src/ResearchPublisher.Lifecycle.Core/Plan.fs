namespace ResearchPublisher.Lifecycle.Core

/// Which migration step a change belongs to.
type MigrationId =
    { FromVersion: int
      ToVersion: int }

module MigrationId =

    let describe id = sprintf "%d->%d" id.FromVersion id.ToVersion

/// A change the tool intends to make. Planned changes are values: calculating them
/// touches nothing, so `--dry-run` and the real run share one code path.
type PlannedChange =
    | CreateDirectory of path: string
    | CreateFile of path: string * ownership: Ownership * contents: string
    | UpdateManagedFile of path: string * ownership: Ownership * contents: string
    | AddPackageScript of name: string * command: string
    | WriteManifest of manifest: Manifest
    | RunMigration of id: MigrationId * title: string

module PlannedChange =

    let kind change =
        match change with
        | CreateDirectory _ -> "create-directory"
        | CreateFile _ -> "create-file"
        | UpdateManagedFile _ -> "update-managed-file"
        | AddPackageScript _ -> "add-package-script"
        | WriteManifest _ -> "write-manifest"
        | RunMigration _ -> "run-migration"

    let target change =
        match change with
        | CreateDirectory path -> path
        | CreateFile (path, _, _) -> path
        | UpdateManagedFile (path, _, _) -> path
        | AddPackageScript (name, _) -> sprintf "package.json#scripts.%s" name
        | WriteManifest _ -> Identity.ManifestPath
        | RunMigration (id, _) -> MigrationId.describe id

    let describe change =
        match change with
        | CreateDirectory path -> sprintf "Create directory %s" path
        | CreateFile (path, ownership, _) -> sprintf "Create %s (%s)" path (Ownership.toWire ownership)
        | UpdateManagedFile (path, ownership, _) -> sprintf "Update %s (%s)" path (Ownership.toWire ownership)
        | AddPackageScript (name, command) -> sprintf "Add package script %s -> %s" name command
        | WriteManifest manifest ->
            sprintf
                "Write %s (configuration %d)"
                Identity.ManifestPath
                manifest.ConfigurationVersion
        | RunMigration (id, title) -> sprintf "Run migration %s: %s" (MigrationId.describe id) title

type PlanStep =
    { Change: PlannedChange
      Reason: string }

/// Something the tool deliberately did not do, and why. Reporting these is what
/// makes "no changes needed" trustworthy.
type SkippedStep =
    { Target: string
      Reason: string }

/// A managed path the tool wanted to change but must not, because the repository
/// changed it first.
type PlanConflict =
    { Target: string
      Ownership: Ownership
      Detail: string
      Resolution: string }

type OperationKind =
    | Initialize
    | Upgrade

module OperationKind =

    let toWire kind =
        match kind with
        | Initialize -> "init"
        | Upgrade -> "upgrade"

/// The complete, validated transition from the current state to the desired one.
type Plan =
    { Operation: OperationKind
      RepositoryRoot: string
      FromState: InstallationState
      Target: TargetVersion
      /// Migrations that will run, in order.
      Migrations: MigrationId list
      Steps: PlanStep list
      Skipped: SkippedStep list
      Conflicts: PlanConflict list
      /// Non-empty means nothing may be executed.
      Blockers: Problem list }

module Plan =

    let isExecutable plan = List.isEmpty plan.Blockers

    let hasChanges plan = not (List.isEmpty plan.Steps)
