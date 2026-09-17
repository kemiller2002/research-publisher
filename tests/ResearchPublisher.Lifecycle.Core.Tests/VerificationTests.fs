namespace ResearchPublisher.Lifecycle.Core.Tests

open Xunit
open ResearchPublisher.Lifecycle.Core

module VerificationTests =

    let private version = "1.0.0"

    let private install (repository: TestRepository) =
        TestPackage.useCheckout ()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore

    /// A repository that also declares the dependency, so strict mode has nothing
    /// else to complain about.
    let private installStrictClean (repository: TestRepository) =
        TestPackage.useCheckout ()

        repository.WritePackageJson """{
  "name": "example-research",
  "private": true,
  "devDependencies": { "@echelon-foundry/research-publisher": "^1.0.0" },
  "scripts": {}
}
"""

        Api.initialize version repository.Root |> ignore

    let private verify strict (repository: TestRepository) =
        repository.Root |> Api.inspectRepository |> Api.verify version strict

    [<Fact>]
    let ``a fresh installation verifies`` () =
        use repository = new TestRepository()
        install repository

        Assert.True((verify false repository).Passed)

    [<Fact>]
    let ``verification never writes to the repository`` () =
        use repository = new TestRepository()
        install repository
        let before = repository.Snapshot()

        verify true repository |> ignore

        Assert.Equal(before, repository.Snapshot())

    [<Fact>]
    let ``a missing manifest fails verification`` () =
        use repository = new TestRepository()
        install repository
        repository.Delete Identity.ManifestPath

        let report = verify false repository
        Assert.False report.Passed
        Assert.Contains(report.Checks, fun check -> check.Id = "manifest" && check.Status = Fail)

    [<Fact>]
    let ``a missing configuration fails verification`` () =
        use repository = new TestRepository()
        install repository
        repository.Delete "research-publisher.config.mjs"

        Assert.False (verify false repository).Passed

    [<Fact>]
    let ``an out of date configuration version fails verification`` () =
        use repository = new TestRepository()
        Fixtures.version1 repository "LEGACY PROMPT\n" "LEGACY PROMPT\n"

        let report = verify false repository
        Assert.False report.Passed
        Assert.Contains(report.Checks, fun check -> check.Id = "configuration-version" && check.Status = Fail)

    [<Fact>]
    let ``strict mode turns an undeclared dependency into a failure`` () =
        use repository = new TestRepository()
        install repository

        Assert.True (verify false repository).Passed
        Assert.False (verify true repository).Passed

    [<Fact>]
    let ``strict mode never hides a problem the normal mode reports`` () =
        use repository = new TestRepository()
        install repository
        repository.Delete Identity.ManifestPath

        let normal = verify false repository
        let strict = verify true repository

        let failing (report: VerificationReport) =
            report.Checks
            |> List.filter (fun check -> check.Status = Fail)
            |> List.map (fun check -> check.Id)
            |> Set.ofList

        Assert.True(Set.isSubset (failing normal) (failing strict))

    [<Fact>]
    let ``a locally edited shared file is not treated as a defect`` () =
        use repository = new TestRepository()
        installStrictClean repository
        repository.Write("prompts/research-publisher-mark-documents.md", "MY OWN PROMPT\n")

        Assert.True (verify false repository).Passed
        Assert.True (verify true repository).Passed

    [<Fact>]
    let ``a version drift is a warning, not a failure`` () =
        use repository = new TestRepository()
        install repository

        let report = repository.Root |> Api.inspectRepository |> Api.verify "9.9.9" false
        Assert.Contains(report.Checks, fun check -> check.Id = "installed-version" && check.Status = Warn)
