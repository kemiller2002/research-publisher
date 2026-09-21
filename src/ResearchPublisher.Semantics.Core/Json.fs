namespace ResearchPublisher.Semantics.Core

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

module SemanticJson =
    let private s value = JsonValue.Create(value) :> JsonNode
    let private b value = JsonValue.Create(value) :> JsonNode
    let private n value = JsonValue.Create(value) :> JsonNode
    let private os = function Some v -> s v | None -> null
    let private on = function Some v -> n v | None -> null
    let private sa values = let a=JsonArray() in for v in values do a.Add(s v); a
    let private asInt fallback node = match node with :? JsonValue as v -> try v.GetValue<int>() with _ -> fallback | _ -> fallback
    let private asString fallback node = match node with :? JsonValue as v -> try v.GetValue<string>() with _ -> fallback | _ -> fallback

    let private rawHeading node : Compiler.RawHeading option =
        match node with :? JsonObject as o -> Some {Depth=asInt 0 o["depth"];Text=asString "" o["text"]} | _ -> None

    let private rawDocument node : Compiler.RawDocument option =
        match node with
        | :? JsonObject as o ->
            let path=asString "" o["sourcePath"]
            if String.IsNullOrWhiteSpace path then None else
            let fm=match o["frontmatter"] with :? JsonObject as v -> v.DeepClone() :?> JsonObject | _ -> JsonObject()
            let e=asString "" o["excerpt"]
            let hs=match o["headings"] with :? JsonArray as a -> a|>Seq.choose rawHeading|>Seq.toList | _ -> []
            Some {SourcePath=path;FrontMatter=fm;Excerpt=if String.IsNullOrWhiteSpace e then None else Some e;Headings=hs}
        | _ -> None

    let private finding (f:Finding) =
        let o=JsonObject()
        o["code"]<-s f.Code; o["severity"]<-s(Severity.toWire f.Severity); o["sourcePath"]<-s f.SourcePath
        o["frontMatterKey"]<-os f.FrontMatterKey; o["message"]<-s f.Message; o["remedy"]<-os f.Remedy; o

    let private relation (r:Relationship) =
        let o=JsonObject()
        o["sourceKey"]<-s r.SourceKey; o["sourcePath"]<-s r.SourcePath; o["field"]<-s r.Field; o["relation"]<-s r.Relation
        o["authority"]<-s(RelationAuthority.toWire r.Authority); o["rawTarget"]<-s r.RawTarget; o["referenceKind"]<-s(ReferenceKind.toWire r.ReferenceKind)
        o["resolution"]<-s(ResolutionStatus.toWire r.Resolution); o["targetKey"]<-os r.TargetKey; o["targetId"]<-os r.TargetId
        o["targetSourcePath"]<-os r.TargetSourcePath; o["targetTitle"]<-os r.TargetTitle; o

    let private artifact (rels:Relationship list) (a:Artifact) =
        let o=JsonObject()
        o["key"]<-s a.Key; o["keyKind"]<-s a.KeyKind; o["id"]<-os a.DeclaredId; o["title"]<-s a.Title; o["titleSource"]<-s a.TitleSource
        o["artifactType"]<-os a.ArtifactType; o["typeSource"]<-s a.TypeSource; o["project"]<-os a.Project; o["purposes"]<-sa a.Purposes
        o["audiences"]<-sa a.Audiences; o["entryPoint"]<-b a.EntryPoint; o["entryPointOrder"]<-on a.EntryPointOrder; o["entryPointLabel"]<-os a.EntryPointLabel
        o["researchArea"]<-os a.ResearchArea; o["discipline"]<-sa a.Discipline; o["summary"]<-os a.Summary; o["status"]<-os a.Status
        o["version"]<-os a.Version; o["confidence"]<-on a.Confidence; o["completion"]<-on a.Completion; o["priority"]<-os a.Priority
        o["authorAgent"]<-os a.AuthorAgent; o["created"]<-os a.Created; o["updated"]<-os a.Updated; o["tags"]<-sa a.Tags; o["keywords"]<-sa a.Keywords
        o["relatedProjects"]<-sa a.RelatedProjects; o["bibliography"]<-sa a.Bibliography; o["sourcePath"]<-s a.SourcePath; o["url"]<-s a.CanonicalUrl
        o["legacyUrls"]<-sa a.LegacyUrls; o["rawFrontmatter"]<-a.FrontMatter.DeepClone(); o["unknownFrontmatter"]<-a.UnknownFrontMatter.DeepClone()
        let ra=JsonArray()
        for r in rels|>List.filter(fun r->r.SourceKey=a.Key) do ra.Add(relation r)
        o["relationships"]<-ra; o

    let toJson (c:Compilation) =
        let root=JsonObject()
        root["schemaVersion"]<-s "2.0"
        let aa=JsonArray(); for a in c.Artifacts do aa.Add(artifact c.Relationships a); root["artifacts"]<-aa
        let rr=JsonArray(); for r in c.Relationships do rr.Add(relation r); root["relationships"]<-rr
        let ff=JsonArray(); for f in c.Findings do ff.Add(finding f); root["findings"]<-ff
        let cc=JsonArray()
        for c in c.Capabilities do let o=JsonObject() in o["name"]<-s c.Name; o["available"]<-b c.Available; o["reason"]<-s c.Reason; cc.Add o
        root["capabilities"]<-cc
        let red=JsonObject(); for oldUrl,newUrl in c.Redirects do red[oldUrl]<-s newUrl; root["redirects"]<-red
        root.ToJsonString(JsonSerializerOptions(WriteIndented=true))

    let compileRepository repositoryRoot =
        let dir=Path.Combine(repositoryRoot,".research-publisher")
        let input=Path.Combine(dir,"semantic-input.json")
        let output=Path.Combine(dir,"semantic-output.json")
        if not(File.Exists input) then invalidOp(sprintf "Semantic compiler input is missing: %s" input)
        let parsed=JsonNode.Parse(File.ReadAllText input)
        let root=match parsed with :? JsonObject as o -> o | _ -> invalidOp "Semantic compiler input root must be a JSON object."
        let docs=match root["documents"] with :? JsonArray as a -> a|>Seq.choose rawDocument|>Seq.toList | _ -> []
        let compiled=Compiler.compile docs
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(output,toJson compiled)
        compiled
