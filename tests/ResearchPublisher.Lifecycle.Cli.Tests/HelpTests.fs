namespace ResearchPublisher.Lifecycle.Cli.Tests

open System
open Xunit
open ResearchPublisher.Lifecycle.Core
open ResearchPublisher.Lifecycle.Cli

/// Help text is public documentation, so it is tested like one.
module HelpTests =

    let private general = Help.general "1.2.3"

    [<Fact>]
    let ``the general help lists every lifecycle command`` () =
        for command in [ "init"; "status"; "verify"; "upgrade"; "doctor" ] do
            Assert.Contains(sprintf "  %s" command, general)

    [<Fact>]
    let ``the general help lists every engine command`` () =
        for command in Args.engineCommandNames do
            Assert.Contains(sprintf "  %s" command, general)

    [<Fact>]
    let ``the general help documents the exit codes`` () =
        Assert.Contains("EXIT CODES", general)
        Assert.Contains("3 verification failed", general)
        Assert.Contains("7 unsupported platform", general)

    [<Fact>]
    let ``the general help shows the published invocation`` () =
        Assert.Contains(sprintf "npx %s" Identity.PackageName, general)

    [<Fact>]
    let ``every documented topic has its own help`` () =
        for topic in Args.helpTopics do
            if topic <> "help" then
                let text = Help.forCommand topic
                Assert.False(String.IsNullOrWhiteSpace text, topic)
                Assert.Contains(topic, text)

    [<Fact>]
    let ``lifecycle help states side effects and examples`` () =
        for command in [ "init"; "status"; "verify"; "upgrade"; "doctor" ] do
            let text = Help.forCommand command
            Assert.Contains("SIDE EFFECTS", text)
            Assert.Contains("OPTIONS", text)
            Assert.Contains("EXAMPLES", text)

    [<Fact>]
    let ``read only commands say so`` () =
        for command in [ "status"; "verify"; "doctor" ] do
            Assert.Contains("never writes to the repository", Help.forCommand command)

    [<Fact>]
    let ``init help states that it is idempotent and what it will not overwrite`` () =
        let text = Help.forCommand "init"
        Assert.Contains("Idempotent", text)
        Assert.Contains("Never overwrites a user-owned file", text)
