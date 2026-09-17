namespace ResearchPublisher.Lifecycle.Core

open System

/// Just enough semantic-version handling to evaluate an npm `engines` range such
/// as ">=24 <26". Anything more exotic is reported as "not evaluated" rather than
/// guessed at.
module Versioning =

    let private parse (value: string) =
        let core = value.Split([| '-'; '+' |]).[0]

        let numbers =
            core.Split('.')
            |> Array.map (fun part ->
                match Int32.TryParse part with
                | true, number -> Some number
                | _ -> None)

        if numbers |> Array.exists Option.isNone then
            None
        else
            let values = numbers |> Array.map Option.get
            Some(Array.append values (Array.create (max 0 (3 - values.Length)) 0))

    let compare (left: string) (right: string) =
        match parse left, parse right with
        | Some left, Some right ->
            let rec loop index =
                if index >= 3 then Some 0
                elif left.[index] <> right.[index] then Some(compare left.[index] right.[index])
                else loop (index + 1)

            loop 0
        | _ -> None

    let private satisfiesComparator (version: string) (comparator: string) =
        let comparator = comparator.Trim()

        let operatorAndValue =
            [ ">="; "<="; ">"; "<"; "=" ]
            |> List.tryPick (fun operator ->
                if comparator.StartsWith(operator, StringComparison.Ordinal) then
                    Some(operator, comparator.Substring(operator.Length).Trim())
                else
                    None)
            |> Option.defaultValue ("=", comparator)

        let operator, target = operatorAndValue

        match compare version target with
        | None -> None
        | Some ordering ->
            match operator with
            | ">=" -> Some(ordering >= 0)
            | ">" -> Some(ordering > 0)
            | "<=" -> Some(ordering <= 0)
            | "<" -> Some(ordering < 0)
            | _ -> Some(ordering = 0)

    /// `None` means the range used syntax this evaluator does not implement.
    let satisfies (version: string) (range: string) =
        if String.IsNullOrWhiteSpace range then
            Some true
        else
            let alternatives = range.Split([| "||" |], StringSplitOptions.RemoveEmptyEntries)

            let results =
                alternatives
                |> Array.map (fun alternative ->
                    let comparators =
                        alternative.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)

                    let evaluated = comparators |> Array.map (satisfiesComparator version)

                    if evaluated |> Array.exists Option.isNone then
                        None
                    else
                        Some(evaluated |> Array.forall (fun result -> result = Some true)))

            if results |> Array.exists Option.isNone then
                None
            else
                Some(results |> Array.exists (fun result -> result = Some true))

/// Environment facts reported by the Node bootstrap. The bootstrap only measures;
/// every judgement about whether a value is acceptable is made here.
type HostEnvironment =
    { NodeVersion: string option
      Platform: string option
      Architecture: string option }

module HostEnvironment =

    [<Literal>]
    let NodeVersionVariable = "RESEARCH_PUBLISHER_NODE_VERSION"

    [<Literal>]
    let PlatformVariable = "RESEARCH_PUBLISHER_PLATFORM"

    [<Literal>]
    let ArchitectureVariable = "RESEARCH_PUBLISHER_ARCH"

    let private read name =
        match Environment.GetEnvironmentVariable(name: string) with
        | null -> None
        | "" -> None
        | value -> Some value

    let fromEnvironment () =
        { NodeVersion = read NodeVersionVariable |> Option.map (fun value -> value.TrimStart('v'))
          Platform = read PlatformVariable
          Architecture = read ArchitectureVariable }

type DiagnosticReport =
    { Diagnostics: Problem list
      /// True when nothing was reported at the severity the caller cares about.
      Healthy: bool }

/// Explains *why* an installation is wrong and what to do about it.
///
/// `verify` answers "is this valid?" with a pass or fail. `doctor` answers
/// "what is wrong and how do I fix it?", separating errors from things that are
/// merely worth knowing.
module Doctor =

    let private problem code severity title detail = Problem.create code severity title detail

    let private engineDiagnostics (host: HostEnvironment) =
        let requirement =
            PackageRoot.tryResolve ()
            |> Option.bind (fun root ->
                let packageJsonPath = System.IO.Path.Combine(root, "package.json")

                match FileSystem.tryReadText packageJsonPath with
                | Ok (Some text) ->
                    match Json.tryParse text with
                    | Ok document ->
                        use document = document

                        Json.tryProperty "engines" document.RootElement
                        |> Option.bind (Json.tryStringProperty "node")
                    | Result.Error _ -> None
                | _ -> None)

        match host.NodeVersion, requirement with
        | None, _ ->
            [ problem
                  "node-version-unknown"
                  Information
                  "The Node.js version could not be determined."
                  "Run the CLI through the packaged `research-publisher` launcher so it can report the host runtime." ]
        | Some version, None ->
            [ problem
                  "node-version"
                  Information
                  "Node.js runtime"
                  (sprintf "Running on Node.js %s. The package does not declare an engines requirement." version) ]
        | Some version, Some range ->
            match Versioning.satisfies version range with
            | Some true ->
                [ problem "node-version" Information "Node.js runtime" (sprintf "Node.js %s satisfies %s." version range) ]
            | Some false ->
                [ problem
                      "node-version-unsupported"
                      Warning
                      "The Node.js version is outside the supported range."
                      (sprintf "Node.js %s does not satisfy %s." version range)
                  |> Problem.withRemediation (sprintf "Install a Node.js release matching %s." range) ]
            | None ->
                [ problem
                      "node-version-unevaluated"
                      Information
                      "The Node.js requirement could not be evaluated."
                      (sprintf "Node.js %s; declared requirement %s." version range) ]

    let private packageAssetDiagnostics () =
        match PackageRoot.tryResolve () with
        | None ->
            [ problem
                  "package-root-missing"
                  Error
                  "The installed package directory could not be located."
                  "Templates and schemas ship inside the npm package, so `init` and `upgrade` cannot run without it."
              |> Problem.withRemediation (
                  sprintf "Reinstall %s, or set %s." Identity.PackageName PackageRoot.PackageRootVariable
              ) ]
        | Some root ->
            match Desired.tryMarkingPromptTemplate () with
            | Ok _ ->
                [ problem "package-assets" Information "Packaged runtime assets" (sprintf "Resolved from %s." root) ]
            | Result.Error message ->
                [ problem
                      "package-asset-missing"
                      Error
                      "A packaged runtime asset is missing."
                      message
                  |> Problem.withRemediation (sprintf "Reinstall %s." Identity.PackageName) ]

    let private repositoryDiagnostics cliVersion inspection =
        let state = Inspection.getInstallationState cliVersion inspection

        let baseDiagnostics =
            [ if not inspection.IsGitRepository then
                  yield
                      problem
                          "not-a-git-repository"
                          Information
                          "This directory is not a Git repository."
                          "Lifecycle commands still work, but generated files will not be version controlled."

              match inspection.PackageProblem with
              | Some problem -> yield problem
              | None ->
                  if not inspection.HasPackageJson then
                      yield
                          problem
                              "package-json-missing"
                              Error
                              "package.json is missing."
                              (sprintf "%s installs npm scripts and cannot run without one." Identity.PackageName)
                          |> Problem.withPath "package.json"
                          |> Problem.withRemediation "Run `npm init -y`." ]

        let stateDiagnostics =
            match state with
            | NotInstalled ->
                [ problem
                      "not-installed"
                      Warning
                      "The capability is not installed in this repository."
                      (sprintf "Neither %s nor %s was found." Desired.ConfigPath Identity.ManifestPath)
                  |> Problem.withRemediation (sprintf "Run `npx %s init`." Identity.PackageName) ]
            | Invalid problems -> problems
            | UpgradeRequired (current, target) ->
                let detail =
                    match current.ToolVersion with
                    | Some version ->
                        sprintf
                            "Recorded installation %s (configuration %d); this CLI is %s (configuration %d)."
                            version
                            current.ConfigurationVersion
                            target.ToolVersion
                            target.ConfigurationVersion
                    | None ->
                        sprintf
                            "The repository was installed before installation manifests existed, so %s is missing."
                            Identity.ManifestPath

                [ problem "upgrade-required" Warning "The installation is out of date." detail
                  |> Problem.withRemediation (sprintf "Run `npx %s upgrade`." Identity.PackageName) ]
            | Installed version ->
                [ problem
                      "installed"
                      Information
                      "The capability is installed."
                      (sprintf
                          "Version %s at configuration version %d."
                          (version.ToolVersion |> Option.defaultValue "unknown")
                          version.ConfigurationVersion) ]

        // When nothing is installed, a missing managed file is not a defect: it is
        // the expected state, already reported once as `not-installed`.
        let isInstalled =
            match state with
            | NotInstalled -> false
            | _ -> true

        let artifactDiagnostics =
            [ if isInstalled then
                for observation in inspection.Artifacts do
                    if not observation.Exists && observation.Artifact.Required then
                        yield
                            problem
                                "required-artifact-missing"
                                Error
                                "A required file is missing."
                                observation.Artifact.Description
                            |> Problem.withPath observation.Artifact.Path
                            |> Problem.withRemediation (sprintf "Run `npx %s init`." Identity.PackageName)
                    elif
                        observation.Artifact.Ownership = Shared
                        && ArtifactObservation.isLocallyModified observation
                    then
                        yield
                            problem
                                "shared-file-modified"
                                Information
                                "A shared file has local changes."
                                "The tool will not replace it during an upgrade, so it may drift from the packaged version."
                            |> Problem.withPath observation.Artifact.Path
                            |> Problem.withRemediation
                                "Delete the file and re-run `init` to take the packaged version, or keep the local one." ]

        let generatedDiagnostics =
            [ if isInstalled then
                for observation in inspection.Generated do
                  if not observation.Exists then
                      yield
                          problem
                              "generated-artifact-absent"
                              Information
                              "A generated path is not present."
                              (sprintf "%s has not been produced yet." observation.Artifact.Description)
                          |> Problem.withPath observation.Artifact.Path
                          |> Problem.withRemediation "Run `npm run research:build`." ]

        let scriptDiagnostics =
            match (if isInstalled then Inspection.missingScripts Desired.current inspection else []) with
            | [] -> []
            | missing ->
                [ problem
                      "package-scripts-missing"
                      Warning
                      "package.json does not define every script for this configuration version."
                      (missing |> List.map (fun script -> script.Name) |> String.concat ", ")
                  |> Problem.withPath "package.json"
                  |> Problem.withRemediation (sprintf "Run `npx %s init`." Identity.PackageName) ]

        let dependencyDiagnostics =
            match inspection.Consumer with
            | Some consumer when isInstalled && not consumer.DeclaresPublisherDependency ->
                [ problem
                      "dependency-not-declared"
                      Warning
                      (sprintf "%s is not a declared dependency." Identity.PackageName)
                      "Builds will use whatever version npx resolves at the time, which can change without notice."
                  |> Problem.withPath "package.json"
                  |> Problem.withRemediation (sprintf "Run `npm install -D %s`." Identity.PackageName) ]
            | _ -> []

        baseDiagnostics
        @ stateDiagnostics
        @ artifactDiagnostics
        @ scriptDiagnostics
        @ dependencyDiagnostics
        @ generatedDiagnostics

    /// Diagnose the environment and the installation.
    /// `strict` promotes warnings to failures for the purposes of the exit code.
    let diagnose (cliVersion: string) (strict: bool) (host: HostEnvironment) (inspection: RepositoryInspection) =
        let diagnostics =
            engineDiagnostics host
            @ packageAssetDiagnostics ()
            @ repositoryDiagnostics cliVersion inspection
            |> List.sortByDescending (fun item -> Severity.rank item.Severity)

        let threshold = if strict then Severity.rank Warning else Severity.rank Error

        { Diagnostics = diagnostics
          Healthy = diagnostics |> List.forall (fun item -> Severity.rank item.Severity < threshold) }
