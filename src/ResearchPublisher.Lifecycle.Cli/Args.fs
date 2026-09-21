namespace ResearchPublisher.Lifecycle.Cli

open System
open System.IO

/// Options every command accepts.
type CommonOptions =
    { RepositoryRoot: string
      Json: bool
      Verbose: bool }

type InitOptions =
    { Common: CommonOptions
      DryRun: bool
      Check: bool }

type StatusOptions = { Common: CommonOptions }

type VerifyOptions =
    { Common: CommonOptions
      Strict: bool }

type UpgradeOptions =
    { Common: CommonOptions
      DryRun: bool
      Check: bool }

type DoctorOptions =
    { Common: CommonOptions
      Strict: bool }

/// The parsed intent of one invocation.
type Command =
    | Init of InitOptions
    | Status of StatusOptions
    | Verify of VerifyOptions
    | Upgrade of UpgradeOptions
    | Doctor of DoctorOptions
    /// Retained so the pre-lifecycle `install-prompt` command keeps working.
    | InstallPrompt of CommonOptions
    | CompileSemantics of CommonOptions
    | Help of topic: string option
    | Version

/// A small hand-written parser. The CLI surface is deliberately narrow, so a
/// framework would add a dependency without removing any complexity.
module Args =

    /// Commands this executable runs.
    let commandNames =
        [ "init"; "status"; "verify"; "upgrade"; "doctor"; "install-prompt"; "help" ]

    let private internalCommandNames = [ "compile-semantics" ]
    let private acceptedCommandNames = commandNames @ internalCommandNames

    /// Commands handled by the JavaScript publishing engine. They are not run here,
    /// but `--help` documents them because the executable name is shared.
    let engineCommandNames =
        [ "build"; "dev"; "validate"; "inventory"; "check-links"; "clean"; "preview"; "migrate" ]

    /// Everything `--help` will describe.
    let helpTopics = commandNames @ engineCommandNames

    let private commonFlags = [ "--json"; "--verbose"; "--config"; "--repo"; "-C" ]

    /// Flags each command accepts beyond the common ones. Anything else is a usage
    /// error rather than a silently ignored argument.
    let private extraFlags command =
        match command with
        | "init"
        | "upgrade" -> [ "--dry-run"; "--check" ]
        | "verify"
        | "doctor" -> [ "--strict" ]
        | _ -> []

    type private Accumulator =
        { Root: string option
          Json: bool
          Verbose: bool
          DryRun: bool
          Check: bool
          Strict: bool }

    let private emptyAccumulator =
        { Root = None
          Json = false
          Verbose = false
          DryRun = false
          Check = false
          Strict = false }

    /// The repository is the directory holding the configuration file when
    /// `--config` is given, otherwise the working directory.
    let private repositoryRootFromConfig (configPath: string) =
        let full = Path.GetFullPath configPath

        if Directory.Exists full then
            full
        else
            let directory = Path.GetDirectoryName full
            if String.IsNullOrEmpty directory then Directory.GetCurrentDirectory() else directory

    let private parseOptions (command: string) (arguments: string list) =
        let allowed = Set.ofList (commonFlags @ extraFlags command)

        let rec loop (accumulator: Accumulator) remaining =
            match remaining with
            | [] -> Ok accumulator
            | (flag: string) :: rest when flag = "--json" && allowed.Contains flag ->
                loop { accumulator with Json = true } rest
            | flag :: rest when flag = "--verbose" && allowed.Contains flag ->
                loop { accumulator with Verbose = true } rest
            | flag :: rest when flag = "--dry-run" && allowed.Contains flag ->
                loop { accumulator with DryRun = true } rest
            | flag :: rest when flag = "--check" && allowed.Contains flag ->
                loop { accumulator with Check = true } rest
            | flag :: rest when flag = "--strict" && allowed.Contains flag ->
                loop { accumulator with Strict = true } rest
            | flag :: value :: rest when flag = "--config" ->
                loop { accumulator with Root = Some(repositoryRootFromConfig value) } rest
            | flag :: value :: rest when (flag = "--repo" || flag = "-C") ->
                loop { accumulator with Root = Some(Path.GetFullPath value) } rest
            | flag :: [] when flag = "--config" || flag = "--repo" || flag = "-C" ->
                Result.Error(sprintf "%s requires a value." flag)
            | flag :: _ when flag.StartsWith("-", StringComparison.Ordinal) ->
                if Set.contains flag (Set.ofList (commonFlags @ [ "--dry-run"; "--check"; "--strict" ])) then
                    Result.Error(sprintf "%s does not accept %s." command flag)
                else
                    Result.Error(sprintf "Unknown option %s." flag)
            | value :: _ -> Result.Error(sprintf "Unexpected argument '%s'." value)

        loop emptyAccumulator arguments

    let private toCommon (accumulator: Accumulator) =
        { RepositoryRoot =
            accumulator.Root
            |> Option.defaultWith (fun () -> Directory.GetCurrentDirectory())
          Json = accumulator.Json
          Verbose = accumulator.Verbose }

    /// Parse an argument vector (excluding the executable name).
    let parse (argv: string list) : Result<Command, string> =
        let wantsHelp = argv |> List.exists (fun argument -> argument = "--help" || argument = "-h")
        let wantsVersion = argv |> List.exists (fun argument -> argument = "--version" || argument = "-v")

        let positional =
            argv
            |> List.tryFind (fun argument -> not (argument.StartsWith("-", StringComparison.Ordinal)))

        match argv with
        | [] -> Ok(Help None)
        | _ when wantsHelp ->
            match positional with
            | Some name when List.contains name helpTopics -> Ok(Help(Some name))
            | Some name -> Result.Error(sprintf "Unknown command '%s'." name)
            | None -> Ok(Help None)
        | _ when wantsVersion -> Ok Version
        | _ ->

        match positional with
        | None ->
            let unknown = argv |> List.head
            Result.Error(sprintf "Unknown option %s." unknown)
        | Some "help" ->
            let topic =
                argv
                |> List.filter (fun argument -> not (argument.StartsWith("-", StringComparison.Ordinal)))
                |> List.tryItem 1

            match topic with
            | Some name when not (List.contains name helpTopics) ->
                Result.Error(sprintf "Unknown command '%s'." name)
            | topic -> Ok(Help topic)
        | Some name when not (List.contains name acceptedCommandNames) ->
            Result.Error(sprintf "Unknown command '%s'." name)
        | Some name ->
            let rest =
                let index = argv |> List.findIndex (fun argument -> argument = name)
                argv |> List.mapi (fun position argument -> position, argument)
                |> List.filter (fun (position, _) -> position <> index)
                |> List.map snd

            match parseOptions name rest with
            | Result.Error message -> Result.Error message
            | Ok accumulator ->
                let common = toCommon accumulator

                match name with
                | "init" ->
                    Ok(
                        Init
                            { Common = common
                              DryRun = accumulator.DryRun
                              Check = accumulator.Check }
                    )
                | "status" -> Ok(Status { Common = common })
                | "verify" -> Ok(Verify { Common = common; Strict = accumulator.Strict })
                | "upgrade" ->
                    Ok(
                        Upgrade
                            { Common = common
                              DryRun = accumulator.DryRun
                              Check = accumulator.Check }
                    )
                | "doctor" -> Ok(Doctor { Common = common; Strict = accumulator.Strict })
                | "install-prompt" -> Ok(InstallPrompt common)
                | "compile-semantics" -> Ok(CompileSemantics common)
                | other -> Result.Error(sprintf "Unknown command '%s'." other)
