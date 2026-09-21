namespace ResearchPublisher.Semantics.Core.Tests

open System.Text.Json.Nodes
open Xunit
open ResearchPublisher.Semantics.Core
open ResearchPublisher.Semantics.Core.Compiler

module SemanticsTests =

    let private fm (pairs: (string * string) list) =
        let obj = JsonObject()

        for key, value in pairs do
            obj[key] <- JsonValue.Create<string>(value)

        obj

    let private fmArray (key: string) (values: string list) =
        let obj = JsonObject()
        let array = JsonArray()

        for value in values do
            array.Add(JsonValue.Create<string>(value))

        obj[key] <- array
        obj

    let private raw
        (path: string)
        (frontMatter: JsonObject)
        : RawDocument =
        { SourcePath = path
          FrontMatter = frontMatter
          Excerpt = None
          Headings =
            [ { Depth = 1
                Text = "Heading title" } ] }

    [<Fact>]
    let snake_case_metadata_has_no_fabricated_defaults () =
        let frontMatter =
            fm
                [ "id", "RP-COMP-005"
                  "document_type", "research_execution_package"
                  "research_area", "Composition"
                  "author_agent", "agent-a" ]

        let result =
            Compiler.compile
                [ raw "research/packages/a.md" frontMatter ]

        let artifact =
            Assert.Single(result.Artifacts)

        Assert.Equal(Some "research-execution-package", artifact.ArtifactType)
        Assert.Equal(Some "Composition", artifact.ResearchArea)
        Assert.Equal(Some "agent-a", artifact.AuthorAgent)
        Assert.Equal<string option>(None, artifact.Created)
        Assert.Equal<string option>(None, artifact.Updated)
        Assert.Equal<string option>(None, artifact.Status)
        Assert.DoesNotContain("2026-07-22", SemanticJson.toJson result)
        Assert.DoesNotContain("General Research", SemanticJson.toJson result)

    [<Fact>]
    let mixed_related_documents_are_classified_and_resolved () =
        let first =
            fmArray
                "related_documents"
                [ "../b.md"
                  "EV-001"
                  "Product Genome: framework" ]

        first["id"] <-
            JsonValue.Create<string>("RP-001")

        let result =
            Compiler.compile
                [ raw "research/a/a.md" first
                  raw "research/b.md" (fm [ "id", "DOC-B" ])
                  raw "research/evidence/e.md" (fm [ "id", "EV-001" ]) ]

        let relationships =
            result.Relationships
            |> List.filter (fun relationship ->
                relationship.SourceKey = "RP-001")

        Assert.Equal(3, relationships.Length)

        Assert.Contains(
            relationships,
            fun relationship ->
                relationship.RawTarget = "../b.md"
                && relationship.Resolution = Resolved
        )

        Assert.Contains(
            relationships,
            fun relationship ->
                relationship.RawTarget = "EV-001"
                && relationship.Resolution = Resolved
        )

        Assert.Contains(
            relationships,
            fun relationship ->
                relationship.RawTarget = "Product Genome: framework"
                && relationship.Resolution = NotALink
        )

    [<Fact>]
    let bibliography_is_not_relationship_data () =
        let frontMatter =
            fmArray
                "references"
                [ "Shannon, C. E. (1948)" ]

        frontMatter["id"] <-
            JsonValue.Create<string>("RP-001")

        let result =
            Compiler.compile
                [ raw "research/a.md" frontMatter ]

        Assert.Empty(result.Relationships)

        let artifact =
            Assert.Single(result.Artifacts)

        Assert.Equal<string list>(
            [ "Shannon, C. E. (1948)" ],
            artifact.Bibliography
        )

    [<Fact>]
    let unknown_front_matter_is_retained () =
        let result =
            Compiler.compile
                [ raw
                    "research/a.md"
                    (fm
                        [ "id", "RP-001"
                          "future_field", "future-value" ]) ]

        let artifact =
            Assert.Single(result.Artifacts)

        Assert.True(
            artifact.UnknownFrontMatter.ContainsKey("future_field")
        )

        Assert.Equal(
            "future-value",
            artifact.UnknownFrontMatter["future_field"].GetValue<string>()
        )

    [<Fact>]
    let dangling_relation_is_visible_as_warning () =
        let frontMatter =
            fmArray
                "relatedDocuments"
                [ "DF-MISSING-001" ]

        frontMatter["id"] <-
            JsonValue.Create<string>("RP-001")

        let result =
            Compiler.compile
                [ raw "research/a.md" frontMatter ]

        let relationship =
            Assert.Single(result.Relationships)

        Assert.Equal(Dangling, relationship.Resolution)

        Assert.Contains(
            result.Findings,
            fun finding ->
                finding.Code = "dangling-reference"
                && finding.Severity = Warning
        )

    [<Fact>]
    let evidence_ids_are_declared_unclassified () =
        let frontMatter =
            fmArray "evidenceIds" [ "EV-001" ]

        frontMatter["id"] <-
            JsonValue.Create<string>("RP-001")

        let result =
            Compiler.compile
                [ raw "research/a.md" frontMatter
                  raw "research/e.md" (fm [ "id", "EV-001" ]) ]

        let relationship =
            Assert.Single(result.Relationships)

        Assert.Equal(
            DeclaredUnclassified,
            relationship.Authority
        )

        Assert.Equal(
            Resolved,
            relationship.Resolution
        )

    [<Fact>]
    let missing_id_uses_path_identity_without_inventing_id () =
        let result =
            Compiler.compile
                [ raw
                    "research/no-id.md"
                    (JsonObject()) ]

        let artifact =
            Assert.Single(result.Artifacts)

        Assert.Equal<string option>(
            None,
            artifact.DeclaredId
        )

        Assert.StartsWith("path:", artifact.Key)
        Assert.StartsWith("/s/", artifact.CanonicalUrl)

        Assert.Contains(
            result.Findings,
            fun finding ->
                finding.Code = "missing-id"
        )

    [<Fact>]
    let duplicate_ids_block_publication () =
        let result =
            Compiler.compile
                [ raw "research/a.md" (fm [ "id", "RP-001" ])
                  raw "research/b.md" (fm [ "id", "RP-001" ]) ]

        Assert.Contains(
            result.Findings,
            fun finding ->
                finding.Code = "duplicate-id"
                && finding.Severity = Blocking
        )


    [<Fact>]
    let snake_case_type_values_are_normalized_to_kebab_case () =
        let result =
            Compiler.compile
                [ raw
                    "research/report.md"
                    (fm
                        [ "id", "EX-COMP-011"
                          "document_type", "experiment_report" ]) ]

        let artifact = Assert.Single(result.Artifacts)
        Assert.Equal(Some "experiment-report", artifact.ArtifactType)
        Assert.Equal("front-matter:document_type", artifact.TypeSource)

    [<Fact>]
    let related_artifacts_are_preserved_without_inventing_relation_semantics () =
        let source =
            fmArray
                "related_artifacts"
                [ "TH-COMP-005"; "DF-COMP-002" ]

        source["id"] <- JsonValue.Create<string>("RP-COMP-005")

        let result =
            Compiler.compile
                [ raw "research/rp.md" source
                  raw "research/th.md" (fm [ "id", "TH-COMP-005" ])
                  raw "research/df.md" (fm [ "id", "DF-COMP-002" ]) ]

        let relationships =
            result.Relationships
            |> List.filter (fun relationship ->
                relationship.SourceKey = "RP-COMP-005")

        Assert.Equal(2, relationships.Length)

        Assert.All(
            relationships,
            fun relationship ->
                Assert.Equal(
                    DeclaredUnclassified,
                    relationship.Authority
                )

                Assert.Equal(
                    Resolved,
                    relationship.Resolution
                )

                Assert.Equal(
                    "related-artifact",
                    relationship.Relation
                )
        )

    [<Fact>]
    let prerequisite_alias_is_canonical_and_resolvable () =
        let source =
            fmArray
                "prerequisite"
                [ "RP-COMP-005" ]

        source["id"] <- JsonValue.Create<string>("EX-COMP-011")

        let result =
            Compiler.compile
                [ raw "research/ex.md" source
                  raw "research/rp.md" (fm [ "id", "RP-COMP-005" ]) ]

        let relationship = Assert.Single(result.Relationships)
        Assert.Equal(Canonical, relationship.Authority)
        Assert.Equal("prerequisite", relationship.Relation)
        Assert.Equal(Resolved, relationship.Resolution)
