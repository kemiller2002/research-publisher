namespace ResearchPublisher.Lifecycle.Core

/// Inputs available to a migration while it plans its changes.
type MigrationContext =
    { Inspection: RepositoryInspection
      /// The marking prompt as shipped by this release, or why it is unavailable.
      PromptTemplate: Result<string, string>
      /// Script names already defined in package.json or already planned by an
      /// earlier migration in the same chain.
      KnownScripts: Set<string> }

/// What one version transition contributes to a plan.
type MigrationResult =
    { Steps: PlanStep list
      Skipped: SkippedStep list
      Conflicts: PlanConflict list
      Blockers: Problem list }

module MigrationResult =

    let empty =
        { Steps = []
          Skipped = []
          Conflicts = []
          Blockers = [] }

    let combine left right =
        { Steps = left.Steps @ right.Steps
          Skipped = left.Skipped @ right.Skipped
          Conflicts = left.Conflicts @ right.Conflicts
          Blockers = left.Blockers @ right.Blockers }

/// One supported version transition.
///
/// Transitions are sequential: reaching configuration version 2 from version 0
/// runs 0->1 and then 1->2. There are no N-to-N shortcuts, so each step only has
/// to understand the shape immediately before it.
type Migration =
    { Id: MigrationId
      Title: string
      Summary: string
      /// Reasons this transition must not run. A non-empty result stops the whole
      /// chain before anything is written.
      Preconditions: MigrationContext -> Problem list
      Plan: MigrationContext -> MigrationResult }

module Migrations =

    let private requirePackageJson (context: MigrationContext) =
        if context.Inspection.HasPackageJson && context.Inspection.Consumer.IsSome then
            []
        else
            [ Problem.create
                "package-json-required"
                Error
                "package.json is required."
                "Lifecycle changes add npm scripts, so the repository must be an npm project."
              |> Problem.withPath "package.json"
              |> Problem.withRemediation "Run `npm init -y` first, then re-run the command." ]

    let private addMissingScripts (scripts: ManagedScript list) (context: MigrationContext) =
        let missing =
            scripts |> List.filter (fun script -> not (context.KnownScripts.Contains script.Name))

        let skipped =
            scripts
            |> List.filter (fun script -> context.KnownScripts.Contains script.Name)
            |> List.map (fun script ->
                { Target = sprintf "package.json#scripts.%s" script.Name
                  Reason = "The repository already defines this script; the existing command is preserved." })

        { MigrationResult.empty with
            Steps =
                [ for script in missing ->
                    { Change = AddPackageScript(script.Name, script.Command)
                      Reason = "Required by the installed configuration version." } ]
            Skipped = skipped }

    /// Refresh a shared, tool-supplied file, but only when the repository has not
    /// edited it. A local edit is reported as a conflict and the file is left alone.
    let private refreshMarkingPrompt (context: MigrationContext) =
        match Inspection.tryObservation Desired.MarkingPromptArtifactId context.Inspection with
        | None -> MigrationResult.empty
        | Some observation ->
            match context.PromptTemplate with
            | Result.Error message ->
                { MigrationResult.empty with
                    Blockers =
                        [ Problem.create
                            "prompt-template-unavailable"
                            Error
                            "The packaged document-marking prompt could not be read."
                            message
                          |> Problem.withPath Desired.MarkingPromptPath
                          |> Problem.withRemediation "Reinstall the package so its runtime assets are present." ] }
            | Ok template ->
                let templateHash = Hash.ofText template

                if not observation.Exists then
                    // Creating missing files is `init`'s job; `upgrade` refuses to run
                    // against an installation that is missing required files at all.
                    { MigrationResult.empty with
                        Skipped =
                            [ { Target = Desired.MarkingPromptPath
                                Reason = "Missing; it is restored by `init` rather than by a migration." } ] }
                elif observation.Hash = Some templateHash then
                    { MigrationResult.empty with
                        Skipped =
                            [ { Target = Desired.MarkingPromptPath
                                Reason = "Already matches the prompt shipped with this release." } ] }
                elif ArtifactObservation.isLocallyModified observation then
                    { MigrationResult.empty with
                        Conflicts =
                            [ { Target = Desired.MarkingPromptPath
                                Ownership = Shared
                                Detail =
                                    "The repository edited this shared file after the tool wrote it, so the newer packaged version was not applied."
                                Resolution =
                                    "Keep the local version, or delete the file and re-run `init` to take the packaged version." } ] }
                elif observation.RecordedHash.IsNone then
                    // Present but never recorded: an installation that predates
                    // manifests. Adopt the local content rather than replacing it.
                    { MigrationResult.empty with
                        Skipped =
                            [ { Target = Desired.MarkingPromptPath
                                Reason =
                                    "Adopted the existing file; its content was not recorded by a previous install." } ] }
                else
                    { MigrationResult.empty with
                        Steps =
                            [ { Change = UpdateManagedFile(Desired.MarkingPromptPath, Shared, template)
                                Reason = "Unmodified shared file refreshed to the version shipped with this release." } ] }

    /// Adopt an installation created before installation manifests existed.
    let private adoptUnmanagedInstallation =
        { Id = { FromVersion = 0; ToVersion = 1 }
          Title = "Adopt an unmanaged installation"
          Summary =
            "Records an existing installation in .echelon/research-publisher.json without changing any content it already has."
          Preconditions =
            fun context ->
                let configPresent =
                    Inspection.tryObservation Desired.ConfigArtifactId context.Inspection
                    |> Option.map (fun observation -> observation.Exists)
                    |> Option.defaultValue false

                if configPresent then
                    requirePackageJson context
                else
                    [ Problem.create
                        "configuration-missing"
                        Error
                        "There is no installation to adopt."
                        (sprintf "%s was not found." Desired.ConfigPath)
                      |> Problem.withPath Desired.ConfigPath
                      |> Problem.withRemediation (sprintf "Run `npx %s init` instead." Identity.PackageName) ]
          Plan =
            fun context ->
                let scripts = (Desired.forConfigurationVersion 1).Scripts
                addMissingScripts scripts context }

    /// Add the standard Echelon lifecycle commands to the repository.
    let private addLifecycleInterface =
        { Id = { FromVersion = 1; ToVersion = 2 }
          Title = "Add the Echelon lifecycle interface"
          Summary =
            "Adds research:status, research:verify and research:doctor scripts and refreshes the shared document-marking prompt when it is unmodified."
          Preconditions = requirePackageJson
          Plan =
            fun context ->
                let scripts = (Desired.forConfigurationVersion 2).Scripts
                MigrationResult.combine (addMissingScripts scripts context) (refreshMarkingPrompt context) }

    /// Every supported transition, lowest first.
    let all = [ adoptUnmanagedInstallation; addLifecycleInterface ]

    /// The ordered transitions needed to move an installation to the current version.
    let pathFrom (fromVersion: int) =
        all
        |> List.filter (fun migration ->
            migration.Id.FromVersion >= fromVersion
            && migration.Id.ToVersion <= Identity.CurrentConfigurationVersion)
        |> List.sortBy (fun migration -> migration.Id.FromVersion)

    /// Script names contributed by a migration, used to thread `KnownScripts`
    /// through a chain without executing anything.
    let scriptsAddedBy (result: MigrationResult) =
        result.Steps
        |> List.choose (fun step ->
            match step.Change with
            | AddPackageScript (name, _) -> Some name
            | _ -> None)
