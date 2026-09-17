namespace ResearchPublisher.Lifecycle.Core.Tests

open Xunit
open ResearchPublisher.Lifecycle.Core

module InspectionTests =

    let private version = "1.0.0"

    [<Fact>]
    let ``an empty npm project is not installed`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        let state =
            repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState version

        Assert.Equal(NotInstalled, state)

    [<Fact>]
    let ``a configuration without a manifest is treated as a pre-manifest installation`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        repository.Write("research-publisher.config.mjs", "export default {};\n")

        let state =
            repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState version

        match state with
        | UpgradeRequired (current, target) ->
            Assert.Equal(None, current.ToolVersion)
            Assert.Equal(0, current.ConfigurationVersion)
            Assert.Equal(Identity.CurrentConfigurationVersion, target.ConfigurationVersion)
        | other -> failwithf "Expected an upgrade to be required, got %A" other

    [<Fact>]
    let ``a current installation reports installed`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore

        let state =
            repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState version

        match state with
        | Installed installed ->
            Assert.Equal(Some version, installed.ToolVersion)
            Assert.Equal(Identity.CurrentConfigurationVersion, installed.ConfigurationVersion)
        | other -> failwithf "Expected an installed state, got %A" other

    [<Fact>]
    let ``a newer CLI version makes an upgrade available`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize "1.0.0" repository.Root |> ignore

        let state =
            repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState "1.1.0"

        match state with
        | UpgradeRequired (current, target) ->
            Assert.Equal(Some "1.0.0", current.ToolVersion)
            Assert.Equal("1.1.0", target.ToolVersion)
        | other -> failwithf "Expected an upgrade to be available, got %A" other

    [<Fact>]
    let ``a missing required file makes the installation invalid`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore
        repository.Delete "prompts/research-publisher-mark-documents.md"

        let state =
            repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState version

        match state with
        | Invalid problems ->
            Assert.NotEmpty problems
            Assert.Equal("required-artifact-missing", problems.Head.Code)
        | other -> failwithf "Expected an invalid installation, got %A" other

    [<Fact>]
    let ``an unreadable manifest makes the installation invalid`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore
        repository.Write(Identity.ManifestPath, "{ not json")

        let state =
            repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState version

        match state with
        | Invalid problems -> Assert.Equal("manifest-unreadable", problems.Head.Code)
        | other -> failwithf "Expected an invalid installation, got %A" other

    [<Fact>]
    let ``a configuration version from the future is refused rather than downgraded`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore

        let manifest =
            repository
                .Read(Identity.ManifestPath)
                .Replace("\"configurationVersion\": 2", "\"configurationVersion\": 99")

        repository.Write(Identity.ManifestPath, manifest)

        let state =
            repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState version

        match state with
        | Invalid problems -> Assert.Equal("configuration-version-too-new", problems.Head.Code)
        | other -> failwithf "Expected the installation to be refused, got %A" other

    [<Fact>]
    let ``inspection never writes to the repository`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        repository.Write("research-publisher.config.mjs", "export default {};\n")
        let before = repository.Snapshot()

        repository.Root |> Inspection.inspectRepository |> Inspection.getInstallationState version |> ignore

        Assert.Equal(before, repository.Snapshot())
