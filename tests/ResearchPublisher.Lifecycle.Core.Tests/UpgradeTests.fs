namespace ResearchPublisher.Lifecycle.Core.Tests

open Xunit
open ResearchPublisher.Lifecycle.Core

/// Historical installation fixtures. Each one reproduces a shape a real repository
/// could be in, so the migration chain is exercised against the states it claims
/// to support rather than only against freshly created ones.
module Fixtures =

    /// Configuration version 0: created before installation manifests existed.
    let unmanaged (repository: TestRepository) =
        repository.WritePackageJson """{
  "name": "legacy-research",
  "private": true,
  "scripts": {
    "test": "vitest",
    "research:build": "custom-build-command",
    "research:inventory": "research-publisher inventory --config ./research-publisher.config.mjs",
    "research:validate": "research-publisher validate --config ./research-publisher.config.mjs",
    "research:clean": "research-publisher clean --config ./research-publisher.config.mjs"
  }
}
"""

        repository.Write("research-publisher.config.mjs", "export default { site: { title: 'Legacy' } };\n")
        repository.Write("prompts/research-publisher-mark-documents.md", "LEGACY PROMPT\n")

    /// Configuration version 1: manifest present, lifecycle scripts absent.
    let version1 (repository: TestRepository) (promptContents: string) (recordedContents: string) =
        repository.WritePackageJson """{
  "name": "v1-research",
  "private": true,
  "devDependencies": { "@echelon-foundry/research-publisher": "^0.1.0" },
  "scripts": {
    "research:inventory": "research-publisher inventory --config ./research-publisher.config.mjs",
    "research:validate": "research-publisher validate --config ./research-publisher.config.mjs",
    "research:build": "research-publisher build --config ./research-publisher.config.mjs",
    "research:clean": "research-publisher clean --config ./research-publisher.config.mjs"
  }
}
"""

        repository.Write("research-publisher.config.mjs", "export default { site: { title: 'V1' } };\n")
        repository.Write("prompts/research-publisher-mark-documents.md", promptContents)

        let manifest =
            { Schema = Identity.ManifestSchema
              Tool = Identity.ToolName
              Package = Identity.PackageName
              InstalledVersion = "0.0.9"
              ConfigurationVersion = 1
              ManagedArtifacts =
                [ { Id = "config"
                    Path = "research-publisher.config.mjs"
                    Ownership = UserOwned
                    Hash = None }
                  { Id = "marking-prompt"
                    Path = "prompts/research-publisher-mark-documents.md"
                    Ownership = Shared
                    Hash = Some(Hash.ofText recordedContents) }
                  { Id = "manifest"
                    Path = Identity.ManifestPath
                    Ownership = ToolOwned
                    Hash = None } ]
              ManagedScripts = [] }

        repository.Write(Identity.ManifestPath, Manifest.render manifest)

module UpgradeTests =

    let private version = "1.0.0"

    let private upgrade (repository: TestRepository) =
        TestPackage.useCheckout ()
        repository.Root |> Api.inspectRepository |> Api.planUpgrade version

    let private currentManifest (repository: TestRepository) =
        match Manifest.parse (repository.Read Identity.ManifestPath) with
        | Ok manifest -> manifest
        | Result.Error problem -> failwith problem.Detail

    [<Fact>]
    let ``an unmanaged installation migrates through every version in order`` () =
        use repository = new TestRepository()
        Fixtures.unmanaged repository

        let plan = upgrade repository

        Assert.Equal<MigrationId list>(
            [ { FromVersion = 0; ToVersion = 1 }; { FromVersion = 1; ToVersion = 2 } ],
            plan.Migrations
        )

        let result = Api.apply plan
        Assert.True result.Succeeded
        Assert.Equal(Identity.CurrentConfigurationVersion, (currentManifest repository).ConfigurationVersion)
        Assert.Equal(version, (currentManifest repository).InstalledVersion)

    [<Fact>]
    let ``upgrading an unmanaged installation preserves user owned content`` () =
        use repository = new TestRepository()
        Fixtures.unmanaged repository

        Api.apply (upgrade repository) |> ignore

        Assert.Equal("export default { site: { title: 'Legacy' } };\n", repository.Read "research-publisher.config.mjs")
        Assert.Contains("\"research:build\": \"custom-build-command\"", repository.Read "package.json")
        Assert.Contains("\"test\": \"vitest\"", repository.Read "package.json")

    [<Fact>]
    let ``upgrading from version 1 runs only the remaining migration`` () =
        use repository = new TestRepository()
        Fixtures.version1 repository "LEGACY PROMPT\n" "LEGACY PROMPT\n"

        let plan = upgrade repository

        Assert.Equal<MigrationId list>([ { FromVersion = 1; ToVersion = 2 } ], plan.Migrations)
        Assert.True (Api.apply plan).Succeeded

    [<Fact>]
    let ``an unmodified shared file is refreshed to the packaged version`` () =
        use repository = new TestRepository()
        Fixtures.version1 repository "LEGACY PROMPT\n" "LEGACY PROMPT\n"

        Api.apply (upgrade repository) |> ignore

        let packaged =
            match Desired.tryMarkingPromptTemplate () with
            | Ok template -> template
            | Result.Error message -> failwith message

        Assert.Equal(packaged, repository.Read "prompts/research-publisher-mark-documents.md")

    [<Fact>]
    let ``a locally modified shared file is reported as a conflict and left alone`` () =
        use repository = new TestRepository()
        Fixtures.version1 repository "MY LOCAL EDITS\n" "LEGACY PROMPT\n"

        let plan = upgrade repository

        Assert.Single plan.Conflicts |> ignore
        Assert.Equal("prompts/research-publisher-mark-documents.md", plan.Conflicts.Head.Target)
        Assert.Equal(Shared, plan.Conflicts.Head.Ownership)

        Assert.True (Api.apply plan).Succeeded
        Assert.Equal("MY LOCAL EDITS\n", repository.Read "prompts/research-publisher-mark-documents.md")

    [<Fact>]
    let ``a conflict keeps the recorded hash so a later release cannot overwrite the edit`` () =
        use repository = new TestRepository()
        Fixtures.version1 repository "MY LOCAL EDITS\n" "LEGACY PROMPT\n"

        Api.apply (upgrade repository) |> ignore

        let recorded =
            (currentManifest repository |> Manifest.tryFindArtifact "marking-prompt").Value.Hash

        Assert.Equal(Some(Hash.ofText "LEGACY PROMPT\n"), recorded)

    [<Fact>]
    let ``upgrading a current installation makes no changes`` () =
        TestPackage.useCheckout ()
        use repository = new TestRepository()
        repository.WriteMinimalPackageJson()
        Api.initialize version repository.Root |> ignore
        let before = repository.Snapshot()

        let plan = upgrade repository
        Assert.Empty plan.Steps

        Api.apply plan |> ignore
        Assert.Equal(before, repository.Snapshot())

    [<Fact>]
    let ``upgrade stops before writing when a migration precondition fails`` () =
        use repository = new TestRepository()
        Fixtures.unmanaged repository
        repository.Delete "package.json"
        let before = repository.Snapshot()

        let plan = upgrade repository

        Assert.False(Plan.isExecutable plan)
        Assert.Contains(plan.Blockers, fun blocker -> blocker.Code = "package-json-required")
        Api.apply plan |> ignore
        Assert.Equal(before, repository.Snapshot())

    [<Fact>]
    let ``upgrade refuses an installation it cannot understand`` () =
        use repository = new TestRepository()
        Fixtures.version1 repository "LEGACY PROMPT\n" "LEGACY PROMPT\n"
        repository.Write(Identity.ManifestPath, "{ not json")
        let before = repository.Snapshot()

        let plan = upgrade repository

        Assert.False(Plan.isExecutable plan)
        Api.apply plan |> ignore
        Assert.Equal(before, repository.Snapshot())

    [<Fact>]
    let ``upgrade repairs nothing: a missing required file sends the user to init`` () =
        use repository = new TestRepository()
        Fixtures.version1 repository "LEGACY PROMPT\n" "LEGACY PROMPT\n"
        repository.Delete "prompts/research-publisher-mark-documents.md"

        let plan = upgrade repository

        Assert.False(Plan.isExecutable plan)
        Assert.Contains(plan.Blockers, fun blocker -> blocker.Remediation.IsSome)

    [<Fact>]
    let ``every migration moves exactly one version forward`` () =
        Migrations.all
        |> List.iter (fun migration -> Assert.Equal(migration.Id.FromVersion + 1, migration.Id.ToVersion))

    [<Fact>]
    let ``the migration chain covers every version up to the current one`` () =
        let covered = Migrations.pathFrom Identity.LowestSupportedConfigurationVersion

        Assert.Equal(Identity.CurrentConfigurationVersion, List.length covered)
        Assert.Equal(Identity.LowestSupportedConfigurationVersion, covered.Head.Id.FromVersion)
        Assert.Equal(Identity.CurrentConfigurationVersion, (List.last covered).Id.ToVersion)
