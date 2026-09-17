namespace ResearchPublisher.Lifecycle.Core

open System

type CheckStatus =
    | Pass
    | Warn
    | Fail
    | Skipped

module CheckStatus =

    let toWire status =
        match status with
        | Pass -> "pass"
        | Warn -> "warn"
        | Fail -> "fail"
        | Skipped -> "skipped"

type Check =
    { Id: string
      Title: string
      Status: CheckStatus
      Detail: string
      Path: string option
      Remediation: string option }

type VerificationReport =
    { Strict: bool
      Checks: Check list
      /// True when the repository satisfies the contract at the requested strictness.
      Passed: bool }

/// Validates that the capability is correctly installed. Read-only.
///
/// Scope boundary: this validates the *installation*. Validating the research
/// corpus itself (front matter, links, duplicate identifiers) remains the job of
/// `research-publisher validate`, which runs the publishing engine.
module Verification =

    let private check id title status detail =
        { Id = id
          Title = title
          Status = status
          Detail = detail
          Path = None
          Remediation = None }

    let private at path (check: Check) : Check = { check with Path = Some path }

    let private fix remediation (check: Check) : Check =
        { check with Remediation = Some remediation }

    /// In strict mode every warning is a failure. Nothing else changes, so a strict
    /// run never reports a problem a normal run would have hidden entirely.
    let private effectiveStatus strict status =
        match strict, status with
        | true, Warn -> Fail
        | _, status -> status

    let private manifestCheck inspection =
        match inspection.ManifestProblem with
        | Some problem ->
            check "manifest" "Installation manifest" Fail problem.Detail
            |> at Identity.ManifestPath
            |> fix (problem.Remediation |> Option.defaultValue "Run `init` to rebuild the manifest.")
        | None ->
            match inspection.Manifest with
            | None ->
                check
                    "manifest"
                    "Installation manifest"
                    Fail
                    (sprintf "%s is missing, so the installation is not recorded." Identity.ManifestPath)
                |> at Identity.ManifestPath
                |> fix (sprintf "Run `npx %s init`." Identity.PackageName)
            | Some manifest ->
                check
                    "manifest"
                    "Installation manifest"
                    Pass
                    (sprintf
                        "Recorded installation %s at configuration version %d."
                        manifest.InstalledVersion
                        manifest.ConfigurationVersion)
                |> at Identity.ManifestPath

    let private artifactChecks inspection =
        inspection.Artifacts
        |> List.filter (fun observation -> observation.Artifact.Id <> Desired.ManifestArtifactId)
        |> List.map (fun observation ->
            let artifact = observation.Artifact

            let title = sprintf "%s (%s)" artifact.Path (Ownership.toWire artifact.Ownership)

            if observation.Exists then
                check (sprintf "artifact:%s" artifact.Id) title Pass artifact.Description
                |> at artifact.Path
            else
                check
                    (sprintf "artifact:%s" artifact.Id)
                    title
                    (if artifact.Required then Fail else Warn)
                    (sprintf "Missing. %s" artifact.Description)
                |> at artifact.Path
                |> fix (sprintf "Run `npx %s init` to restore it." Identity.PackageName))

    let private configurationShapeCheck inspection =
        match Inspection.tryObservation Desired.ConfigArtifactId inspection with
        | Some observation when observation.Exists ->
            match RepositoryPath.resolve inspection.Root observation.Artifact.Path with
            | Result.Error message -> check "configuration-shape" "Configuration shape" Fail message
            | Ok absolutePath ->
                match FileSystem.tryReadText absolutePath with
                | Ok (Some text) when text.Contains "export default" ->
                    check "configuration-shape" "Configuration shape" Pass "Exports a default configuration object."
                    |> at observation.Artifact.Path
                | Ok (Some _) ->
                    check
                        "configuration-shape"
                        "Configuration shape"
                        Warn
                        "No `export default` was found, so the publishing engine may not be able to load it."
                    |> at observation.Artifact.Path
                    |> fix "Ensure the configuration module has a default export."
                | Ok None
                | Result.Error _ ->
                    check "configuration-shape" "Configuration shape" Fail "The configuration file could not be read."
                    |> at observation.Artifact.Path
        | _ ->
            check "configuration-shape" "Configuration shape" Skipped "No configuration file to inspect."

    let private scriptChecks inspection =
        let missing = Inspection.missingScripts Desired.current inspection

        if not inspection.HasPackageJson then
            [ check "package-scripts" "Package scripts" Fail "package.json is missing."
              |> at "package.json"
              |> fix "Run `npm init -y`, then re-run `init`." ]
        elif List.isEmpty missing then
            [ check "package-scripts" "Package scripts" Pass "All lifecycle and engine scripts are defined."
              |> at "package.json" ]
        else
            [ check
                "package-scripts"
                "Package scripts"
                Warn
                (sprintf
                    "package.json does not define %s."
                    (missing |> List.map (fun script -> script.Name) |> String.concat ", "))
              |> at "package.json"
              |> fix (sprintf "Run `npx %s init` to add the missing scripts." Identity.PackageName) ]

    let private dependencyCheck inspection =
        match inspection.Consumer with
        | Some consumer when consumer.DeclaresPublisherDependency ->
            check "declared-dependency" "Declared dependency" Pass (sprintf "%s is declared." Identity.PackageName)
            |> at "package.json"
        | Some _ ->
            check
                "declared-dependency"
                "Declared dependency"
                Warn
                (sprintf
                    "%s is not listed in package.json, so builds depend on whatever version npx resolves."
                    Identity.PackageName)
            |> at "package.json"
            |> fix (sprintf "Run `npm install -D %s`." Identity.PackageName)
        | None -> check "declared-dependency" "Declared dependency" Skipped "package.json could not be read."

    let private versionChecks cliVersion inspection =
        match inspection.Manifest with
        | None -> []
        | Some manifest ->
            let configuration =
                if manifest.ConfigurationVersion = Identity.CurrentConfigurationVersion then
                    check
                        "configuration-version"
                        "Configuration version"
                        Pass
                        (sprintf "At the current version (%d)." Identity.CurrentConfigurationVersion)
                elif manifest.ConfigurationVersion > Identity.CurrentConfigurationVersion then
                    check
                        "configuration-version"
                        "Configuration version"
                        Fail
                        (sprintf
                            "The repository is at version %d but this CLI understands up to %d."
                            manifest.ConfigurationVersion
                            Identity.CurrentConfigurationVersion)
                    |> fix (sprintf "Install a newer %s release." Identity.PackageName)
                else
                    check
                        "configuration-version"
                        "Configuration version"
                        Fail
                        (sprintf
                            "The repository is at version %d; this release expects %d."
                            manifest.ConfigurationVersion
                            Identity.CurrentConfigurationVersion)
                    |> fix (sprintf "Run `npx %s upgrade`." Identity.PackageName)

            let installed =
                if manifest.InstalledVersion = cliVersion then
                    check "installed-version" "Installed version" Pass (sprintf "Matches the CLI (%s)." cliVersion)
                else
                    check
                        "installed-version"
                        "Installed version"
                        Warn
                        (sprintf
                            "The manifest records %s but the CLI is %s."
                            manifest.InstalledVersion
                            cliVersion)
                    |> fix (sprintf "Run `npx %s upgrade` to record the current version." Identity.PackageName)

            [ configuration; installed ]

    let private sharedFileChecks inspection =
        inspection.Artifacts
        |> List.filter (fun observation ->
            observation.Artifact.Ownership = Shared && ArtifactObservation.isLocallyModified observation)
        |> List.map (fun observation ->
            // Editing a shared file is supported, so this is reported rather than
            // penalized: strict mode must not fail a repository for doing something
            // the ownership model explicitly allows.
            check
                (sprintf "shared-modified:%s" observation.Artifact.Id)
                "Locally modified shared file"
                Pass
                "Changed locally after the tool wrote it. Upgrades will preserve the local version."
            |> at observation.Artifact.Path)

    /// Run every check. Nothing here writes to the repository.
    let verify (cliVersion: string) (strict: bool) (inspection: RepositoryInspection) : VerificationReport =
        let checks =
            [ yield manifestCheck inspection
              yield! artifactChecks inspection
              yield configurationShapeCheck inspection
              yield! versionChecks cliVersion inspection
              yield! scriptChecks inspection
              yield dependencyCheck inspection
              yield! sharedFileChecks inspection ]
            |> List.map (fun item ->
                { item with
                    Status = effectiveStatus strict item.Status })

        { Strict = strict
          Checks = checks
          Passed = checks |> List.forall (fun item -> item.Status <> Fail) }
