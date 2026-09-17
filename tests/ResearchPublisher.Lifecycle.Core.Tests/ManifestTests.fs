namespace ResearchPublisher.Lifecycle.Core.Tests

open Xunit
open ResearchPublisher.Lifecycle.Core

module ManifestTests =

    let private sample =
        { Schema = Identity.ManifestSchema
          Tool = Identity.ToolName
          Package = Identity.PackageName
          InstalledVersion = "1.2.3"
          ConfigurationVersion = 2
          ManagedArtifacts =
            [ { Id = "config"
                Path = "research-publisher.config.mjs"
                Ownership = UserOwned
                Hash = None }
              { Id = "marking-prompt"
                Path = "prompts/research-publisher-mark-documents.md"
                Ownership = Shared
                Hash = Some "sha256:abc" } ]
          ManagedScripts = [ { Name = "research:build"; Command = "research-publisher build" } ] }

    [<Fact>]
    let ``round trips through render and parse`` () =
        match Manifest.parse (Manifest.render sample) with
        | Ok parsed -> Assert.Equal<Manifest>(sample, parsed)
        | Result.Error problem -> failwith problem.Detail

    [<Fact>]
    let ``rendering is stable, so rewriting an unchanged installation is a no-op`` () =
        Assert.Equal(Manifest.render sample, Manifest.render sample)

    [<Fact>]
    let ``contains no timestamps or machine specific values`` () =
        let rendered = Manifest.render sample
        Assert.DoesNotContain("\"timestamp\"", rendered)
        Assert.DoesNotContain("\"installedAt\"", rendered)
        Assert.DoesNotContain("\"machine\"", rendered)
        Assert.DoesNotContain("\"user\"", rendered)

    [<Fact>]
    let ``rejects an unsupported schema`` () =
        let text = (Manifest.render sample).Replace(Identity.ManifestSchema, "echelon.tool-installation/99")

        match Manifest.parse text with
        | Ok _ -> failwith "Expected an unsupported schema to be rejected."
        | Result.Error problem -> Assert.Equal("manifest-schema-unsupported", problem.Code)

    [<Fact>]
    let ``rejects malformed json with an actionable problem`` () =
        match Manifest.parse "{ not json" with
        | Ok _ -> failwith "Expected malformed JSON to be rejected."
        | Result.Error problem ->
            Assert.Equal("manifest-unreadable", problem.Code)
            Assert.True(problem.Remediation.IsSome)

    [<Fact>]
    let ``rejects a manifest belonging to another tool`` () =
        let text = (Manifest.render sample).Replace("\"tool\": \"research-publisher\"", "\"tool\": \"sde\"")

        match Manifest.parse text with
        | Ok _ -> failwith "Expected another tool's manifest to be rejected."
        | Result.Error problem -> Assert.Equal("manifest-unreadable", problem.Code)
