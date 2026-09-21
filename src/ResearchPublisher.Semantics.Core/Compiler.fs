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

    type RawHeading =
        { Depth: int
          Text: string }

    type RawDocument =
        { SourcePath: string
          FrontMatter: JsonObject
          Excerpt: string option
          Headings: RawHeading list }

    type RawDerivedRelationship =
        { SourceRef: string
          TargetRef: string
          Relation: string
          EvidenceSource: string }

    let private knownKeys =
        set
            [ "id"; "identifier"; "stableId"
              "title"; "slug"; "url"
              "document_type"; "artifact_type"; "artifactType"
              "project"; "projectId"
              "purposes"; "documentPurpose"; "purpose"
              "audiences"; "audience"
              "entryPoint"; "entryPointOrder"; "entryPointLabel"
              "research_area"; "researchArea"
              "discipline"; "summary"; "abstract"; "status"; "version"
              "confidence"; "completion"; "priority"
              "author"; "author_agent"; "authorAgent"
              "date"; "created"; "created_at"; "updated"; "updated_at"
              "tags"; "keywords"
              "related_projects"; "relatedProjects"
              "related_documents"; "relatedDocuments"
              "source_rep"; "sourceRep"
              "supersedes"; "superseded_by"; "supersededBy"
              "evidenceIds"; "evidence_ids"
              "hypothesisIds"; "hypothesis_ids"
              "theoryIds"; "theory_ids"
              "dependencies"; "prerequisite"; "originates"; "related_artifacts"
              "references"; "evidence_level" ]

    let private idPattern =
        Regex("^[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)+$", RegexOptions.Compiled)

    let private normalizeSlashes (value: string) =
        value.Replace('\\', '/').Trim()

    let private collapsePath (value: string) =
        let rooted = (normalizeSlashes value).TrimStart('/')
        let stack = ResizeArray<string>()

        for segment in rooted.Split('/', StringSplitOptions.RemoveEmptyEntries) do
            match segment with
            | "." -> ()
            | ".." when stack.Count > 0 -> stack.RemoveAt(stack.Count - 1)
            | ".." -> ()
            | other -> stack.Add(other)

        String.Join("/", stack)

    let private directoryOf (value: string) =
        let normalized = normalizeSlashes value
        let index = normalized.LastIndexOf('/')
        if index < 0 then "" else normalized.Substring(0, index)

    let private fileNameOf (value: string) =
        let parts = (normalizeSlashes value).Split('/', StringSplitOptions.RemoveEmptyEntries)
        if parts.Length = 0 then value else parts[parts.Length - 1]

    let private slugify (value: string) =
        Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')

    let private pathKey (sourcePath: string) =
        let bytes = Encoding.UTF8.GetBytes(collapsePath sourcePath)
        let hash = SHA256.HashData(bytes)
        Convert.ToHexString(hash).ToLowerInvariant().Substring(0, 16)

    let private tryNode (frontMatter: JsonObject) (names: string list) : (string * JsonNode) option =
        names
        |> List.tryPick (fun name ->
            if frontMatter.ContainsKey(name) then
                let node = frontMatter[name]
                if isNull node then None else Some(name, node)
            else
                None)

    let private nodeToString (node: JsonNode) : string option =
        match node with
        | :? JsonValue as value ->
            try
                Some(value.GetValue<string>().Trim())
            with _ ->
                let raw = value.ToJsonString().Trim()
                if raw.Length >= 2 && raw.StartsWith('"') && raw.EndsWith('"') then
                    Some(raw.Substring(1, raw.Length - 2).Trim())
                else
                    Some(raw)
        | _ -> None

    let private tryString (frontMatter: JsonObject) (names: string list) : string option =
        match tryNode frontMatter names with
        | Some(_, node) ->
            nodeToString node
            |> Option.filter (fun value -> not (String.IsNullOrWhiteSpace value))
        | None -> None

    let private splitValues (value: string) =
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun part -> part.Trim())
        |> Array.filter (fun part -> not (String.IsNullOrWhiteSpace part))
        |> Array.toList

    let private valuesOfNode (node: JsonNode) : string list =
        match node with
        | :? JsonArray as values ->
            values
            |> Seq.choose (fun item ->
                if isNull item then None else nodeToString item)
            |> Seq.filter (fun value ->
                not (String.IsNullOrWhiteSpace value))
            |> Seq.toList
        | _ ->
            match nodeToString node with
            | Some value -> splitValues value
            | None -> []

    let private tryListWithKey
        (frontMatter: JsonObject)
        (names: string list)
        : (string * string list) option =
        tryNode frontMatter names
        |> Option.map (fun (key, node) -> key, valuesOfNode node)

    let private listValue (frontMatter: JsonObject) (names: string list) : string list =
        match tryListWithKey frontMatter names with
        | Some(_, values) -> values
        | None -> []

    let private tryBoolean (frontMatter: JsonObject) (names: string list) : bool option =
        match tryNode frontMatter names with
        | Some(_, (:? JsonValue as value)) ->
            try
                Some(value.GetValue<bool>())
            with _ ->
                match nodeToString value with
                | Some raw ->
                    match raw.Trim().ToLowerInvariant() with
                    | "true"
                    | "yes"
                    | "1" -> Some true
                    | "false"
                    | "no"
                    | "0" -> Some false
                    | _ -> None
                | None -> None
        | _ -> None

    let private tryNumber (frontMatter: JsonObject) (names: string list) : float option =
        match tryNode frontMatter names with
        | Some(_, (:? JsonValue as value)) ->
            try
                Some(value.GetValue<double>())
            with _ ->
                match nodeToString value with
                | Some raw ->
                    match raw.Trim().ToLowerInvariant() with
                    | "high" -> Some 0.9
                    | "medium" -> Some 0.6
                    | "low" -> Some 0.3
                    | text ->
                        let mutable parsed = 0.0
                        if Double.TryParse(
                            text,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            &parsed
                        ) then
                            Some parsed
                        else
                            None
                | None -> None
        | _ -> None

    let private normalizeDate
        (sourcePath: string)
        (field: string)
        (raw: string option)
        (findings: Finding list)
        : string option * Finding list =
        match raw with
        | None -> None, findings
        | Some value ->
            let mutable parsed = DateTimeOffset.MinValue

            if DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                &parsed
            ) then
                Some(parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), findings
            else
                let finding =
                    { Code = "invalid-date"
                      Severity = Warning
                      SourcePath = sourcePath
                      FrontMatterKey = Some field
                      Message =
                        sprintf
                            "Date value '%s' could not be parsed and was preserved only in raw front matter."
                            value
                      Remedy =
                        Some
                            "Use an ISO-8601 date if this field should participate in date projections." }

                None, finding :: findings

    let private normalizeArtifactType (value: string) =
        value.Trim().ToLowerInvariant().Replace('_', '-')

    let private inferType
        (declaredId: string option)
        (sourcePath: string)
        : (string * string) option =
        let prefix =
            declaredId
            |> Option.bind (fun id ->
                let segments =
                    id.Split('-', StringSplitOptions.RemoveEmptyEntries)

                if segments.Length = 0 then None else Some segments[0])

        match prefix with
        | Some "RP" -> Some("research-package", "id-prefix:RP")
        | Some "JR" -> Some("journal-entry", "id-prefix:JR")
        | Some "EV" -> Some("evidence", "id-prefix:EV")
        | Some "HY" -> Some("hypothesis", "id-prefix:HY")
        | Some "TH" -> Some("theory", "id-prefix:TH")
        | Some "EX" -> Some("experiment", "id-prefix:EX")
        | Some "DF" -> Some("decision-record", "id-prefix:DF")
        | Some "CN" -> Some("concept", "id-prefix:CN")
        | Some "GL" -> Some("glossary", "id-prefix:GL")
        | Some "RFR" -> Some("frontier-record", "id-prefix:RFR")
        | _ ->
            let normalized =
                "/" + (normalizeSlashes sourcePath).ToLowerInvariant() + "/"

            if normalized.Contains("/evidence/") then
                Some("evidence", "directory:evidence")
            elif normalized.Contains("/hypotheses/") then
                Some("hypothesis", "directory:hypotheses")
            elif normalized.Contains("/theories/") then
                Some("theory", "directory:theories")
            elif normalized.Contains("/experiments/") then
                Some("experiment", "directory:experiments")
            elif normalized.Contains("/journals/") then
                Some("journal-entry", "directory:journals")
            elif normalized.Contains("/frontier/") then
                Some("frontier-record", "directory:frontier")
            else
                None

    let private buildUnknownFrontMatter (frontMatter: JsonObject) =
        let unknown = JsonObject()

        for pair in frontMatter do
            if not (Set.contains pair.Key knownKeys) then
                unknown[pair.Key] <-
                    if isNull pair.Value then
                        null
                    else
                        pair.Value.DeepClone()

        unknown

    let private artifactFromRaw
        (raw: RawDocument)
        : Artifact * Finding list =
        let frontMatter = raw.FrontMatter
        let mutable findings: Finding list = []
        let declaredId =
            tryString frontMatter [ "id"; "identifier"; "stableId" ]

        let key, keyKind =
            match declaredId with
            | Some id -> id, "declared-id"
            | None -> "path:" + pathKey raw.SourcePath, "source-path"

        let title, titleSource =
            match tryString frontMatter [ "title" ] with
            | Some title -> title, "front-matter:title"
            | None ->
                match
                    raw.Headings
                    |> List.tryFind (fun heading ->
                        heading.Depth = 1
                        && not (String.IsNullOrWhiteSpace heading.Text))
                with
                | Some heading ->
                    heading.Text.Trim(), "heading:h1"
                | None ->
                    Path.GetFileNameWithoutExtension(raw.SourcePath)
                        .Replace('-', ' ')
                        .Replace('_', ' '),
                    "source-path"

        let artifactType, typeSource =
            match
                tryNode
                    frontMatter
                    [ "artifactType"; "artifact_type"; "document_type" ]
            with
            | Some(keyName, node) ->
                nodeToString node
                |> Option.map normalizeArtifactType,
                "front-matter:" + keyName
            | None ->
                match inferType declaredId raw.SourcePath with
                | Some(value, source) -> Some value, source
                | None -> None, "unknown"

        if declaredId.IsNone then
            findings <-
                { Code = "missing-id"
                  Severity = Warning
                  SourcePath = raw.SourcePath
                  FrontMatterKey = None
                  Message =
                    "Artifact has no declared id; a source-path key is used for publication identity."
                  Remedy = None }
                :: findings

        if artifactType.IsNone then
            findings <-
                { Code = "type-unknown"
                  Severity = Warning
                  SourcePath = raw.SourcePath
                  FrontMatterKey = None
                  Message =
                    "Artifact type is unknown; no fallback research-document type was fabricated."
                  Remedy = None }
                :: findings

        let createdRaw =
            tryString frontMatter [ "created"; "created_at"; "date" ]

        let createdField =
            if frontMatter.ContainsKey("created") then
                "created"
            elif frontMatter.ContainsKey("created_at") then
                "created_at"
            else
                "date"

        let created, createdFindings =
            normalizeDate raw.SourcePath createdField createdRaw findings

        findings <- createdFindings

        let updatedRaw =
            tryString frontMatter [ "updated"; "updated_at" ]

        let updatedField =
            if frontMatter.ContainsKey("updated") then
                "updated"
            else
                "updated_at"

        let updated, updatedFindings =
            normalizeDate raw.SourcePath updatedField updatedRaw findings

        findings <- updatedFindings

        let canonicalUrl =
            match declaredId with
            | Some id -> "/a/" + Uri.EscapeDataString(id) + "/"
            | None -> "/s/" + pathKey raw.SourcePath + "/"

        let oldUrl =
            let seed =
                match declaredId with
                | Some id -> id + "-" + title
                | None -> title

            "/research/" + slugify seed + "/"

        let legacyUrls =
            [ tryString frontMatter [ "url" ]; Some oldUrl ]
            |> List.choose id
            |> List.filter (fun value -> value <> canonicalUrl)
            |> List.distinct

        let summary =
            match tryString frontMatter [ "summary"; "abstract" ] with
            | Some value -> Some value
            | None ->
                raw.Excerpt
                |> Option.filter (fun value ->
                    not (String.IsNullOrWhiteSpace value))

        let artifact =
            { Key = key
              KeyKind = keyKind
              DeclaredId = declaredId
              Title = title
              TitleSource = titleSource
              ArtifactType = artifactType
              TypeSource = typeSource
              Project =
                tryString frontMatter [ "project"; "projectId" ]
              Purposes =
                listValue
                    frontMatter
                    [ "purposes"; "documentPurpose"; "purpose" ]
                |> List.map (fun value -> value.ToLowerInvariant())
              Audiences =
                listValue frontMatter [ "audiences"; "audience" ]
                |> List.map (fun value -> value.ToLowerInvariant())
              EntryPoint =
                tryBoolean frontMatter [ "entryPoint" ]
                |> Option.defaultValue false
              EntryPointOrder =
                tryNumber frontMatter [ "entryPointOrder" ]
              EntryPointLabel =
                tryString frontMatter [ "entryPointLabel" ]
              ResearchArea =
                tryString frontMatter [ "researchArea"; "research_area" ]
              Discipline =
                listValue frontMatter [ "discipline" ]
              Summary = summary
              Status =
                tryString frontMatter [ "status" ]
              Version =
                tryString frontMatter [ "version" ]
              Confidence =
                tryNumber frontMatter [ "confidence" ]
              Completion =
                tryNumber frontMatter [ "completion" ]
              Priority =
                tryString frontMatter [ "priority" ]
              AuthorAgent =
                tryString
                    frontMatter
                    [ "authorAgent"; "author_agent"; "author" ]
              Created = created
              Updated = updated
              Tags =
                listValue frontMatter [ "tags" ]
              Keywords =
                listValue frontMatter [ "keywords" ]
              RelatedProjects =
                listValue
                    frontMatter
                    [ "relatedProjects"; "related_projects" ]
              Bibliography =
                listValue frontMatter [ "references" ]
              SourcePath = collapsePath raw.SourcePath
              CanonicalUrl = canonicalUrl
              LegacyUrls = legacyUrls
              FrontMatter =
                frontMatter.DeepClone() :?> JsonObject
              UnknownFrontMatter =
                buildUnknownFrontMatter frontMatter }

        artifact, List.rev findings

    type private RefMode =
        | Mixed
        | ForceId

    type private RelationSpec =
        { Aliases: string list
          Relation: string
          Authority: RelationAuthority
          Mode: RefMode }

    let private relationSpecs =
        [ { Aliases = [ "relatedDocuments"; "related_documents" ]
            Relation = "related-document"
            Authority = Canonical
            Mode = Mixed }
          { Aliases = [ "sourceRep"; "source_rep" ]
            Relation = "source-rep"
            Authority = Canonical
            Mode = ForceId }
          { Aliases = [ "supersedes" ]
            Relation = "supersedes"
            Authority = Canonical
            Mode = Mixed }
          { Aliases = [ "supersededBy"; "superseded_by" ]
            Relation = "superseded-by"
            Authority = Canonical
            Mode = Mixed }
          { Aliases = [ "dependencies"; "prerequisite" ]
            Relation = "prerequisite"
            Authority = Canonical
            Mode = ForceId }
          { Aliases = [ "originates" ]
            Relation = "originates"
            Authority = Canonical
            Mode = Mixed }
          { Aliases = [ "related_artifacts" ]
            Relation = "related-artifact"
            Authority = DeclaredUnclassified
            Mode = ForceId }
          { Aliases = [ "evidenceIds"; "evidence_ids" ]
            Relation = "evidence-reference"
            Authority = DeclaredUnclassified
            Mode = ForceId }
          { Aliases = [ "hypothesisIds"; "hypothesis_ids" ]
            Relation = "hypothesis-reference"
            Authority = DeclaredUnclassified
            Mode = ForceId }
          { Aliases = [ "theoryIds"; "theory_ids" ]
            Relation = "theory-reference"
            Authority = DeclaredUnclassified
            Mode = ForceId } ]

    let private classifyMixed (raw: string) =
        let value = raw.Trim()

        if value.Contains('/') || value.Contains('\\') then
            if
                value.StartsWith("../", StringComparison.Ordinal)
                || value.StartsWith("./", StringComparison.Ordinal)
            then
                FileRelativePath
            else
                RepoRelativePath
        elif value.EndsWith(".md", StringComparison.OrdinalIgnoreCase) then
            BareFileName
        elif idPattern.IsMatch(value) then
            IdReference
        elif value.Length > 0 then
            ProseTitle
        else
            Unparsed

    let private resolveReferences
        (artifacts: Artifact list)
        : Relationship list * Finding list =
        let byId = Dictionary<string, Artifact>(StringComparer.Ordinal)
        let byPath =
            Dictionary<string, Artifact>(StringComparer.Ordinal)

        let byFileName =
            Dictionary<string, ResizeArray<Artifact>>(
                StringComparer.OrdinalIgnoreCase
            )

        for artifact in artifacts do
            match artifact.DeclaredId with
            | Some id when not (byId.ContainsKey id) ->
                byId[id] <- artifact
            | _ -> ()

            byPath[collapsePath artifact.SourcePath] <- artifact

            let fileName = fileNameOf artifact.SourcePath

            if not (byFileName.ContainsKey fileName) then
                byFileName[fileName] <- ResizeArray<Artifact>()

            byFileName[fileName].Add(artifact)

        let tryArtifactById (id: string) =
            match byId.TryGetValue(id) with
            | true, artifact -> Some artifact
            | _ -> None

        let tryArtifactByPath (path: string) =
            match byPath.TryGetValue(collapsePath path) with
            | true, artifact -> Some artifact
            | _ -> None

        let outcome
            (sourcePath: string)
            (mode: RefMode)
            (raw: string)
            : ReferenceKind * Artifact option * ResolutionStatus =
            let kind =
                if mode = ForceId then
                    IdReference
                else
                    classifyMixed raw

            let target, status =
                match kind with
                | IdReference ->
                    match tryArtifactById raw with
                    | Some artifact -> Some artifact, Resolved
                    | None -> None, Dangling
                | RepoRelativePath ->
                    match tryArtifactByPath raw with
                    | Some artifact -> Some artifact, Resolved
                    | None ->
                        let relativePath =
                            directoryOf sourcePath + "/" + raw

                        match tryArtifactByPath relativePath with
                        | Some artifact -> Some artifact, Resolved
                        | None -> None, Dangling
                | FileRelativePath ->
                    let relativePath =
                        directoryOf sourcePath + "/" + raw

                    match tryArtifactByPath relativePath with
                    | Some artifact -> Some artifact, Resolved
                    | None -> None, Dangling
                | BareFileName ->
                    match byFileName.TryGetValue(fileNameOf raw) with
                    | true, matches when matches.Count = 1 ->
                        Some matches[0], Resolved
                    | true, matches when matches.Count > 1 ->
                        None, Ambiguous
                    | _ -> None, Dangling
                | ProseTitle -> None, NotALink
                | Unparsed -> None, Dangling

            kind, target, status

        let mutable relationships: Relationship list = []
        let mutable findings: Finding list = []

        for artifact in artifacts do
            for spec in relationSpecs do
                match
                    tryListWithKey
                        artifact.FrontMatter
                        spec.Aliases
                with
                | None -> ()
                | Some(field, values) ->
                    for raw in values do
                        let kind, target, status =
                            outcome artifact.SourcePath spec.Mode raw

                        let relationship: Relationship =
                            { SourceKey = artifact.Key
                              SourcePath = artifact.SourcePath
                              RawSource = None
                              EvidenceSource = Some artifact.SourcePath
                              Field = field
                              Relation = spec.Relation
                              Authority = spec.Authority
                              RawTarget = raw
                              ReferenceKind = kind
                              Resolution = status
                              TargetKey =
                                target |> Option.map (fun value -> value.Key)
                              TargetId =
                                target
                                |> Option.bind (fun value -> value.DeclaredId)
                              TargetSourcePath =
                                target
                                |> Option.map (fun value -> value.SourcePath)
                              TargetTitle =
                                target
                                |> Option.map (fun value -> value.Title) }

                        relationships <- relationship :: relationships

                        match status with
                        | Dangling ->
                            findings <-
                                { Code = "dangling-reference"
                                  Severity = Warning
                                  SourcePath = artifact.SourcePath
                                  FrontMatterKey = Some field
                                  Message =
                                    sprintf
                                        "%s reference '%s' does not resolve to a published artifact."
                                        field
                                        raw
                                  Remedy = None }
                                :: findings
                        | Ambiguous ->
                            findings <-
                                { Code = "ambiguous-reference"
                                  Severity = Warning
                                  SourcePath = artifact.SourcePath
                                  FrontMatterKey = Some field
                                  Message =
                                    sprintf
                                        "%s reference '%s' matches more than one published artifact."
                                        field
                                        raw
                                  Remedy =
                                    Some
                                        "Use a declared artifact id or repository-relative path." }
                                :: findings
                        | _ -> ()

        List.rev relationships, List.rev findings

    let private resolveDerivedRelationships
        (artifacts: Artifact list)
        (rawRelationships: RawDerivedRelationship list)
        : Relationship list * Finding list =
        let byId = Dictionary<string, Artifact>(StringComparer.Ordinal)
        let byPath = Dictionary<string, Artifact>(StringComparer.Ordinal)

        for artifact in artifacts do
            match artifact.DeclaredId with
            | Some id when not (byId.ContainsKey id) ->
                byId[id] <- artifact
            | _ -> ()

            byPath[collapsePath artifact.SourcePath] <- artifact

        let tryById (id: string) =
            match byId.TryGetValue(id) with
            | true, artifact -> Some artifact
            | _ -> None

        let tryByPath (path: string) =
            match byPath.TryGetValue(collapsePath path) with
            | true, artifact -> Some artifact
            | _ -> None

        let resolveEndpoint (reference: string) =
            let value = reference.Trim()

            if value.StartsWith("DOC:", StringComparison.Ordinal) then
                let sourcePath = value.Substring(4)
                RepoRelativePath, tryByPath sourcePath
            else
                match tryById value with
                | Some artifact -> IdReference, Some artifact
                | None ->
                    if value.Contains("/") || value.EndsWith(".md", StringComparison.OrdinalIgnoreCase) then
                        RepoRelativePath, tryByPath value
                    else
                        IdReference, None

        let mutable relationships: Relationship list = []
        let mutable findings: Finding list = []

        for raw in rawRelationships do
            let _, source = resolveEndpoint raw.SourceRef
            let targetKind, target = resolveEndpoint raw.TargetRef

            let status =
                if source.IsSome && target.IsSome then
                    Resolved
                else
                    Dangling

            let sourceKey =
                source
                |> Option.map (fun artifact -> artifact.Key)
                |> Option.defaultValue ("unresolved:" + raw.SourceRef)

            let sourcePath =
                source
                |> Option.map (fun artifact -> artifact.SourcePath)
                |> Option.defaultValue raw.EvidenceSource

            let relationship =
                { SourceKey = sourceKey
                  SourcePath = sourcePath
                  RawSource = Some raw.SourceRef
                  EvidenceSource = Some raw.EvidenceSource
                  Field = "derived-relationship"
                  Relation = raw.Relation
                  Authority = Derived
                  RawTarget = raw.TargetRef
                  ReferenceKind = targetKind
                  Resolution = status
                  TargetKey = target |> Option.map (fun artifact -> artifact.Key)
                  TargetId = target |> Option.bind (fun artifact -> artifact.DeclaredId)
                  TargetSourcePath = target |> Option.map (fun artifact -> artifact.SourcePath)
                  TargetTitle = target |> Option.map (fun artifact -> artifact.Title) }

            relationships <- relationship :: relationships

            if status = Dangling then
                let missing =
                    [ if source.IsNone then yield "source " + raw.SourceRef
                      if target.IsNone then yield "target " + raw.TargetRef ]
                    |> String.concat ", "

                findings <-
                    { Code = "derived-relationship-unresolved"
                      Severity = Warning
                      SourcePath = raw.EvidenceSource
                      FrontMatterKey = None
                      Message =
                        sprintf
                            "Derived %s relationship could not resolve %s."
                            raw.Relation
                            missing
                      Remedy =
                        Some
                            "Keep the generated graph and published artifact inventory aligned." }
                    :: findings

        List.rev relationships, List.rev findings

    let private duplicateFindings
        (artifacts: Artifact list)
        : Finding list =
        let duplicateIds =
            artifacts
            |> List.choose (fun artifact ->
                artifact.DeclaredId
                |> Option.map (fun id -> id, artifact.SourcePath))
            |> List.groupBy fst
            |> List.collect (fun (id, values) ->
                if List.length values > 1 then
                    values
                    |> List.map (fun (_, sourcePath) ->
                        { Code = "duplicate-id"
                          Severity = Blocking
                          SourcePath = sourcePath
                          FrontMatterKey = Some "id"
                          Message =
                            sprintf
                                "Declared artifact id '%s' is used by more than one artifact."
                                id
                          Remedy =
                            Some
                                "Assign unique declared ids before publishing." })
                else
                    [])

        let duplicateUrls =
            artifacts
            |> List.groupBy (fun artifact -> artifact.CanonicalUrl)
            |> List.collect (fun (url, values) ->
                if List.length values > 1 then
                    values
                    |> List.map (fun artifact ->
                        { Code = "duplicate-url"
                          Severity = Blocking
                          SourcePath = artifact.SourcePath
                          FrontMatterKey = None
                          Message =
                            sprintf
                                "Canonical URL '%s' is produced by more than one artifact."
                                url
                          Remedy =
                            Some
                                "Resolve the duplicate identity or path-key collision." })
                else
                    [])

        duplicateIds @ duplicateUrls

    let private orphanFindings
        (artifacts: Artifact list)
        (relationships: Relationship list)
        : Finding list =
        let connected = HashSet<string>(StringComparer.Ordinal)

        for relationship in relationships do
            if
                relationship.Authority = Canonical
                && relationship.Resolution = Resolved
            then
                connected.Add(relationship.SourceKey) |> ignore

                match relationship.TargetKey with
                | Some key -> connected.Add(key) |> ignore
                | None -> ()

        artifacts
        |> List.choose (fun artifact ->
            if connected.Contains(artifact.Key) then
                None
            else
                Some
                    { Code = "orphan-artifact"
                      Severity = Warning
                      SourcePath = artifact.SourcePath
                      FrontMatterKey = None
                      Message =
                        "Artifact has no resolved canonical incoming or outgoing relationship."
                      Remedy = None })

    let private capabilities
        (artifacts: Artifact list)
        : ViewCapability list =
        let hasQuestions =
            artifacts
            |> List.exists (fun artifact ->
                artifact.ArtifactType = Some "frontier-record"
                || (artifact.DeclaredId
                    |> Option.exists (fun id ->
                        id.StartsWith("RFR-", StringComparison.Ordinal))))

        [ { Name = "research-index"
            Available = true
            Reason = "Artifact identity and metadata are available." }
          { Name = "question-view"
            Available = hasQuestions
            Reason =
                if hasQuestions then
                    "Structured frontier records are present."
                else
                    "No structured frontier records are present in this corpus." }
          { Name = "relationship-view"
            Available = true
            Reason =
                "Declared relationships are preserved, resolved when possible, and unresolved references remain visible." }
          { Name = "integrity-view"
            Available = true
            Reason = "Semantic findings are emitted for every compilation." }
          { Name = "finding-view"
            Available = false
            Reason =
                "The current ROS corpus does not identify sub-document findings as stable research objects." }
          { Name = "evidence-view"
            Available = false
            Reason =
                "The current ROS corpus does not identify sub-document evidence items as stable research objects." }
          { Name = "contradiction-view"
            Available = false
            Reason =
                "The corpus does not encode contradicts/refutes as canonical relationships." }
          { Name = "timeline-view"
            Available = false
            Reason =
                "Date coverage is insufficient and legacy publisher dates were fabricated; timeline projection remains disabled." } ]

    let compileWithDerived
        (rawDocuments: RawDocument list)
        (rawDerivedRelationships: RawDerivedRelationship list)
        : Compilation =
        let artifactsWithFindings =
            rawDocuments |> List.map artifactFromRaw

        let artifacts =
            artifactsWithFindings |> List.map fst

        let initialFindings =
            artifactsWithFindings |> List.collect snd

        let declaredRelationships, relationshipFindings =
            resolveReferences artifacts

        let derivedRelationships, derivedFindings =
            resolveDerivedRelationships artifacts rawDerivedRelationships

        let relationships =
            declaredRelationships @ derivedRelationships

        let findings =
            initialFindings
            @ duplicateFindings artifacts
            @ relationshipFindings
            @ derivedFindings
            @ orphanFindings artifacts relationships

        let redirects =
            artifacts
            |> List.collect (fun artifact ->
                artifact.LegacyUrls
                |> List.map (fun oldUrl ->
                    oldUrl, artifact.CanonicalUrl))
            |> List.distinct
            |> List.sortBy fst

        { Artifacts =
            artifacts
            |> List.sortBy (fun artifact -> artifact.CanonicalUrl)
          Relationships = relationships
          Findings =
            findings
            |> List.sortBy (fun finding ->
                finding.SourcePath, finding.Code, finding.Message)
          Capabilities = capabilities artifacts
          Redirects = redirects }


    let compile (rawDocuments: RawDocument list) : Compilation =
        compileWithDerived rawDocuments []
