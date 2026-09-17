namespace ResearchPublisher.Lifecycle.Cli.Tests

open System.Text.Json
open Xunit
open ResearchPublisher.Lifecycle.Core
open ResearchPublisher.Lifecycle.Cli

/// The JSON interface is consumed by CI and by agents, so its shape is pinned.
module JsonOutputTests =

    let private parse (text: string) =
        let document = JsonDocument.Parse text
        document.RootElement.Clone()

    let private schemaOf text =
        parse text |> Json.tryStringProperty "schema"

    let private emptyVerification: VerificationReport =
        { Strict = false
          Checks =
            [ { Id = "manifest"
                Title = "Installation manifest"
                Status = Pass
                Detail = "Recorded."
                Path = Some Identity.ManifestPath
                Remediation = None } ]
          Passed = true }

    let private summary: StatusSummary =
        { Tool = Identity.ToolName
          Package = Identity.PackageName
          CliVersion = "1.2.3"
          RepositoryRoot = "/tmp/repo"
          State = Installed { ToolVersion = Some "1.2.3"; ConfigurationVersion = 2 }
          InstalledVersion = Some "1.2.3"
          ConfigurationVersion = Some 2
          ConfigurationStatus = "valid"
          ArtifactStatus = "valid"
          IntegrationStatus = "valid"
          Verification = emptyVerification
          UpgradeAvailable = None }

    let private samplePlan: Plan =
        { Operation = Initialize
          RepositoryRoot = "/tmp/repo"
          FromState = NotInstalled
          Target = { ToolVersion = "1.2.3"; ConfigurationVersion = 2 }
          Migrations = [ { FromVersion = 0; ToVersion = 1 } ]
          Steps =
            [ { Change = CreateFile("research-publisher.config.mjs", UserOwned, "export default {};")
                Reason = "No configuration exists." } ]
          Skipped = [ { Target = "package.json"; Reason = "Already correct." } ]
          Conflicts =
            [ { Target = "prompts/research-publisher-mark-documents.md"
                Ownership = Shared
                Detail = "Edited locally."
                Resolution = "Keep the local version." } ]
          Blockers = [] }

    [<Fact>]
    let ``status output is valid json carrying its schema`` () =
        let text = JsonOutput.status summary 0
        Assert.Equal(Some JsonOutput.StatusSchema, schemaOf text)

    [<Fact>]
    let ``status reports everything the documented report promises`` () =
        let root = parse (JsonOutput.status summary 0)

        for field in
            [ "tool"
              "package"
              "cliVersion"
              "repositoryRoot"
              "state"
              "installedVersion"
              "configurationVersion"
              "configuration"
              "artifacts"
              "integration"
              "verification"
              "upgradeAvailable"
              "manifestPath"
              "exitCode" ] do
            Assert.True((Json.tryProperty field root).IsSome, field)

    [<Fact>]
    let ``verification output is valid json`` () =
        let text = JsonOutput.verification "1.2.3" "/tmp/repo" emptyVerification 0
        Assert.Equal(Some JsonOutput.VerifySchema, schemaOf text)
        Assert.Equal(1, (Json.tryProperty "checks" (parse text) |> Option.get |> Json.arrayItems).Length)

    [<Fact>]
    let ``doctor output is valid json with severity counts`` () =
        let report =
            { Diagnostics =
                [ Problem.create "a" Error "An error" "detail"
                  Problem.create "b" Warning "A warning" "detail" ]
              Healthy = false }

        let text = JsonOutput.diagnostics "1.2.3" "/tmp/repo" false report 6
        Assert.Equal(Some JsonOutput.DoctorSchema, schemaOf text)

        let counts = Json.tryProperty "counts" (parse text) |> Option.get
        Assert.Equal(Some 1, Json.tryIntProperty "error" counts)
        Assert.Equal(Some 1, Json.tryIntProperty "warning" counts)

    [<Fact>]
    let ``plan output reports changes, conflicts and skipped paths`` () =
        let text = JsonOutput.plan "1.2.3" true samplePlan 0
        let root = parse text

        Assert.Equal(Some JsonOutput.PlanSchema, schemaOf text)
        Assert.Equal(Some 1, Json.tryIntProperty "changeCount" root)
        Assert.Equal(1, (Json.tryProperty "conflicts" root |> Option.get |> Json.arrayItems).Length)
        Assert.Equal(1, (Json.tryProperty "skipped" root |> Option.get |> Json.arrayItems).Length)

        let change = (Json.tryProperty "changes" root |> Option.get |> Json.arrayItems).Head
        Assert.Equal(Some "create-file", Json.tryStringProperty "kind" change)
        Assert.Equal(Some "user-owned", Json.tryStringProperty "ownership" change)

    [<Fact>]
    let ``execution output reports the outcome of each change`` () =
        let result =
            { Plan = samplePlan
              Changes = [ { Change = samplePlan.Steps.Head.Change; Outcome = Applied } ]
              Succeeded = true
              Failures = [] }

        let text = JsonOutput.executionResult "1.2.3" result 0
        let root = parse text

        Assert.Equal(Some JsonOutput.ResultSchema, schemaOf text)
        let applied = (Json.tryProperty "applied" root |> Option.get |> Json.arrayItems).Head
        Assert.Equal(Some "applied", Json.tryStringProperty "outcome" applied)

    [<Fact>]
    let ``error output is valid json`` () =
        let text = JsonOutput.failure "1.2.3" "usage" "Unknown option --wat." 2
        Assert.Equal(Some JsonOutput.ErrorSchema, schemaOf text)
        Assert.Equal(Some 2, Json.tryIntProperty "exitCode" (parse text))

    [<Fact>]
    let ``no json document carries decorative text`` () =
        let documents =
            [ JsonOutput.status summary 0
              JsonOutput.verification "1.2.3" "/tmp/repo" emptyVerification 0
              JsonOutput.plan "1.2.3" true samplePlan 0
              JsonOutput.failure "1.2.3" "usage" "message" 2 ]

        for text in documents do
            Assert.StartsWith("{", text.TrimStart())
            Assert.EndsWith("}", text.TrimEnd())
