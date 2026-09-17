namespace ResearchPublisher.Lifecycle.Core.Tests

open Xunit
open ResearchPublisher.Lifecycle.Core

module RepositoryPathTests =

    [<Theory>]
    [<InlineData("../escape.md")>]
    [<InlineData("prompts/../../escape.md")>]
    [<InlineData("/etc/passwd")>]
    [<InlineData("")>]
    let ``rejects paths that leave the repository`` (candidate: string) =
        Assert.False(RepositoryPath.isSafe candidate)

    [<Theory>]
    [<InlineData("research-publisher.config.mjs")>]
    [<InlineData("prompts/research-publisher-mark-documents.md")>]
    [<InlineData(".echelon/research-publisher.json")>]
    let ``accepts ordinary repository paths`` (candidate: string) =
        Assert.True(RepositoryPath.isSafe candidate)

    [<Fact>]
    let ``resolve refuses traversal even when the prefix looks safe`` () =
        match RepositoryPath.resolve "/tmp/repo" "prompts/../../outside.md" with
        | Ok _ -> failwith "Expected traversal to be refused."
        | Result.Error message -> Assert.Contains("unsafe", message)

    [<Fact>]
    let ``normalize collapses separators`` () =
        Assert.Equal("a/b/c", RepositoryPath.normalize "a\\b//./c")

module HashTests =

    [<Fact>]
    let ``line endings do not change the hash`` () =
        Assert.Equal(Hash.ofText "one\ntwo\n", Hash.ofText "one\r\ntwo\r\n")

    [<Fact>]
    let ``different content hashes differently`` () =
        Assert.NotEqual<string>(Hash.ofText "one", Hash.ofText "two")

module VersioningTests =

    [<Theory>]
    [<InlineData("24.3.0", ">=24 <26", true)>]
    [<InlineData("26.0.0", ">=24 <26", false)>]
    [<InlineData("20.11.1", ">=24 <26", false)>]
    [<InlineData("24.0.0", ">=24", true)>]
    [<InlineData("18.0.0", ">=20 || >=24", false)>]
    let ``evaluates the engines ranges the package uses`` (version: string, range: string, expected: bool) =
        Assert.Equal(Some expected, Versioning.satisfies version range)

    [<Fact>]
    let ``reports ranges it cannot evaluate rather than guessing`` () =
        Assert.Equal(None, Versioning.satisfies "24.0.0" "^24.x-lts")

module TemplateTests =

    [<Theory>]
    [<InlineData("example-research", "Example Research")>]
    [<InlineData("@scope/my_research-repo", "My Research Repo")>]
    [<InlineData("", "Research Repository")>]
    let ``derives a site title the way the original initializer did`` (name: string, expected: string) =
        Assert.Equal(expected, Desired.titleFromName name)

    [<Theory>]
    [<InlineData("git+https://github.com/example/example-research.git", "https://github.com/example/example-research")>]
    [<InlineData("https://github.com/example/x", "https://github.com/example/x")>]
    [<InlineData("", "")>]
    let ``normalizes the repository url the way the original initializer did`` (raw: string, expected: string) =
        Assert.Equal(expected, Desired.repositoryUrl raw)

    [<Fact>]
    let ``the configuration template is a loadable module with the documented defaults`` () =
        let template =
            Desired.configTemplate
                { Name = "example-research"
                  RepositoryUrl = "git+https://github.com/example/example-research.git"
                  Scripts = Map.empty
                  DeclaresPublisherDependency = false }

        Assert.Contains("export default", template)
        Assert.Contains("title: \"Example Research\"", template)
        Assert.Contains("sourceUrl: \"https://github.com/example/example-research\"", template)
        Assert.Contains("include: [\"**/*.md\"]", template)
        Assert.Contains("branding: {", template)

    [<Fact>]
    let ``the marking prompt ships with the package`` () =
        TestPackage.useCheckout ()

        match Desired.tryMarkingPromptTemplate () with
        | Ok template -> Assert.Contains("Organize A Research Corpus By Reader Purpose", template)
        | Result.Error message -> failwith message

module ExitCodeTests =

    /// The exit codes are a public contract; this pins the documented values.
    [<Fact>]
    let ``exit codes match the documented table`` () =
        Assert.Equal(0, ExitCode.Success)
        Assert.Equal(1, ExitCode.InternalError)
        Assert.Equal(2, ExitCode.UsageError)
        Assert.Equal(3, ExitCode.VerificationFailed)
        Assert.Equal(4, ExitCode.IncompatibleInstallation)
        Assert.Equal(5, ExitCode.MigrationBlocked)
        Assert.Equal(6, ExitCode.PrerequisiteFailed)
        Assert.Equal(7, ExitCode.UnsupportedPlatform)
