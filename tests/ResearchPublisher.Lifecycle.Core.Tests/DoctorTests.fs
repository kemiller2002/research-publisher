namespace ResearchPublisher.Lifecycle.Core.Tests

open Xunit
open ResearchPublisher.Lifecycle.Core

module DoctorTests =

    let private version = "1.0.0"

    let private noHost =
        { NodeVersion = None
          Platform = None
          Architecture = None }

    let private diagnose strict host (repository: TestRepository) =
        TestPackage.useCheckout ()
        repository.Root |> Api.inspectRepository |> Api.diagnose version strict host

    let private codes (report: DiagnosticReport) =
        report.Diagnostics |> List.map (fun item -> item.Code) |> Set.ofList

    [<Fact>]
    let ``a healthy installation reports no errors`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore

        let report = diagnose false noHost repository
        Assert.True report.Healthy
        Assert.DoesNotContain(report.Diagnostics, fun item -> item.Severity = Error)

    [<Fact>]
    let ``an intentionally damaged installation is identified with a remedy`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore
        repository.Delete "prompts/research-publisher-mark-documents.md"

        let report = diagnose false noHost repository

        Assert.False report.Healthy
        Assert.Contains("required-artifact-missing", codes report)

        let problem =
            report.Diagnostics |> List.find (fun item -> item.Code = "required-artifact-missing")

        Assert.True problem.Remediation.IsSome
        Assert.True problem.Path.IsSome

    [<Fact>]
    let ``a corrupt manifest is reported as an error`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore
        repository.Write(Identity.ManifestPath, "{ not json")

        Assert.Contains("manifest-unreadable", codes (diagnose false noHost repository))

    [<Fact>]
    let ``an uninstalled repository is a warning, not an error`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        let report = diagnose false noHost repository
        Assert.True report.Healthy
        Assert.Contains("not-installed", codes report)

    [<Fact>]
    let ``strict mode turns warnings into an unhealthy verdict`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        Assert.True (diagnose false noHost repository).Healthy
        Assert.False (diagnose true noHost repository).Healthy

    [<Fact>]
    let ``an unsupported node version is reported with the required range`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore

        let host =
            { NodeVersion = Some "18.19.0"
              Platform = Some "linux"
              Architecture = Some "x64" }

        let report = diagnose false host repository
        Assert.Contains("node-version-unsupported", codes report)

    [<Fact>]
    let ``diagnosis never writes to the repository`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore
        let before = repository.Snapshot()

        diagnose true noHost repository |> ignore

        Assert.Equal(before, repository.Snapshot())

    [<Fact>]
    let ``every diagnostic carries a severity and a title`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        for item in (diagnose false noHost repository).Diagnostics do
            Assert.False(System.String.IsNullOrWhiteSpace item.Code)
            Assert.False(System.String.IsNullOrWhiteSpace item.Title)
            Assert.False(System.String.IsNullOrWhiteSpace item.Detail)
