namespace ResearchPublisher.Lifecycle.Cli

open System
open ResearchPublisher.Lifecycle.Core

/// The command-line adapter.
///
/// It parses arguments, calls `Api`, chooses an exit code and renders output.
/// No lifecycle decision is made here; every one of them lives in the core library
/// so other hosts can make the same decisions without a command line.
module Program =

    /// Maps a blocker to the documented exit code. Keeping the table in one place
    /// stops exception behaviour from quietly becoming the public contract.
    let private exitCodeForBlocker (problem: Problem) =
        match problem.Code with
        | "configuration-version-too-new"
        | "not-installed"
        | "installation-invalid"
        | "manifest-unreadable"
        | "manifest-schema-unsupported"
        | "required-artifact-missing" -> ExitCode.IncompatibleInstallation
        | "package-json-required"
        | "package-json-missing"
        | "package-json-invalid"
        | "prompt-template-unavailable"
        | "package-root-missing"
        | "package-asset-missing" -> ExitCode.PrerequisiteFailed
        | "unsafe-path" -> ExitCode.InternalError
        | _ -> ExitCode.MigrationBlocked

    let private blockedExitCode (plan: Plan) =
        match plan.Blockers with
        | [] -> ExitCode.Success
        | first :: _ -> exitCodeForBlocker first

    type private Writer(json: bool) =
        /// With `--json`, stdout carries exactly one JSON document, so anything
        /// meant for a person goes to stderr.
        member _.Report(text: string) =
            if json then eprintf "%s" text else printf "%s" text

        member _.Machine(text: string) = printfn "%s" text

    let private runPlanCommand
        (writer: Writer)
        (common: CommonOptions)
        (cliVersion: string)
        (dryRun: bool)
        (check: bool)
        (plan: Plan)
        =
        if not (Plan.isExecutable plan) then
            let code = blockedExitCode plan

            if common.Json then
                writer.Machine(JsonOutput.plan cliVersion dryRun plan code)
            else
                writer.Report(HumanOutput.plan common.Verbose dryRun plan)

            code
        elif check then
            let code =
                if Plan.hasChanges plan then ExitCode.VerificationFailed else ExitCode.Success

            if common.Json then
                writer.Machine(JsonOutput.plan cliVersion true plan code)
            else
                writer.Report(HumanOutput.plan common.Verbose true plan)

            code
        elif dryRun then
            if common.Json then
                writer.Machine(JsonOutput.plan cliVersion true plan ExitCode.Success)
            else
                writer.Report(HumanOutput.plan common.Verbose true plan)

            ExitCode.Success
        else
            let result = Api.apply plan

            let code =
                if result.Succeeded then ExitCode.Success else ExitCode.InternalError

            if common.Json then
                writer.Machine(JsonOutput.executionResult cliVersion result code)
            else
                writer.Report(HumanOutput.executionResult common.Verbose result)

            code

    let private execute (command: Command) =
        let cliVersion = Api.cliVersion ()

        match command with
        | Command.Version ->
            printfn "%s" cliVersion
            ExitCode.Success
        | Command.Help None ->
            printfn "%s" (Help.general cliVersion)
            ExitCode.Success
        | Command.Help (Some topic) ->
            printfn "%s" (Help.forCommand topic)
            ExitCode.Success
        | Command.Status options ->
            let writer = Writer(options.Common.Json)
            let inspection = Api.inspectRepository options.Common.RepositoryRoot
            let summary = Api.getStatus cliVersion inspection

            if options.Common.Json then
                writer.Machine(JsonOutput.status summary ExitCode.Success)
            else
                writer.Report(HumanOutput.status options.Common.Verbose summary)

            ExitCode.Success
        | Command.Verify options ->
            let writer = Writer(options.Common.Json)
            let inspection = Api.inspectRepository options.Common.RepositoryRoot
            let report = Api.verify cliVersion options.Strict inspection

            let code =
                if report.Passed then ExitCode.Success else ExitCode.VerificationFailed

            if options.Common.Json then
                writer.Machine(JsonOutput.verification cliVersion inspection.Root report code)
            else
                writer.Report(HumanOutput.verification options.Common.Verbose report)

            code
        | Command.Doctor options ->
            let writer = Writer(options.Common.Json)
            let inspection = Api.inspectRepository options.Common.RepositoryRoot
            let host = HostEnvironment.fromEnvironment ()
            let report = Api.diagnose cliVersion options.Strict host inspection

            let code =
                if report.Healthy then ExitCode.Success else ExitCode.PrerequisiteFailed

            if options.Common.Json then
                writer.Machine(JsonOutput.diagnostics cliVersion inspection.Root options.Strict report code)
            else
                writer.Report(HumanOutput.diagnostics options.Common.Verbose report)

            code
        | Command.Init options ->
            let writer = Writer(options.Common.Json)
            let inspection = Api.inspectRepository options.Common.RepositoryRoot
            let plan = Api.createInitializationPlan cliVersion inspection
            runPlanCommand writer options.Common cliVersion options.DryRun options.Check plan
        | Command.Upgrade options ->
            let writer = Writer(options.Common.Json)
            let inspection = Api.inspectRepository options.Common.RepositoryRoot
            let plan = Api.planUpgrade cliVersion inspection
            runPlanCommand writer options.Common cliVersion options.DryRun options.Check plan
        | Command.InstallPrompt common ->
            let writer = Writer(common.Json)
            let inspection = Api.inspectRepository common.RepositoryRoot
            let plan = Api.createPromptOnlyPlan cliVersion inspection
            runPlanCommand writer common cliVersion false false plan

    [<EntryPoint>]
    let main argv =
        let arguments = List.ofArray argv
        let wantsJson = arguments |> List.contains "--json"

        try
            match Args.parse arguments with
            | Ok command -> execute command
            | Result.Error message ->
                if wantsJson then
                    printfn "%s" (JsonOutput.failure (Api.cliVersion ()) "usage" message ExitCode.UsageError)
                else
                    eprintfn "%s" message
                    eprintfn "Run `%s --help` for usage." Identity.ExecutableName

                ExitCode.UsageError
        with error ->
            if wantsJson then
                printfn "%s" (JsonOutput.failure (Api.cliVersion ()) "internal" error.Message ExitCode.InternalError)
            else
                eprintfn "%s: %s" Identity.ExecutableName error.Message

            ExitCode.InternalError
