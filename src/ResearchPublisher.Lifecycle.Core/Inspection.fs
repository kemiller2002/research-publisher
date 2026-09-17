namespace ResearchPublisher.Lifecycle.Core

open System
open System.IO
open System.Text.Json

/// What is actually on disk for one managed path.
type ArtifactObservation =
    { Artifact: ManagedArtifact
      Exists: bool
      IsDirectory: bool
      /// Hash of the current content, for files only.
      Hash: string option
      /// Hash recorded in the manifest when the tool last wrote or adopted it.
      RecordedHash: string option }

module ArtifactObservation =

    /// True when the tool wrote this file, recorded its hash, and the content has
    /// since changed. Used to protect local edits during upgrades.
    let isLocallyModified observation =
        match observation.RecordedHash, observation.Hash with
        | Some recorded, Some current -> recorded <> current
        | _ -> false

/// Everything the lifecycle commands read from a repository, gathered once.
/// Inspection never mutates anything.
type RepositoryInspection =
    { Root: string
      IsGitRepository: bool
      PackageJsonPath: string
      HasPackageJson: bool
      /// Set when package.json exists but could not be understood.
      PackageProblem: Problem option
      Consumer: ConsumerPackage option
      ManifestExists: bool
      Manifest: Manifest option
      ManifestProblem: Problem option
      Artifacts: ArtifactObservation list
      Generated: ArtifactObservation list }

module Inspection =

    let private readConsumerPackage (path: string) =
        match FileSystem.tryReadText path with
        | Result.Error message ->
            Result.Error(
                Problem.create "package-json-unreadable" Error "package.json could not be read." message
                |> Problem.withPath "package.json"
            )
        | Ok None -> Ok None
        | Ok (Some text) ->
            match Json.tryParse text with
            | Result.Error message ->
                Result.Error(
                    Problem.create "package-json-invalid" Error "package.json is not valid JSON." message
                    |> Problem.withPath "package.json"
                    |> Problem.withRemediation "Fix the JSON syntax before running lifecycle commands."
                )
            | Ok document ->
                use document = document
                let root = document.RootElement

                let name =
                    Json.tryStringProperty "name" root
                    |> Option.defaultValue (Path.GetFileName(Path.GetDirectoryName path))

                let repositoryUrl =
                    match Json.tryProperty "repository" root with
                    | Some element when element.ValueKind = JsonValueKind.String -> element.GetString()
                    | Some element when element.ValueKind = JsonValueKind.Object ->
                        Json.tryStringProperty "url" element |> Option.defaultValue ""
                    | _ -> ""

                let scripts =
                    match Json.tryProperty "scripts" root with
                    | Some element when element.ValueKind = JsonValueKind.Object ->
                        element.EnumerateObject()
                        |> Seq.choose (fun property ->
                            if property.Value.ValueKind = JsonValueKind.String then
                                Some(property.Name, property.Value.GetString())
                            else
                                None)
                        |> Map.ofSeq
                    | _ -> Map.empty

                let declaresDependency =
                    [ "dependencies"; "devDependencies"; "peerDependencies"; "optionalDependencies" ]
                    |> List.exists (fun section ->
                        match Json.tryProperty section root with
                        | Some element when element.ValueKind = JsonValueKind.Object ->
                            (Json.tryProperty Identity.PackageName element).IsSome
                        | _ -> false)

                Ok(
                    Some
                        { Name = name
                          RepositoryUrl = repositoryUrl
                          Scripts = scripts
                          DeclaresPublisherDependency = declaresDependency }
                )

    let private observe (root: string) (recordedHashes: Map<string, string>) (artifact: ManagedArtifact) =
        match RepositoryPath.resolve root artifact.Path with
        | Result.Error _ ->
            { Artifact = artifact
              Exists = false
              IsDirectory = false
              Hash = None
              RecordedHash = None }
        | Ok absolutePath ->
            let isDirectory = Directory.Exists absolutePath
            let exists = isDirectory || File.Exists absolutePath

            { Artifact = artifact
              Exists = exists
              IsDirectory = isDirectory
              Hash = (if isDirectory then None else Hash.ofFile absolutePath)
              RecordedHash = Map.tryFind artifact.Id recordedHashes }

    /// Read the repository. This is the only entry point that touches the disk for
    /// state determination, and it is side-effect free.
    let inspectRepository (repositoryRoot: string) : RepositoryInspection =
        let root = Path.GetFullPath repositoryRoot
        let packageJsonPath = Path.Combine(root, "package.json")

        let consumerResult = readConsumerPackage packageJsonPath

        let manifestPath = Path.Combine(root, Identity.ManifestPath)
        let manifestExists = File.Exists manifestPath

        let manifest, manifestProblem =
            if not manifestExists then
                None, None
            else
                match FileSystem.tryReadText manifestPath with
                | Result.Error message ->
                    None,
                    Some(
                        Problem.create "manifest-unreadable" Error "The installation manifest could not be read." message
                        |> Problem.withPath Identity.ManifestPath
                    )
                | Ok None -> None, None
                | Ok (Some text) ->
                    match Manifest.parse text with
                    | Ok manifest -> Some manifest, None
                    | Result.Error problem -> None, Some problem

        let recordedHashes =
            manifest
            |> Option.map (fun manifest ->
                manifest.ManagedArtifacts
                |> List.choose (fun artifact -> artifact.Hash |> Option.map (fun hash -> artifact.Id, hash))
                |> Map.ofList)
            |> Option.defaultValue Map.empty

        let desired = Desired.current

        { Root = root
          IsGitRepository = Directory.Exists(Path.Combine(root, ".git"))
          PackageJsonPath = packageJsonPath
          HasPackageJson = File.Exists packageJsonPath
          PackageProblem =
            match consumerResult with
            | Result.Error problem -> Some problem
            | Ok _ -> None
          Consumer =
            match consumerResult with
            | Ok consumer -> consumer
            | Result.Error _ -> None
          ManifestExists = manifestExists
          Manifest = manifest
          ManifestProblem = manifestProblem
          Artifacts = desired.Artifacts |> List.map (observe root recordedHashes)
          Generated = Desired.generatedArtifacts |> List.map (observe root recordedHashes) }

    let tryObservation id inspection =
        inspection.Artifacts |> List.tryFind (fun observation -> observation.Artifact.Id = id)

    let private configPresent inspection =
        match tryObservation Desired.ConfigArtifactId inspection with
        | Some observation -> observation.Exists
        | None -> false

    /// Scripts from the desired state that package.json does not already define.
    let missingScripts (desired: DesiredState) inspection =
        match inspection.Consumer with
        | None -> desired.Scripts
        | Some consumer ->
            desired.Scripts
            |> List.filter (fun script -> not (consumer.Scripts.ContainsKey script.Name))

    /// Derive the lifecycle state. Pure with respect to the inspection.
    let getInstallationState (cliVersion: string) (inspection: RepositoryInspection) : InstallationState =
        let target =
            { ToolVersion = cliVersion
              ConfigurationVersion = Identity.CurrentConfigurationVersion }

        match inspection.PackageProblem, inspection.ManifestProblem with
        | Some problem, _ when inspection.ManifestExists || configPresent inspection -> Invalid [ problem ]
        | _, Some problem -> Invalid [ problem ]
        | _ ->

        match inspection.Manifest with
        | None ->
            if configPresent inspection then
                // Installed by a release that predates installation manifests.
                let unmanaged: InstalledVersion = { ToolVersion = None; ConfigurationVersion = 0 }
                UpgradeRequired(unmanaged, target)
            else
                NotInstalled
        | Some manifest ->
            let current: InstalledVersion =
                { ToolVersion = Some manifest.InstalledVersion
                  ConfigurationVersion = manifest.ConfigurationVersion }

            if manifest.ConfigurationVersion > Identity.CurrentConfigurationVersion then
                Invalid
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
            else
                let missingRequired =
                    inspection.Artifacts
                    |> List.filter (fun observation -> observation.Artifact.Required && not observation.Exists)

                if not missingRequired.IsEmpty then
                    Invalid
                        [ for observation in missingRequired ->
                            Problem.create
                                "required-artifact-missing"
                                Error
                                "A required file recorded in the installation manifest is missing."
                                observation.Artifact.Description
                            |> Problem.withPath observation.Artifact.Path
                            |> Problem.withRemediation (
                                sprintf "Run `npx %s init` to restore it." Identity.PackageName
                            ) ]
                elif manifest.ConfigurationVersion < Identity.CurrentConfigurationVersion then
                    UpgradeRequired(current, target)
                elif manifest.InstalledVersion <> cliVersion then
                    UpgradeRequired(current, target)
                else
                    Installed current
