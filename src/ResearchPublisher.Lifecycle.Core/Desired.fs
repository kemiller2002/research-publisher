namespace ResearchPublisher.Lifecycle.Core

open System
open System.Text
open System.Text.RegularExpressions

/// Facts read out of the consuming repository's package.json that the templates need.
type ConsumerPackage =
    { Name: string
      RepositoryUrl: string
      Scripts: Map<string, string>
      DeclaresPublisherDependency: bool }

/// The complete set of managed paths and scripts for one configuration version.
type DesiredState =
    { ConfigurationVersion: int
      Artifacts: ManagedArtifact list
      Scripts: ManagedScript list }

/// What a correct installation looks like, and the content the tool writes for it.
module Desired =

    [<Literal>]
    let ConfigArtifactId = "config"

    [<Literal>]
    let MarkingPromptArtifactId = "marking-prompt"

    [<Literal>]
    let ManifestArtifactId = "manifest"

    [<Literal>]
    let ConfigPath = "research-publisher.config.mjs"

    [<Literal>]
    let MarkingPromptPath = "prompts/research-publisher-mark-documents.md"

    /// The marking prompt as it ships inside the npm package.
    [<Literal>]
    let MarkingPromptTemplateAsset = "prompts/mark-research-documents.md"

    /// Paths the build engine produces. They are never created or removed by the
    /// lifecycle commands; classifying them keeps `verify` and `doctor` from
    /// treating a clean checkout as damaged.
    let generatedArtifacts =
        [ { Id = "site-output"
            Path = "dist"
            Ownership = Generated
            Required = false
            Description = "Rendered static site produced by `research-publisher build`." }
          { Id = "engine-cache"
            Path = ".research-publisher"
            Ownership = Generated
            Required = false
            Description = "Intermediate build data produced by the publishing engine." }
          { Id = "build-reports"
            Path = "build-reports"
            Ownership = Generated
            Required = false
            Description = "Diagnostics written during validate and build runs." } ]

    let private installedArtifacts =
        [ { Id = ConfigArtifactId
            Path = ConfigPath
            Ownership = UserOwned
            Required = true
            Description = "Repository publishing configuration. Created once, then owned by the repository." }
          { Id = MarkingPromptArtifactId
            Path = MarkingPromptPath
            Ownership = Shared
            Required = true
            Description = "Corpus-classification prompt supplied by the tool and editable by the repository." }
          { Id = ManifestArtifactId
            Path = Identity.ManifestPath
            Ownership = ToolOwned
            Required = true
            Description = "Installation record for this tool." } ]

    /// Scripts introduced by configuration version 1 (the original installation shape).
    let private engineScripts: ManagedScript list =
        [ { Name = "research:inventory"
            Command = "research-publisher inventory --config ./research-publisher.config.mjs" }
          { Name = "research:validate"
            Command = "research-publisher validate --config ./research-publisher.config.mjs" }
          { Name = "research:build"
            Command = "research-publisher build --config ./research-publisher.config.mjs" }
          { Name = "research:clean"
            Command = "research-publisher clean --config ./research-publisher.config.mjs" } ]

    /// Scripts introduced by configuration version 2 (the lifecycle interface).
    let private lifecycleScripts: ManagedScript list =
        [ { Name = "research:status"; Command = "research-publisher status" }
          { Name = "research:verify"; Command = "research-publisher verify" }
          { Name = "research:doctor"; Command = "research-publisher doctor" } ]

    /// Desired state for a given configuration version. Unknown versions fall back
    /// to the newest known shape.
    let forConfigurationVersion version =
        if version <= 1 then
            { ConfigurationVersion = 1
              Artifacts = installedArtifacts
              Scripts = engineScripts }
        else
            { ConfigurationVersion = Identity.CurrentConfigurationVersion
              Artifacts = installedArtifacts
              Scripts = engineScripts @ lifecycleScripts }

    let current = forConfigurationVersion Identity.CurrentConfigurationVersion

    let allKnownArtifacts = installedArtifacts @ generatedArtifacts

    // ---------------------------------------------------------------- templates

    let private escapeJsonString (value: string) =
        let builder = StringBuilder()
        builder.Append('"') |> ignore

        for character in value do
            match character with
            | '"' -> builder.Append("\\\"") |> ignore
            | '\\' -> builder.Append("\\\\") |> ignore
            | '\n' -> builder.Append("\\n") |> ignore
            | '\r' -> builder.Append("\\r") |> ignore
            | '\t' -> builder.Append("\\t") |> ignore
            | c when c < ' ' -> builder.AppendFormat("\\u{0:x4}", int c) |> ignore
            | c -> builder.Append(c) |> ignore

        builder.Append('"') |> ignore
        builder.ToString()

    /// Turn an npm package name into a human site title, matching the behaviour of
    /// the original JavaScript initializer.
    let titleFromName (name: string) =
        let source = if String.IsNullOrWhiteSpace name then "Research Repository" else name
        let withoutScope = Regex.Replace(source, "^@[^/]+/", "")
        let spaced = Regex.Replace(withoutScope, "[-_]+", " ")
        Regex.Replace(spaced, "\\b\\w", fun m -> m.Value.ToUpperInvariant())

    let repositoryUrl (raw: string) =
        if String.IsNullOrWhiteSpace raw then
            ""
        else
            let withoutPrefix = Regex.Replace(raw, "^git\\+", "")
            Regex.Replace(withoutPrefix, "\\.git$", "")

    /// The configuration file written by `init` when the repository has none.
    /// Once written it is user-owned and is never rewritten by the tool.
    let configTemplate (consumer: ConsumerPackage) =
        let name = consumer.Name
        let title = titleFromName name
        let sourceUrl = repositoryUrl consumer.RepositoryUrl

        String.concat
            "\n"
            [ "export default {"
              "  site: {"
              sprintf "    title: %s," (escapeJsonString title)
              "    description: \"Searchable research repository\","
              "    // Use \"/repository-name/\" for a GitHub Pages project site."
              "    baseUrl: \"/\","
              "    language: \"en\","
              "    siteUrl: \"https://example.com/\""
              "  },"
              "  repository: {"
              sprintf "    name: %s," (escapeJsonString name)
              sprintf "    sourceUrl: %s" (escapeJsonString sourceUrl)
              "  },"
              "  content: {"
              "    // Broad discovery keeps future research folders visible without config churn."
              "    include: [\"**/*.md\"],"
              "    exclude: ["
              "      \"README.md\","
              "      \"CHANGELOG.md\","
              "      \"CONTRIBUTING.md\","
              "      \"node_modules/**\","
              "      \"dist/**\","
              "      \".git/**\","
              "      \".github/**\","
              "      \".echelon/**\","
              "      \".research-publisher/**\","
              "      \"build-reports/**\","
              "      \"prompts/**\","
              "      \"coverage/**\","
              "      \"tmp/**\","
              "      \"temp/**\","
              "      \"**/archive/**\","
              "      \"**/archives/**\""
              "    ],"
              "    drafts: false"
              "  },"
              "  metadata: {"
              "    mode: \"compatible\","
              "    strictInCI: true,"
              "    required: [\"title\"],"
              "    stableIdPrefixes: [\"RP\", \"JR\", \"EV\", \"HY\", \"TH\", \"EX\", \"DF\", \"CN\", \"GL\"]"
              "  },"
              "  output: {"
              "    directory: \"dist\","
              "    catalog: \"data/research-catalog.json\","
              "    diagnostics: \"data/build-diagnostics.json\""
              "  },"
              "  branding: {"
              "    // Package defaults provide a unified design. Override only the semantic"
              "    // color roles this repository needs; see the Research Publisher README."
              "    cssVariables: {"
              "      // \"--color-accent\": \"#2457a6\","
              "      // \"--color-accent-strong\": \"#173b73\","
              "      // \"--color-accent-soft\": \"#dce8fa\""
              "    }"
              "  }"
              "};"
              "" ]

    /// The marking prompt template, read from the packaged runtime assets.
    let tryMarkingPromptTemplate () = PackageRoot.tryReadAsset MarkingPromptTemplateAsset
