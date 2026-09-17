namespace ResearchPublisher.Lifecycle.Core

open System
open System.IO
open System.Reflection

/// Where the distributed npm package lives on disk, and what version it is.
///
/// The npm `package.json` that ships in the tarball is the single authoritative
/// version source, so the CLI version can never drift from the released package.
module PackageRoot =

    [<Literal>]
    let PackageRootVariable = "RESEARCH_PUBLISHER_PACKAGE_ROOT"

    let private looksLikeOurPackage (packageJsonPath: string) =
        try
            match Json.tryParse (File.ReadAllText packageJsonPath) with
            | Ok document ->
                use document = document
                match Json.tryStringProperty "name" document.RootElement with
                | Some name -> name = Identity.PackageName
                | None -> false
            | Result.Error _ -> false
        with _ ->
            false

    let private probeUpwards (start: string) =
        let rec walk (directory: DirectoryInfo) depth =
            if isNull (box directory) || depth > 8 then
                None
            else
                let candidate = Path.Combine(directory.FullName, "package.json")
                if File.Exists candidate && looksLikeOurPackage candidate then
                    Some directory.FullName
                else
                    walk directory.Parent (depth + 1)

        try
            walk (DirectoryInfo start) 0
        with _ ->
            None

    /// Resolution order: the bootstrap's explicit hand-off, then a search upwards
    /// from the executable. The bootstrap only reports where it lives; it makes no
    /// decisions about the repository.
    let tryResolve () =
        let fromEnvironment =
            match Environment.GetEnvironmentVariable PackageRootVariable with
            | null -> None
            | "" -> None
            | value when Directory.Exists value -> Some(Path.GetFullPath value)
            | _ -> None

        match fromEnvironment with
        | Some root -> Some root
        | None -> probeUpwards AppContext.BaseDirectory

    let private assemblyVersion () =
        let assembly = Assembly.GetExecutingAssembly()
        match assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() with
        | null -> None
        | attribute ->
            let raw = attribute.InformationalVersion
            match raw.Split('+') |> Array.tryHead with
            | Some value when not (String.IsNullOrWhiteSpace value) -> Some value
            | _ -> None

    /// The version reported by `--version`. Read from the shipped package.json so
    /// it is by construction the published npm version.
    let version () =
        let fromPackageJson =
            tryResolve ()
            |> Option.bind (fun root ->
                let packageJsonPath = Path.Combine(root, "package.json")
                if File.Exists packageJsonPath then
                    match Json.tryParse (File.ReadAllText packageJsonPath) with
                    | Ok document ->
                        use document = document
                        Json.tryStringProperty "version" document.RootElement
                    | Result.Error _ -> None
                else
                    None)

        match fromPackageJson with
        | Some version -> version
        | None ->
            match assemblyVersion () with
            | Some version -> version
            | None -> "0.0.0-unknown"

    /// Load a runtime asset that ships inside the package (templates, schemas).
    let tryReadAsset (relativePath: string) =
        match tryResolve () with
        | None ->
            Result.Error(
                sprintf
                    "Could not locate the %s package directory. Set %s to the package root."
                    Identity.PackageName
                    PackageRootVariable
            )
        | Some root ->
            let assetPath = Path.GetFullPath(Path.Combine(root, relativePath))
            if File.Exists assetPath then
                try
                    Ok(File.ReadAllText assetPath)
                with error ->
                    Result.Error(sprintf "Could not read the packaged asset '%s': %s" relativePath error.Message)
            else
                Result.Error(sprintf "The packaged asset '%s' is missing from %s." relativePath root)
