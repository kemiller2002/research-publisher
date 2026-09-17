namespace ResearchPublisher.Lifecycle.Cli.Tests

open Xunit
open ResearchPublisher.Lifecycle.Cli

module ArgsTests =

    let private parse arguments = Args.parse arguments

    let private expectOk arguments =
        match parse arguments with
        | Ok command -> command
        | Result.Error message -> failwithf "Expected %A to parse, got: %s" arguments message

    let private expectError arguments =
        match parse arguments with
        | Ok command -> failwithf "Expected %A to be rejected, got %A" arguments command
        | Result.Error message -> message

    [<Fact>]
    let ``no arguments shows help rather than doing something`` () =
        Assert.Equal(Help None, expectOk [])

    [<Theory>]
    [<InlineData("--help")>]
    [<InlineData("-h")>]
    let ``the help flags are accepted`` (flag: string) = Assert.Equal(Help None, expectOk [ flag ])

    [<Theory>]
    [<InlineData("--version")>]
    [<InlineData("-v")>]
    let ``the version flags are accepted`` (flag: string) = Assert.Equal(Version, expectOk [ flag ])

    [<Theory>]
    [<InlineData("init")>]
    [<InlineData("status")>]
    [<InlineData("verify")>]
    [<InlineData("upgrade")>]
    [<InlineData("doctor")>]
    let ``each lifecycle command has command specific help`` (command: string) =
        Assert.Equal(Help(Some command), expectOk [ command; "--help" ])

    [<Fact>]
    let ``engine commands are documented even though they run elsewhere`` () =
        Assert.Equal(Help(Some "build"), expectOk [ "build"; "--help" ])

    [<Fact>]
    let ``init accepts dry run and check`` () =
        match expectOk [ "init"; "--dry-run"; "--check"; "--json"; "--verbose" ] with
        | Init options ->
            Assert.True options.DryRun
            Assert.True options.Check
            Assert.True options.Common.Json
            Assert.True options.Common.Verbose
        | other -> failwithf "Expected init, got %A" other

    [<Fact>]
    let ``verify accepts strict`` () =
        match expectOk [ "verify"; "--strict" ] with
        | Verify options -> Assert.True options.Strict
        | other -> failwithf "Expected verify, got %A" other

    [<Fact>]
    let ``doctor accepts strict`` () =
        match expectOk [ "doctor"; "--strict" ] with
        | Doctor options -> Assert.True options.Strict
        | other -> failwithf "Expected doctor, got %A" other

    [<Fact>]
    let ``the repository can be selected explicitly`` () =
        match expectOk [ "status"; "--repo"; "/tmp" ] with
        | Status options -> Assert.Equal("/tmp", options.Common.RepositoryRoot)
        | other -> failwithf "Expected status, got %A" other

    [<Fact>]
    let ``a config path selects the repository that contains it`` () =
        match expectOk [ "verify"; "--config"; "/tmp/repo/research-publisher.config.mjs" ] with
        | Verify options -> Assert.Equal("/tmp/repo", options.Common.RepositoryRoot)
        | other -> failwithf "Expected verify, got %A" other

    [<Fact>]
    let ``an unknown command is a usage error`` () =
        Assert.Contains("Unknown command", expectError [ "publish" ])

    [<Fact>]
    let ``an unknown option is a usage error`` () =
        Assert.Contains("Unknown option", expectError [ "status"; "--wat" ])

    [<Fact>]
    let ``an option a command does not support is rejected rather than ignored`` () =
        Assert.Contains("does not accept", expectError [ "status"; "--strict" ])
        Assert.Contains("does not accept", expectError [ "verify"; "--dry-run" ])

    [<Fact>]
    let ``a flag that needs a value is rejected without one`` () =
        Assert.Contains("requires a value", expectError [ "status"; "--config" ])

    [<Fact>]
    let ``the legacy install-prompt command still parses`` () =
        match expectOk [ "install-prompt"; "--config"; "/tmp/repo/research-publisher.config.mjs" ] with
        | InstallPrompt common -> Assert.Equal("/tmp/repo", common.RepositoryRoot)
        | other -> failwithf "Expected install-prompt, got %A" other
