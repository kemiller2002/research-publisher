namespace ResearchPublisher.Semantics.Core

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json.Nodes
open System.Text.RegularExpressions

module Compiler =

    type RawHeading = { Depth: int; Text: string }
    type RawDocument = { SourcePath: string; FrontMatter: JsonObject; Excerpt: string option; Headings: RawHeading list }

    let private knownKeys =
        set [ "id"; "identifier"; "stableId"; "title"; "slug"; "url"; "document_type"; "artifact_type"; "artifactType"
              "project"; "projectId"; "purposes"; "documentPurpose"; "purpose"; "audiences"; "audience"; "entryPoint"
              "entryPointOrder"; "entryPointLabel"; "research_area"; "researchArea"; "discipline"; "summary"; "abstract"
              "status"; "version"; "confidence"; "completion"; "priority"; "author"; "author_agent"; "authorAgent"; "date"
              "created"; "created_at"; "updated"; "updated_at"; "tags"; "keywords"; "related_projects"; "relatedProjects"
              "related_documents"; "relatedDocuments"; "source_rep"; "sourceRep"; "supersedes"; "superseded_by"
              "supersededBy"; "evidenceIds"; "evidence_ids"; "hypothesisIds"; "hypothesis_ids"; "theoryIds"; "theory_ids"
              "dependencies"; "references"; "evidence_level" ]

    let private idPattern = Regex("^[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)+$", RegexOptions.Compiled)
    let private normalizeSlashes (value: string) = value.Replace('\\', '/').Trim()

    let private collapsePath (value: string) =
        let stack = ResizeArray<string>()
        for segment in (normalizeSlashes value).TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries) do
            match segment with
            | "." -> ()
            | ".." when stack.Count > 0 -> stack.RemoveAt(stack.Count - 1)
            | ".." -> ()
            | other -> stack.Add other
        String.Join("/", stack)

    let private directoryOf (value: string) =
        let value = normalizeSlashes value
        let i = value.LastIndexOf('/')
        if i < 0 then "" else value.Substring(0, i)

    let private fileNameOf value =
        normalizeSlashes value
        |> fun p -> p.Split('/', StringSplitOptions.RemoveEmptyEntries)
        |> Array.tryLast
        |> Option.defaultValue value

    let private slugify (value: string) = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')

    let private pathKey sourcePath =
        sourcePath |> collapsePath |> Encoding.UTF8.GetBytes |> SHA256.HashData
        |> Convert.ToHexString |> fun h -> h.ToLowerInvariant().Substring(0, 16)

    let private tryNode (fm: JsonObject) names =
        names |> List.tryPick (fun name ->
            if fm.ContainsKey name && not (isNull fm[name]) then Some(name, fm[name]) else None)

    let private nodeToString (node: JsonNode) =
        match node with
        | :? JsonValue as value ->
            try Some(value.GetValue<string>().Trim())
            with _ ->
                let raw = value.ToJsonString()
                if raw.Length >= 2 && raw.StartsWith('"') && raw.EndsWith('"')
                then Some(raw.Substring(1, raw.Length - 2).Trim())
                else Some(raw.Trim())
        | _ -> None

    let private tryString fm names =
        tryNode fm names |> Option.bind (snd >> nodeToString) |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let private valuesOfNode node =
        let split (s: string) =
            s.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Seq.map _.Trim() |> Seq.filter (String.IsNullOrWhiteSpace >> not)
        match node with
        | :? JsonArray as a -> a |> Seq.choose (fun n -> if isNull n then None else nodeToString n) |> Seq.collect split |> Seq.toList
        | _ -> nodeToString node |> Option.map (split >> Seq.toList) |> Option.defaultValue []

    let private tryListWithKey fm names = tryNode fm names |> Option.map (fun (k,n) -> k, valuesOfNode n)
    let private listValue fm names = tryListWithKey fm names |> Option.map snd |> Option.defaultValue []

    let private tryBoolean fm names =
        match tryNode fm names with
        | Some(_, (:? JsonValue as v)) ->
            try Some(v.GetValue<bool>())
            with _ ->
                nodeToString v |> Option.bind (fun s ->
                    match s.ToLowerInvariant() with "true" | "yes" | "1" -> Some true | "false" | "no" | "0" -> Some false | _ -> None)
        | _ -> None

    let private tryNumber fm names =
        match tryNode fm names with
        | Some(_, (:? JsonValue as v)) ->
            try Some(v.GetValue<double>())
            with _ ->
                nodeToString v |> Option.bind (fun s ->
                    match s.ToLowerInvariant() with
                    | "high" -> Some 0.9 | "medium" -> Some 0.6 | "low" -> Some 0.3
                    | text -> match Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture) with true,n -> Some n | _ -> None)
        | _ -> None

    let private normalizeDate source field raw findings =
        match raw with
        | None -> None, findings
        | Some value ->
            match DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces) with
            | true,date -> Some(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), findings
            | _ -> None, { Code="invalid-date"; Severity=Warning; SourcePath=source; FrontMatterKey=Some field
                           Message=sprintf "Date value '%s' could not be parsed and was preserved only in raw front matter." value
                           Remedy=Some "Use an ISO-8601 date if this field should participate in date projections." } :: findings

    let private inferType declaredId sourcePath =
        let prefix =
            declaredId |> Option.bind (fun id -> id.Split('-', StringSplitOptions.RemoveEmptyEntries) |> Array.tryHead)
        match prefix with
        | Some "RP" -> Some("research-package","id-prefix:RP")
        | Some "JR" -> Some("journal-entry","id-prefix:JR")
        | Some "EV" -> Some("evidence","id-prefix:EV")
        | Some "HY" -> Some("hypothesis","id-prefix:HY")
        | Some "TH" -> Some("theory","id-prefix:TH")
        | Some "EX" -> Some("experiment","id-prefix:EX")
        | Some "DF" -> Some("decision-record","id-prefix:DF")
        | Some "CN" -> Some("concept","id-prefix:CN")
        | Some "GL" -> Some("glossary","id-prefix:GL")
        | Some "RFR" -> Some("frontier-record","id-prefix:RFR")
        | _ ->
            let p = "/" + normalizeSlashes (sourcePath.ToLowerInvariant()) + "/"
            if p.Contains("/evidence/") then Some("evidence","directory:evidence")
            elif p.Contains("/hypotheses/") then Some("hypothesis","directory:hypotheses")
            elif p.Contains("/theories/") then Some("theory","directory:theories")
            elif p.Contains("/experiments/") then Some("experiment","directory:experiments")
            elif p.Contains("/journals/") then Some("journal-entry","directory:journals")
            elif p.Contains("/frontier/") then Some("frontier-record","directory:frontier")
            else None

    let private buildUnknown (fm: JsonObject) =
        let result = JsonObject()
        for pair in fm do
            if not (Set.contains pair.Key knownKeys) then result[pair.Key] <- if isNull pair.Value then null else pair.Value.DeepClone()
        result

    let private artifactFromRaw (raw: RawDocument) =
        let fm = raw.FrontMatter
        let mutable findings : Finding list = []
        let declaredId = tryString fm [ "id"; "identifier"; "stableId" ]
        let key,keyKind = match declaredId with Some id -> id,"declared-id" | None -> "path:"+pathKey raw.SourcePath,"source-path"
        let title,titleSource =
            match tryString fm ["title"] with
            | Some t -> t,"front-matter:title"
            | None ->
                match raw.Headings |> List.tryFind (fun h -> h.Depth=1 && not(String.IsNullOrWhiteSpace h.Text)) with
                | Some h -> h.Text.Trim(),"heading:h1"
                | None -> Path.GetFileNameWithoutExtension(raw.SourcePath).Replace('-',' ').Replace('_',' '),"source-path"
        let artifactType,typeSource =
            match tryNode fm ["artifactType";"artifact_type";"document_type"] with
            | Some(k,n) -> nodeToString n,"front-matter:"+k
            | None -> match inferType declaredId raw.SourcePath with Some(t,s) -> Some t,s | None -> None,"unknown"
        if declaredId.IsNone then findings <- { Code="missing-id"; Severity=Warning; SourcePath=raw.SourcePath; FrontMatterKey=None
                                                Message="Artifact has no declared id; a source-path key is used for publication identity."; Remedy=None }::findings
        if artifactType.IsNone then findings <- { Code="type-unknown"; Severity=Warning; SourcePath=raw.SourcePath; FrontMatterKey=None
                                                  Message="Artifact type is unknown; no fallback research-document type was fabricated."; Remedy=None }::findings
        let createdRaw = tryString fm ["created";"created_at";"date"]
        let createdField = if fm.ContainsKey "created" then "created" elif fm.ContainsKey "created_at" then "created_at" else "date"
        let created,f1 = normalizeDate raw.SourcePath createdField createdRaw findings
        findings <- f1
        let updatedRaw = tryString fm ["updated";"updated_at"]
        let updatedField = if fm.ContainsKey "updated" then "updated" else "updated_at"
        let updated,f2 = normalizeDate raw.SourcePath updatedField updatedRaw findings
        findings <- f2
        let canonicalUrl = match declaredId with Some id -> "/a/"+Uri.EscapeDataString(id)+"/" | None -> "/s/"+pathKey raw.SourcePath+"/"
        let oldUrl =
            let seed = match declaredId with Some id -> id+"-"+title | None -> title
            "/research/"+slugify seed+"/"
        let legacyUrls = [ tryString fm ["url"]; Some oldUrl ] |> List.choose id |> List.filter ((<>) canonicalUrl) |> List.distinct
        let summary = tryString fm ["summary";"abstract"] |> Option.orElse raw.Excerpt |> Option.filter (String.IsNullOrWhiteSpace >> not)
        { Key=key; KeyKind=keyKind; DeclaredId=declaredId; Title=title; TitleSource=titleSource; ArtifactType=artifactType; TypeSource=typeSource
          Project=tryString fm ["project";"projectId"]; Purposes=listValue fm ["purposes";"documentPurpose";"purpose"] |> List.map _.ToLowerInvariant()
          Audiences=listValue fm ["audiences";"audience"] |> List.map _.ToLowerInvariant(); EntryPoint=tryBoolean fm ["entryPoint"] |> Option.defaultValue false
          EntryPointOrder=tryNumber fm ["entryPointOrder"]; EntryPointLabel=tryString fm ["entryPointLabel"]; ResearchArea=tryString fm ["researchArea";"research_area"]
          Discipline=listValue fm ["discipline"]; Summary=summary; Status=tryString fm ["status"]; Version=tryString fm ["version"]
          Confidence=tryNumber fm ["confidence"]; Completion=tryNumber fm ["completion"]; Priority=tryString fm ["priority"]
          AuthorAgent=tryString fm ["authorAgent";"author_agent";"author"]; Created=created; Updated=updated; Tags=listValue fm ["tags"]
          Keywords=listValue fm ["keywords"]; RelatedProjects=listValue fm ["relatedProjects";"related_projects"]; Bibliography=listValue fm ["references"]
          SourcePath=collapsePath raw.SourcePath; CanonicalUrl=canonicalUrl; LegacyUrls=legacyUrls; FrontMatter=fm.DeepClone() :?> JsonObject
          UnknownFrontMatter=buildUnknown fm }, List.rev findings

    type private RefMode = Mixed | ForceId
    type private RelationSpec = { Aliases:string list; Relation:string; Authority:RelationAuthority; Mode:RefMode }
    let private relationSpecs =
        [ {Aliases=["relatedDocuments";"related_documents"];Relation="related-document";Authority=Canonical;Mode=Mixed}
          {Aliases=["sourceRep";"source_rep"];Relation="source-rep";Authority=Canonical;Mode=ForceId}
          {Aliases=["supersedes"];Relation="supersedes";Authority=Canonical;Mode=Mixed}
          {Aliases=["supersededBy";"superseded_by"];Relation="superseded-by";Authority=Canonical;Mode=Mixed}
          {Aliases=["dependencies"];Relation="prerequisite";Authority=Canonical;Mode=ForceId}
          {Aliases=["evidenceIds";"evidence_ids"];Relation="evidence-reference";Authority=DeclaredUnclassified;Mode=ForceId}
          {Aliases=["hypothesisIds";"hypothesis_ids"];Relation="hypothesis-reference";Authority=DeclaredUnclassified;Mode=ForceId}
          {Aliases=["theoryIds";"theory_ids"];Relation="theory-reference";Authority=DeclaredUnclassified;Mode=ForceId} ]

    let private classifyMixed (raw:string) =
        let v=raw.Trim()
        if v.Contains('/') || v.Contains('\\') then if v.StartsWith("../") || v.StartsWith("./") then FileRelativePath else RepoRelativePath
        elif v.EndsWith(".md",StringComparison.OrdinalIgnoreCase) then BareFileName
        elif idPattern.IsMatch v then IdReference elif v.Length>0 then ProseTitle else Unparsed

    let private resolveReferences artifacts =
        let byId=Dictionary<string,Artifact>(StringComparer.Ordinal)
        let byPath=Dictionary<string,Artifact>(StringComparer.Ordinal)
        let byFile=Dictionary<string,ResizeArray<Artifact>>(StringComparer.OrdinalIgnoreCase)
        for a in artifacts do
            a.DeclaredId |> Option.iter(fun id -> if not(byId.ContainsKey id) then byId[id] <- a)
            byPath[collapsePath a.SourcePath] <- a
            let f=fileNameOf a.SourcePath
            if not(byFile.ContainsKey f) then byFile[f] <- ResizeArray()
            byFile[f].Add a
        let byIdTry id = match byId.TryGetValue id with true,a -> Some a | _ -> None
        let byPathTry p = match byPath.TryGetValue(collapsePath p) with true,a -> Some a | _ -> None
        let outcome source mode raw =
            let kind=if mode=ForceId then IdReference else classifyMixed raw
            let target,status =
                match kind with
                | IdReference -> match byIdTry raw with Some a -> Some a,Resolved | None -> None,Dangling
                | RepoRelativePath ->
                    match byPathTry raw with Some a -> Some a,Resolved | None ->
                        match byPathTry(directoryOf source+"/"+raw) with Some a -> Some a,Resolved | None -> None,Dangling
                | FileRelativePath -> match byPathTry(directoryOf source+"/"+raw) with Some a -> Some a,Resolved | None -> None,Dangling
                | BareFileName -> match byFile.TryGetValue(fileNameOf raw) with true,m when m.Count=1 -> Some m[0],Resolved | true,m when m.Count>1 -> None,Ambiguous | _ -> None,Dangling
                | ProseTitle -> None,NotALink
                | Unparsed -> None,Dangling
            kind,target,status
        let mutable rels=[]
        let mutable findings=[]
        for a in artifacts do
            for spec in relationSpecs do
                match tryListWithKey a.FrontMatter spec.Aliases with
                | None -> ()
                | Some(field,values) ->
                    for raw in values do
                        let kind,target,status=outcome a.SourcePath spec.Mode raw
                        rels <- { SourceKey=a.Key; SourcePath=a.SourcePath; Field=field; Relation=spec.Relation; Authority=spec.Authority; RawTarget=raw
                                  ReferenceKind=kind; Resolution=status; TargetKey=target|>Option.map _.Key; TargetId=target|>Option.bind _.DeclaredId
                                  TargetSourcePath=target|>Option.map _.SourcePath; TargetTitle=target|>Option.map _.Title }::rels
                        if status=Dangling then findings <- {Code="dangling-reference";Severity=Warning;SourcePath=a.SourcePath;FrontMatterKey=Some field;Message=sprintf "%s reference '%s' does not resolve to a published artifact." field raw;Remedy=None}::findings
                        elif status=Ambiguous then findings <- {Code="ambiguous-reference";Severity=Warning;SourcePath=a.SourcePath;FrontMatterKey=Some field;Message=sprintf "%s reference '%s' matches more than one published artifact." field raw;Remedy=Some "Use a declared artifact id or repository-relative path."}::findings
        List.rev rels,List.rev findings

    let private duplicates artifacts =
        let ids =
            artifacts |> List.choose(fun a -> a.DeclaredId|>Option.map(fun id -> id,a.SourcePath)) |> List.groupBy fst
            |> List.collect(fun (id,xs) -> if xs.Length>1 then xs|>List.map(fun (_,p)->{Code="duplicate-id";Severity=Blocking;SourcePath=p;FrontMatterKey=Some "id";Message=sprintf "Declared artifact id '%s' is used by more than one artifact." id;Remedy=Some "Assign unique declared ids before publishing."}) else [])
        let urls =
            artifacts |> List.groupBy _.CanonicalUrl
            |> List.collect(fun (url,xs) -> if xs.Length>1 then xs|>List.map(fun a->{Code="duplicate-url";Severity=Blocking;SourcePath=a.SourcePath;FrontMatterKey=None;Message=sprintf "Canonical URL '%s' is produced by more than one artifact." url;Remedy=Some "Resolve the duplicate identity or path-key collision."}) else [])
        ids@urls

    let private orphans artifacts rels =
        let connected=HashSet<string>(StringComparer.Ordinal)
        for r in rels do if r.Authority=Canonical && r.Resolution=Resolved then connected.Add(r.SourceKey)|>ignore; r.TargetKey|>Option.iter(fun k->connected.Add(k)|>ignore)
        artifacts |> List.choose(fun a -> if connected.Contains a.Key then None else Some {Code="orphan-artifact";Severity=Warning;SourcePath=a.SourcePath;FrontMatterKey=None;Message="Artifact has no resolved canonical incoming or outgoing relationship.";Remedy=None})

    let private capabilities artifacts =
        let q=artifacts|>List.exists(fun a -> a.ArtifactType=Some "frontier-record" || a.DeclaredId|>Option.exists _.StartsWith("RFR-",StringComparison.Ordinal))
        [ {Name="research-index";Available=true;Reason="Artifact identity and metadata are available."}
          {Name="question-view";Available=q;Reason=if q then "Structured frontier records are present." else "No structured frontier records are present in this corpus."}
          {Name="relationship-view";Available=true;Reason="Declared relationships are preserved, resolved when possible, and unresolved references remain visible."}
          {Name="integrity-view";Available=true;Reason="Semantic findings are emitted for every compilation."}
          {Name="finding-view";Available=false;Reason="The current ROS corpus does not identify sub-document findings as stable research objects."}
          {Name="evidence-view";Available=false;Reason="The current ROS corpus does not identify sub-document evidence items as stable research objects."}
          {Name="contradiction-view";Available=false;Reason="The corpus does not encode contradicts/refutes as canonical relationships."}
          {Name="timeline-view";Available=false;Reason="Date coverage is insufficient and legacy publisher dates were fabricated; timeline projection remains disabled."} ]

    let compile (rawDocuments:RawDocument list) =
        let pairs=rawDocuments|>List.map artifactFromRaw
        let artifacts=pairs|>List.map fst
        let rels,rf=resolveReferences artifacts
        let findings=(pairs|>List.collect snd) @ duplicates artifacts @ rf @ orphans artifacts rels
        { Artifacts=artifacts|>List.sortBy _.CanonicalUrl; Relationships=rels
          Findings=findings|>List.sortBy(fun f->f.SourcePath,f.Code,f.Message); Capabilities=capabilities artifacts
          Redirects=artifacts|>List.collect(fun a->a.LegacyUrls|>List.map(fun old->old,a.CanonicalUrl))|>List.distinct|>List.sortBy fst }
