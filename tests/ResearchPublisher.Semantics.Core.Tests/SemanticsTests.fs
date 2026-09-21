namespace ResearchPublisher.Semantics.Core.Tests

open System.Text.Json.Nodes
open Xunit
open ResearchPublisher.Semantics.Core
open ResearchPublisher.Semantics.Core.Compiler

module SemanticsTests =
    let private fm pairs =
        let o=JsonObject()
        for k,v in pairs do o[k]<-JsonValue.Create(v)
        o
    let private fmArray key values =
        let o=JsonObject()
        let a=JsonArray()
        for v in values do a.Add(JsonValue.Create(v))
        o[key]<-a; o
    let private raw path frontMatter =
        { SourcePath=path; FrontMatter=frontMatter; Excerpt=None; Headings=[{Depth=1;Text="Heading title"}] }

    [<Fact>]
    let snake_case_metadata_has_no_fabricated_defaults () =
        let result=Compiler.compile[raw "research/packages/a.md" (fm["id","RP-COMP-005";"document_type","research-execution-package";"research_area","Composition";"author_agent","agent-a"])]
        let a=Assert.Single(result.Artifacts)
        Assert.Equal(Some "research-execution-package",a.ArtifactType)
        Assert.Equal(Some "Composition",a.ResearchArea)
        Assert.Equal(Some "agent-a",a.AuthorAgent)
        Assert.Equal(None,a.Created); Assert.Equal(None,a.Updated); Assert.Equal(None,a.Status)
        Assert.DoesNotContain("2026-07-22",SemanticJson.toJson result)
        Assert.DoesNotContain("General Research",SemanticJson.toJson result)

    [<Fact>]
    let mixed_related_documents_are_classified_and_resolved () =
        let first=fmArray "related_documents" ["../b.md";"EV-001";"Product Genome: framework"]
        first["id"]<-JsonValue.Create("RP-001")
        let result=Compiler.compile[raw "research/a/a.md" first;raw "research/b.md"(fm["id","DOC-B"]);raw "research/evidence/e.md"(fm["id","EV-001"])]
        let rs=result.Relationships|>List.filter(fun r->r.SourceKey="RP-001")
        Assert.Equal(3,rs.Length)
        Assert.Contains(rs,fun r->r.RawTarget="../b.md" && r.Resolution=Resolved)
        Assert.Contains(rs,fun r->r.RawTarget="EV-001" && r.Resolution=Resolved)
        Assert.Contains(rs,fun r->r.RawTarget="Product Genome: framework" && r.Resolution=NotALink)

    [<Fact>]
    let bibliography_is_not_relationship_data () =
        let x=fmArray "references" ["Shannon, C. E. (1948)"]; x["id"]<-JsonValue.Create("RP-001")
        let result=Compiler.compile[raw "research/a.md" x]
        Assert.Empty(result.Relationships)
        Assert.Equal<string list>(["Shannon, C. E. (1948)"],Assert.Single(result.Artifacts).Bibliography)

    [<Fact>]
    let unknown_front_matter_is_retained () =
        let result=Compiler.compile[raw "research/a.md"(fm["id","RP-001";"future_field","future-value"])]
        let a=Assert.Single(result.Artifacts)
        Assert.True(a.UnknownFrontMatter.ContainsKey("future_field"))
        Assert.Equal("future-value",a.UnknownFrontMatter["future_field"].GetValue<string>())

    [<Fact>]
    let dangling_relation_is_visible_as_warning () =
        let x=fmArray "relatedDocuments" ["DF-MISSING-001"]; x["id"]<-JsonValue.Create("RP-001")
        let result=Compiler.compile[raw "research/a.md" x]
        Assert.Equal(Dangling,Assert.Single(result.Relationships).Resolution)
        Assert.Contains(result.Findings,fun f->f.Code="dangling-reference" && f.Severity=Warning)

    [<Fact>]
    let evidence_ids_are_declared_unclassified () =
        let x=fmArray "evidenceIds" ["EV-001"]; x["id"]<-JsonValue.Create("RP-001")
        let result=Compiler.compile[raw "research/a.md" x;raw "research/e.md"(fm["id","EV-001"])]
        let r=Assert.Single(result.Relationships)
        Assert.Equal(DeclaredUnclassified,r.Authority); Assert.Equal(Resolved,r.Resolution)

    [<Fact>]
    let missing_id_uses_path_identity_without_inventing_id () =
        let result=Compiler.compile[raw "research/no-id.md"(JsonObject())]
        let a=Assert.Single(result.Artifacts)
        Assert.Equal(None,a.DeclaredId); Assert.StartsWith("path:",a.Key); Assert.StartsWith("/s/",a.CanonicalUrl)
        Assert.Contains(result.Findings,fun f->f.Code="missing-id")

    [<Fact>]
    let duplicate_ids_block_publication () =
        let result=Compiler.compile[raw "research/a.md"(fm["id","RP-001"]);raw "research/b.md"(fm["id","RP-001"])]
        Assert.Contains(result.Findings,fun f->f.Code="duplicate-id" && f.Severity=Blocking)
