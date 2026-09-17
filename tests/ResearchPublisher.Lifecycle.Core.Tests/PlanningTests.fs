namespace ResearchPublisher.Lifecycle.Core.Tests

open Xunit
open ResearchPublisher.Lifecycle.Core

module PlanningTests =

    let private version = "1.0.0"

    let private planInit (repository: TestRepository) =
        TestPackage.useCheckout ()
        repository.Root |> Api.inspectRepository |> Api.createInitializationPlan version

    [<Fact>]
    let ``a fresh install plans every managed path and script`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        let plan = planInit repository
        let targets = plan.Steps |> List.map (fun step -> PlannedChange.target step.Change)

        Assert.Contains("research-publisher.config.mjs", targets)
        Assert.Contains("prompts/research-publisher-mark-documents.md", targets)
        Assert.Contains(Identity.ManifestPath, targets)
        Assert.Contains("package.json#scripts.research:build", targets)
        Assert.Contains("package.json#scripts.research:verify", targets)
        Assert.True(Plan.isExecutable plan)

    [<Fact>]
    let ``planning writes nothing`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        let before = repository.Snapshot()

        planInit repository |> ignore

        Assert.Equal(before, repository.Snapshot())

    [<Fact>]
    let ``init is idempotent: the second run plans no changes`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        let first = Api.apply (planInit repository)
        Assert.True first.Succeeded

        let second = planInit repository
        Assert.Empty second.Steps
        Assert.False(Plan.hasChanges second)

    [<Fact>]
    let ``init is idempotent: the repository is byte identical after a second run`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        Api.apply (planInit repository) |> ignore
        let after = repository.Snapshot()

        Api.apply (planInit repository) |> ignore

        Assert.Equal(after, repository.Snapshot())

    [<Fact>]
    let ``a user owned configuration is never rewritten`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        repository.Write("research-publisher.config.mjs", "export default { mine: true };\n")

        let plan = planInit repository

        Assert.DoesNotContain(
            "research-publisher.config.mjs",
            plan.Steps |> List.map (fun step -> PlannedChange.target step.Change)
        )

        Api.apply plan |> ignore
        Assert.Equal("export default { mine: true };\n", repository.Read "research-publisher.config.mjs")

    [<Fact>]
    let ``an existing script keeps its own command`` () =
        use repository = new TestRepository()

        repository.WritePackageJson """{
  "name": "example-research",
  "scripts": { "research:build": "my-own-build" }
}
"""

        Api.apply (planInit repository) |> ignore

        let packageJson = repository.Read "package.json"
        Assert.Contains("\"research:build\": \"my-own-build\"", packageJson)
        Assert.Contains("\"research:verify\"", packageJson)

    /// Written with explicit line endings so the assertions do not depend on how
    /// the repository was checked out.
    let private fourSpacePackageJson (lineEnding: string) =
        [ "{"
          "    \"name\": \"example-research\","
          "    \"version\": \"2.0.0\","
          "    \"private\": true,"
          "    \"scripts\": {"
          "        \"test\": \"vitest\""
          "    }"
          "}"
          "" ]
        |> String.concat lineEnding

    [<Fact>]
    let ``package json key order and indentation survive`` () =
        use repository = new TestRepository()
        repository.WritePackageJson(fourSpacePackageJson "\n")

        Api.apply (planInit repository) |> ignore
        let packageJson = repository.Read "package.json"

        Assert.StartsWith("{\n    \"name\": \"example-research\",\n    \"version\": \"2.0.0\",", packageJson)
        Assert.Contains("        \"test\": \"vitest\",", packageJson)

    [<Fact>]
    let ``package json line endings survive`` () =
        // Rewriting a file must not convert it wholesale, which is what
        // Utf8JsonWriter would do on Windows if its output were used verbatim.
        for lineEnding in [ "\n"; "\r\n" ] do
            use repository = new TestRepository()
            repository.WritePackageJson(fourSpacePackageJson lineEnding)

            Api.apply (planInit repository) |> ignore
            let packageJson = repository.Read "package.json"

            Assert.Contains("\"research:verify\"", packageJson)
            Assert.Equal(lineEnding, if packageJson.Contains "\r\n" then "\r\n" else "\n")

    [<Fact>]
    let ``the installation manifest is written with canonical line endings`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.apply (planInit repository) |> ignore

        Assert.DoesNotContain("\r\n", repository.Read Identity.ManifestPath)

    [<Fact>]
    let ``init without package json is blocked and changes nothing`` () =
        use repository = new TestRepository()
        let before = repository.Snapshot()

        let plan = planInit repository
        Assert.False(Plan.isExecutable plan)
        Assert.Equal("package-json-required", plan.Blockers.Head.Code)

        let result = Api.apply plan
        Assert.False result.Succeeded
        Assert.Equal(before, repository.Snapshot())
        Assert.All(result.Changes, fun change ->
            match change.Outcome with
            | NotAttempted _ -> ()
            | other -> failwithf "Expected nothing to be attempted, got %A" other)

    [<Fact>]
    let ``a damaged installation is repaired by init`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.apply (planInit repository) |> ignore
        repository.Delete "prompts/research-publisher-mark-documents.md"

        let result = Api.apply (planInit repository)

        Assert.True result.Succeeded
        Assert.True(repository.Exists "prompts/research-publisher-mark-documents.md")

    [<Fact>]
    let ``the manifest records ownership for every managed path`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.apply (planInit repository) |> ignore

        match Manifest.parse (repository.Read Identity.ManifestPath) with
        | Result.Error problem -> failwith problem.Detail
        | Ok manifest ->
            let ownership id =
                manifest |> Manifest.tryFindArtifact id |> Option.map (fun artifact -> artifact.Ownership)

            Assert.Equal(Some UserOwned, ownership "config")
            Assert.Equal(Some Shared, ownership "marking-prompt")
            Assert.Equal(Some ToolOwned, ownership "manifest")
            Assert.Equal(Some Generated, ownership "site-output")

    [<Fact>]
    let ``a hash is recorded for shared files and withheld for user owned files`` () =
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.apply (planInit repository) |> ignore

        match Manifest.parse (repository.Read Identity.ManifestPath) with
        | Result.Error problem -> failwith problem.Detail
        | Ok manifest ->
            Assert.True((Manifest.tryFindArtifact "marking-prompt" manifest).Value.Hash.IsSome)
            Assert.True((Manifest.tryFindArtifact "config" manifest).Value.Hash.IsNone)

    [<Fact>]
    let ``upgrade refuses to create an installation`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()

        let plan = repository.Root |> Api.inspectRepository |> Api.planUpgrade version

        Assert.False(Plan.isExecutable plan)
        Assert.Equal("not-installed", plan.Blockers.Head.Code)
