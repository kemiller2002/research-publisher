namespace ResearchPublisher.Lifecycle.Core

open System.IO

/// Calculates the transition from the observed state to the desired state.
///
/// Planning is pure: it reads an inspection and produces values. Nothing here
/// writes to the repository, which is what makes `--dry-run` exact rather than
/// an approximation.
module Planning =

    let private configPresent inspection =
        Inspection.tryObservation Desired.ConfigArtifactId inspection
        |> Option.map (fun observation -> observation.Exists)
        |> Option.defaultValue false

    /// Configuration version the repository is currently at.
    /// 0 means "installed before manifests existed".
    let currentConfigurationVersion inspection =
        match inspection.Manifest with
        | Some manifest -> Some manifest.ConfigurationVersion
        | None -> if configPresent inspection then Some 0 else None

    let private plannedContentFor (path: string) (steps: PlanStep list) =
        steps
        |> List.tryPick (fun step ->
            match step.Change with
            | CreateFile (target, _, contents) when target = path -> Some contents
            | UpdateManagedFile (target, _, contents) when target = path -> Some contents
            | _ -> None)

    /// Hashes are recorded only for paths the tool writes and may later want to
    /// refresh. A user-owned file is never rewritten, so recording its hash would
    /// add churn without adding safety.
    let private recordsHash (artifact: ManagedArtifact) =
        artifact.Id <> Desired.ManifestArtifactId
        && (match artifact.Ownership with
            | Shared
            | ToolOwned -> true
            | UserOwned
            | Generated -> false)

    let private prospectiveManifest
        (cliVersion: string)
        (configurationVersion: int)
        (desired: DesiredState)
        (inspection: RepositoryInspection)
        (steps: PlanStep list)
        =
        let artifacts =
            Desired.allKnownArtifacts
            |> List.map (fun artifact ->
                let hash =
                    if not (recordsHash artifact) then
                        None
                    else
                        match plannedContentFor artifact.Path steps with
                        | Some contents -> Some(Hash.ofText contents)
                        | None ->
                            inspection.Artifacts
                            |> List.tryFind (fun observation -> observation.Artifact.Id = artifact.Id)
                            |> Option.bind (fun observation ->
                                // A shared file the repository edited keeps the hash the
                                // tool last wrote. Adopting the local content instead would
                                // erase the evidence of the edit and let a later release
                                // overwrite it silently.
                                if ArtifactObservation.isLocallyModified observation then
                                    observation.RecordedHash
                                else
                                    observation.Hash)

                { Id = artifact.Id
                  Path = artifact.Path
                  Ownership = artifact.Ownership
                  Hash = hash })

        let existingScripts =
            inspection.Consumer
            |> Option.map (fun consumer -> consumer.Scripts)
            |> Option.defaultValue Map.empty

        let scripts =
            desired.Scripts
            |> List.map (fun script ->
                // Record the command the repository will actually run: a script the
                // repository already defined keeps its own command.
                let command =
                    match Map.tryFind script.Name existingScripts with
                    | Some existing -> existing
                    | None -> script.Command

                { Name = script.Name; Command = command })

        { Schema = Identity.ManifestSchema
          Tool = Identity.ToolName
          Package = Identity.PackageName
          InstalledVersion = cliVersion
          ConfigurationVersion = configurationVersion
          ManagedArtifacts = artifacts
          ManagedScripts = scripts }

    let private requirePackageJson inspection =
        match inspection.PackageProblem with
        | Some problem -> [ problem ]
        | None ->
            if inspection.HasPackageJson then
                []
            else
                [ Problem.create
                    "package-json-required"
                    Error
                    "package.json is required."
                    (sprintf
                        "%s installs npm scripts, so the target repository must be an npm project."
                        Identity.PackageName)
                  |> Problem.withPath "package.json"
                  |> Problem.withRemediation
                      (sprintf "Run `npm init -y` first, then run `npx %s init`." Identity.PackageName) ]

    let private tooNewBlocker inspection =
        match inspection.Manifest with
        | Some manifest when manifest.ConfigurationVersion > Identity.CurrentConfigurationVersion ->
            [ Problem.create
                "configuration-version-too-new"
                Error
                "The installation was created by a newer release."
                (sprintf
                    "The repository declares configuration version %d; this CLI supports up to %d."
                    manifest.ConfigurationVersion
                    Identity.CurrentConfigurationVersion)
              |> Problem.withPath Identity.ManifestPath
              |> Problem.withRemediation (sprintf "Install a newer %s release." Identity.PackageName) ]
        | _ -> []

    /// Files a fresh or damaged installation is missing. Only paths the tool owns
    /// or shares are created; a user-owned file is created once and then left alone.
    let private repairSteps (promptTemplate: Result<string, string>) inspection =
        let consumer =
            inspection.Consumer
            |> Option.defaultValue
                { Name = Path.GetFileName inspection.Root
                  RepositoryUrl = ""
                  Scripts = Map.empty
                  DeclaresPublisherDependency = false }

        let mutable blockers = []
        let mutable steps = []
        let mutable skipped = []

        for observation in inspection.Artifacts do
            let artifact = observation.Artifact

            if artifact.Id = Desired.ManifestArtifactId then
                ()
            elif observation.Exists then
                skipped <-
                    skipped
                    @ [ { Target = artifact.Path
                          Reason =
                            match artifact.Ownership with
                            | UserOwned -> "Already present and owned by the repository; left unchanged."
                            | _ -> "Already present." } ]
            elif artifact.Id = Desired.ConfigArtifactId then
                steps <-
                    steps
                    @ [ { Change = CreateFile(artifact.Path, artifact.Ownership, Desired.configTemplate consumer)
                          Reason = "No publishing configuration exists yet." } ]
            elif artifact.Id = Desired.MarkingPromptArtifactId then
                match promptTemplate with
                | Ok template ->
                    steps <-
                        steps
                        @ [ { Change = CreateFile(artifact.Path, artifact.Ownership, template)
                              Reason = "The shared document-marking prompt is missing." } ]
                | Result.Error message ->
                    blockers <-
                        blockers
                        @ [ Problem.create
                              "prompt-template-unavailable"
                              Error
                              "The packaged document-marking prompt could not be read."
                              message
                            |> Problem.withPath artifact.Path
                            |> Problem.withRemediation "Reinstall the package so its runtime assets are present." ]

        steps, skipped, blockers

    let private runMigrationChain (promptTemplate: Result<string, string>) inspection fromVersion =
        let initialScripts =
            inspection.Consumer
            |> Option.map (fun consumer -> consumer.Scripts |> Map.toList |> List.map fst |> Set.ofList)
            |> Option.defaultValue Set.empty

        let migrations = Migrations.pathFrom fromVersion

        let folder (accumulated: MigrationResult, known: Set<string>, applied: MigrationId list) migration =
            let context =
                { Inspection = inspection
                  PromptTemplate = promptTemplate
                  KnownScripts = known }

            let preconditionFailures = migration.Preconditions context

            if not (List.isEmpty preconditionFailures) then
                // Stop immediately: later transitions assume this one ran.
                let stopped =
                    { accumulated with
                        Blockers = accumulated.Blockers @ preconditionFailures }

                stopped, known, applied
            elif not (List.isEmpty accumulated.Blockers) then
                accumulated, known, applied
            else
                let result = migration.Plan context

                let announced =
                    { result with
                        Steps =
                            { Change = RunMigration(migration.Id, migration.Title)
                              Reason = migration.Summary }
                            :: result.Steps }

                let known =
                    Migrations.scriptsAddedBy result
                    |> List.fold (fun (set: Set<string>) name -> set.Add name) known

                MigrationResult.combine accumulated announced, known, applied @ [ migration.Id ]

        let result, known, applied =
            migrations |> List.fold folder (MigrationResult.empty, initialScripts, [])

        result, known, applied

    /// Bring package.json up to the target script set. Scripts the repository
    /// already defines are reported as unchanged rather than silently ignored.
    let private convergeScripts (desired: DesiredState) (known: Set<string>) =
        let steps =
            desired.Scripts
            |> List.filter (fun script -> not (known.Contains script.Name))
            |> List.map (fun script ->
                { Change = AddPackageScript(script.Name, script.Command)
                  Reason = "Required by the current configuration version." })

        let skipped =
            desired.Scripts
            |> List.filter (fun script -> known.Contains script.Name)
            |> List.map (fun script ->
                { Target = sprintf "package.json#scripts.%s" script.Name
                  Reason = "The repository already defines this script; the existing command is preserved." })

        steps, skipped

    let private buildPlan
        (operation: OperationKind)
        (cliVersion: string)
        (promptTemplate: Result<string, string>)
        (inspection: RepositoryInspection)
        (fromState: InstallationState)
        (includeRepairs: bool)
        (extraBlockers: Problem list)
        =
        let desired = Desired.current
        let fromVersion = currentConfigurationVersion inspection |> Option.defaultValue Identity.CurrentConfigurationVersion

        let repairSteps, repairSkipped, repairBlockers =
            if includeRepairs then repairSteps promptTemplate inspection else ([], [], [])

        let migrationResult, knownAfterMigrations, appliedMigrations =
            runMigrationChain promptTemplate inspection fromVersion

        let knownScripts =
            repairSteps
            |> List.fold
                (fun (set: Set<string>) step ->
                    match step.Change with
                    | AddPackageScript (name, _) -> set.Add name
                    | _ -> set)
                knownAfterMigrations

        let convergenceSteps, convergenceSkipped = convergeScripts desired knownScripts

        let directorySteps =
            if Directory.Exists(Path.Combine(inspection.Root, Identity.EchelonDirectory)) then
                []
            else
                [ { Change = CreateDirectory Identity.EchelonDirectory
                    Reason = "Shared Echelon Foundry tooling directory." } ]

        let contentSteps = directorySteps @ repairSteps @ migrationResult.Steps @ convergenceSteps

        let manifest =
            prospectiveManifest cliVersion Identity.CurrentConfigurationVersion desired inspection contentSteps

        let manifestChanged =
            match inspection.Manifest with
            | Some existing -> Manifest.render existing <> Manifest.render manifest
            | None -> true

        let manifestSteps =
            if manifestChanged then
                [ { Change = WriteManifest manifest
                    Reason =
                        if inspection.ManifestExists then
                            "Record the resulting installation state."
                        else
                            "Create the installation record." } ]
            else
                []

        let manifestSkipped =
            if manifestChanged then
                []
            else
                [ { Target = Identity.ManifestPath
                    Reason = "Already records the current state." } ]

        { Operation = operation
          RepositoryRoot = inspection.Root
          FromState = fromState
          Target =
            { ToolVersion = cliVersion
              ConfigurationVersion = Identity.CurrentConfigurationVersion }
          Migrations = appliedMigrations
          Steps = contentSteps @ manifestSteps
          Skipped = repairSkipped @ migrationResult.Skipped @ convergenceSkipped @ manifestSkipped
          Conflicts = migrationResult.Conflicts
          Blockers =
            extraBlockers
            @ tooNewBlocker inspection
            @ requirePackageJson inspection
            @ repairBlockers
            @ migrationResult.Blockers }

    /// The plan `init` would execute: create what is missing, migrate what is old,
    /// and leave everything the repository owns alone.
    let createInitializationPlan (cliVersion: string) (promptTemplate: Result<string, string>) inspection =
        let state = Inspection.getInstallationState cliVersion inspection
        buildPlan Initialize cliVersion promptTemplate inspection state true []

    /// The plan `upgrade` would execute. Unlike `init` it refuses to create a new
    /// installation, so an accidental `upgrade` in the wrong directory does nothing.
    let planUpgrade (cliVersion: string) (promptTemplate: Result<string, string>) inspection =
        let state = Inspection.getInstallationState cliVersion inspection

        let blockers =
            match state with
            | NotInstalled ->
                [ Problem.create
                    "not-installed"
                    Error
                    "There is nothing to upgrade."
                    (sprintf "%s is not installed in this repository." Identity.PackageName)
                  |> Problem.withRemediation (sprintf "Run `npx %s init` first." Identity.PackageName) ]
            | Invalid problems ->
                problems
                @ [ Problem.create
                      "installation-invalid"
                      Error
                      "The installation must be repaired before it can be upgraded."
                      "Upgrade only moves a valid installation between versions."
                    |> Problem.withRemediation (sprintf "Run `npx %s init` to repair it." Identity.PackageName) ]
            | Installed _
            | UpgradeRequired _ -> []

        buildPlan Upgrade cliVersion promptTemplate inspection state false blockers

    /// A deliberately narrow plan used by the legacy `install-prompt` command:
    /// it installs the shared document-marking prompt and nothing else.
    let createPromptOnlyPlan (cliVersion: string) (promptTemplate: Result<string, string>) inspection =
        let state = Inspection.getInstallationState cliVersion inspection

        let steps, skipped, blockers =
            match Inspection.tryObservation Desired.MarkingPromptArtifactId inspection with
            | Some observation when observation.Exists ->
                [],
                [ { Target = Desired.MarkingPromptPath
                    Reason = "Already present; left unchanged." } ],
                []
            | _ ->
                match promptTemplate with
                | Ok template ->
                    [ { Change = CreateFile(Desired.MarkingPromptPath, Shared, template)
                        Reason = "Install the shared document-marking prompt." } ],
                    [],
                    []
                | Result.Error message ->
                    [],
                    [],
                    [ Problem.create
                        "prompt-template-unavailable"
                        Error
                        "The packaged document-marking prompt could not be read."
                        message
                      |> Problem.withPath Desired.MarkingPromptPath
                      |> Problem.withRemediation "Reinstall the package so its runtime assets are present." ]

        { Operation = Initialize
          RepositoryRoot = inspection.Root
          FromState = state
          Target =
            { ToolVersion = cliVersion
              ConfigurationVersion = Identity.CurrentConfigurationVersion }
          Migrations = []
          Steps = steps
          Skipped = skipped
          Conflicts = []
          Blockers = blockers }
