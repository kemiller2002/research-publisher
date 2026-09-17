namespace ResearchPublisher.Lifecycle.Core

/// Read-only summary of an installation, shaped for both humans and machines.
type StatusSummary =
    { Tool: string
      Package: string
      CliVersion: string
      RepositoryRoot: string
      State: InstallationState
      InstalledVersion: string option
      ConfigurationVersion: int option
      /// "valid", "missing" or "unreadable".
      ConfigurationStatus: string
      /// "valid" or a description of what is missing.
      ArtifactStatus: string
      /// npm integration: scripts and declared dependency.
      IntegrationStatus: string
      Verification: VerificationReport
      /// Present when `upgrade` has work to do.
      UpgradeAvailable: TargetVersion option }

/// The lifecycle service.
///
/// Every operation is available here as a plain function over values, so ROS,
/// integration assemblies, tests and future hosts can use it without constructing
/// a command line. The CLI in `ResearchPublisher.Lifecycle.Cli` is one adapter
/// over this module, not the application.
module Api =

    let cliVersion () = PackageRoot.version ()

    let inspectRepository (repositoryRoot: string) = Inspection.inspectRepository repositoryRoot

    let getInstallationState (cliVersion: string) (inspection: RepositoryInspection) =
        Inspection.getInstallationState cliVersion inspection

    let private promptTemplate () = Desired.tryMarkingPromptTemplate ()

    let createInitializationPlan (cliVersion: string) (inspection: RepositoryInspection) =
        Planning.createInitializationPlan cliVersion (promptTemplate ()) inspection

    let planUpgrade (cliVersion: string) (inspection: RepositoryInspection) =
        Planning.planUpgrade cliVersion (promptTemplate ()) inspection

    /// Legacy compatibility: install only the shared document-marking prompt.
    let createPromptOnlyPlan (cliVersion: string) (inspection: RepositoryInspection) =
        Planning.createPromptOnlyPlan cliVersion (promptTemplate ()) inspection

    /// Execute a plan. Separated from planning so a caller can inspect, validate
    /// and approve a transition before anything is written.
    let apply (plan: Plan) = Execution.execute plan

    let initialize (cliVersion: string) (repositoryRoot: string) =
        repositoryRoot |> inspectRepository |> createInitializationPlan cliVersion |> apply

    let performUpgrade (cliVersion: string) (repositoryRoot: string) =
        repositoryRoot |> inspectRepository |> planUpgrade cliVersion |> apply

    let verify (cliVersion: string) (strict: bool) (inspection: RepositoryInspection) =
        Verification.verify cliVersion strict inspection

    let diagnose (cliVersion: string) (strict: bool) (host: HostEnvironment) (inspection: RepositoryInspection) =
        Doctor.diagnose cliVersion strict host inspection

    let getStatus (cliVersion: string) (inspection: RepositoryInspection) : StatusSummary =
        let state = getInstallationState cliVersion inspection
        let verification = verify cliVersion false inspection

        let configurationStatus =
            match Inspection.tryObservation Desired.ConfigArtifactId inspection with
            | Some observation when observation.Exists ->
                verification.Checks
                |> List.tryFind (fun check -> check.Id = "configuration-shape")
                |> Option.map (fun check ->
                    match check.Status with
                    | Pass -> "valid"
                    | Warn -> "questionable"
                    | Fail -> "unreadable"
                    | Skipped -> "unknown")
                |> Option.defaultValue "valid"
            | _ -> "missing"

        let missingArtifacts =
            inspection.Artifacts
            |> List.filter (fun observation -> observation.Artifact.Required && not observation.Exists)

        let artifactStatus =
            match missingArtifacts with
            | [] -> "valid"
            | missing ->
                sprintf
                    "missing %s"
                    (missing
                     |> List.map (fun observation -> observation.Artifact.Path)
                     |> String.concat ", ")

        let integrationStatus =
            match inspection.Consumer with
            | None -> "unknown"
            | Some consumer ->
                match Inspection.missingScripts Desired.current inspection with
                | [] when consumer.DeclaresPublisherDependency -> "valid"
                | [] -> "scripts registered; package not declared as a dependency"
                | missing ->
                    sprintf
                        "missing scripts: %s"
                        (missing |> List.map (fun script -> script.Name) |> String.concat ", ")

        { Tool = Identity.ToolName
          Package = Identity.PackageName
          CliVersion = cliVersion
          RepositoryRoot = inspection.Root
          State = state
          InstalledVersion = inspection.Manifest |> Option.map (fun manifest -> manifest.InstalledVersion)
          ConfigurationVersion = inspection.Manifest |> Option.map (fun manifest -> manifest.ConfigurationVersion)
          ConfigurationStatus = configurationStatus
          ArtifactStatus = artifactStatus
          IntegrationStatus = integrationStatus
          Verification = verification
          UpgradeAvailable =
            match state with
            | UpgradeRequired (_, target) -> Some target
            | _ -> None }
