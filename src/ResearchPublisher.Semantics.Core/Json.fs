namespace ResearchPublisher.Semantics.Core

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization.Metadata

module SemanticJson =

    let private stringNode (value: string) : JsonNode =
        JsonValue.Create<string>(value) :> JsonNode

    let private boolNode (value: bool) : JsonNode =
        JsonValue.Create<bool>(value) :> JsonNode

    let private numberNode (value: float) : JsonNode =
        JsonValue.Create<float>(value) :> JsonNode

    let private optionalString (value: string option) : JsonNode =
        match value with
        | Some text -> stringNode text
        | None -> null

    let private optionalNumber (value: float option) : JsonNode =
        match value with
        | Some number -> numberNode number
        | None -> null

    let private stringArray (values: string list) : JsonArray =
        let result = JsonArray()

        for value in values do
            result.Add(stringNode value)

        result

    let private valueAsInt
        (fallback: int)
        (node: JsonNode)
        : int =
        match node with
        | :? JsonValue as value ->
            try
                value.GetValue<int>()
            with _ ->
                fallback
        | _ -> fallback

    let private valueAsString
        (fallback: string)
        (node: JsonNode)
        : string =
        match node with
        | :? JsonValue as value ->
            try
                value.GetValue<string>()
            with _ ->
                fallback
        | _ -> fallback

    let private rawHeadingFromNode
        (node: JsonNode)
        : Compiler.RawHeading option =
        match node with
        | :? JsonObject as obj ->
            Some
                { Depth = valueAsInt 0 obj["depth"]
                  Text = valueAsString "" obj["text"] }
        | _ -> None

    let private rawDocumentFromNode
        (node: JsonNode)
        : Compiler.RawDocument option =
        match node with
        | :? JsonObject as obj ->
            let sourcePath =
                valueAsString "" obj["sourcePath"]

            if String.IsNullOrWhiteSpace(sourcePath) then
                None
            else
                let frontMatter =
                    match obj["frontmatter"] with
                    | :? JsonObject as value ->
                        value.DeepClone() :?> JsonObject
                    | _ -> JsonObject()

                let excerpt =
                    let value =
                        valueAsString "" obj["excerpt"]

                    if String.IsNullOrWhiteSpace(value) then
                        None
                    else
                        Some value

                let headings =
                    match obj["headings"] with
                    | :? JsonArray as values ->
                        values
                        |> Seq.choose rawHeadingFromNode
                        |> Seq.toList
                    | _ -> []

                Some
                    { SourcePath = sourcePath
                      FrontMatter = frontMatter
                      Excerpt = excerpt
                      Headings = headings }
        | _ -> None

    let private rawDerivedRelationshipFromNode
        (node: JsonNode)
        : Compiler.RawDerivedRelationship option =
        match node with
        | :? JsonObject as obj ->
            let sourceRef = valueAsString "" obj["sourceRef"]
            let targetRef = valueAsString "" obj["targetRef"]
            let relation = valueAsString "" obj["relation"]
            let evidenceSource = valueAsString "" obj["evidenceSource"]

            if
                String.IsNullOrWhiteSpace(sourceRef)
                || String.IsNullOrWhiteSpace(targetRef)
                || String.IsNullOrWhiteSpace(relation)
                || String.IsNullOrWhiteSpace(evidenceSource)
            then
                None
            else
                Some
                    { SourceRef = sourceRef
                      TargetRef = targetRef
                      Relation = relation
                      EvidenceSource = evidenceSource }
        | _ -> None

    let private findingNode (finding: Finding) : JsonObject =
        let obj = JsonObject()
        obj["code"] <- stringNode finding.Code
        obj["severity"] <- stringNode (Severity.toWire finding.Severity)
        obj["sourcePath"] <- stringNode finding.SourcePath
        obj["frontMatterKey"] <- optionalString finding.FrontMatterKey
        obj["message"] <- stringNode finding.Message
        obj["remedy"] <- optionalString finding.Remedy
        obj

    let private relationshipNode
        (relationship: Relationship)
        : JsonObject =
        let obj = JsonObject()
        obj["sourceKey"] <- stringNode relationship.SourceKey
        obj["sourcePath"] <- stringNode relationship.SourcePath
        obj["rawSource"] <- optionalString relationship.RawSource
        obj["evidenceSource"] <- optionalString relationship.EvidenceSource
        obj["field"] <- stringNode relationship.Field
        obj["relation"] <- stringNode relationship.Relation

        obj["authority"] <-
            stringNode
                (RelationAuthority.toWire relationship.Authority)

        obj["rawTarget"] <- stringNode relationship.RawTarget

        obj["referenceKind"] <-
            stringNode
                (ReferenceKind.toWire relationship.ReferenceKind)

        obj["resolution"] <-
            stringNode
                (ResolutionStatus.toWire relationship.Resolution)

        obj["targetKey"] <- optionalString relationship.TargetKey
        obj["targetId"] <- optionalString relationship.TargetId

        obj["targetSourcePath"] <-
            optionalString relationship.TargetSourcePath

        obj["targetTitle"] <- optionalString relationship.TargetTitle
        obj

    let private artifactNode
        (relationships: Relationship list)
        (artifact: Artifact)
        : JsonObject =
        let obj = JsonObject()
        obj["key"] <- stringNode artifact.Key
        obj["keyKind"] <- stringNode artifact.KeyKind
        obj["id"] <- optionalString artifact.DeclaredId
        obj["title"] <- stringNode artifact.Title
        obj["titleSource"] <- stringNode artifact.TitleSource
        obj["artifactType"] <- optionalString artifact.ArtifactType
        obj["typeSource"] <- stringNode artifact.TypeSource
        obj["project"] <- optionalString artifact.Project
        obj["purposes"] <- stringArray artifact.Purposes
        obj["audiences"] <- stringArray artifact.Audiences
        obj["entryPoint"] <- boolNode artifact.EntryPoint
        obj["entryPointOrder"] <- optionalNumber artifact.EntryPointOrder

        obj["entryPointLabel"] <-
            optionalString artifact.EntryPointLabel

        obj["researchArea"] <- optionalString artifact.ResearchArea
        obj["discipline"] <- stringArray artifact.Discipline
        obj["summary"] <- optionalString artifact.Summary
        obj["status"] <- optionalString artifact.Status
        obj["version"] <- optionalString artifact.Version
        obj["confidence"] <- optionalNumber artifact.Confidence
        obj["completion"] <- optionalNumber artifact.Completion
        obj["priority"] <- optionalString artifact.Priority
        obj["authorAgent"] <- optionalString artifact.AuthorAgent
        obj["created"] <- optionalString artifact.Created
        obj["updated"] <- optionalString artifact.Updated
        obj["tags"] <- stringArray artifact.Tags
        obj["keywords"] <- stringArray artifact.Keywords

        obj["relatedProjects"] <-
            stringArray artifact.RelatedProjects

        obj["bibliography"] <- stringArray artifact.Bibliography
        obj["sourcePath"] <- stringNode artifact.SourcePath
        obj["url"] <- stringNode artifact.CanonicalUrl
        obj["legacyUrls"] <- stringArray artifact.LegacyUrls

        obj["rawFrontmatter"] <-
            artifact.FrontMatter.DeepClone()

        obj["unknownFrontmatter"] <-
            artifact.UnknownFrontMatter.DeepClone()

        let relationArray = JsonArray()

        for relationship in
            (relationships
             |> List.filter (fun relationship ->
                 relationship.SourceKey = artifact.Key)) do
            relationArray.Add(relationshipNode relationship)

        obj["relationships"] <- relationArray
        obj

    let toJson (compilation: Compilation) : string =
        let root = JsonObject()
        root["schemaVersion"] <- stringNode "2.0"

        let artifacts = JsonArray()

        for artifact in compilation.Artifacts do
            artifacts.Add(
                artifactNode compilation.Relationships artifact
            )

        root["artifacts"] <- artifacts

        let relationships = JsonArray()

        for relationship in compilation.Relationships do
            relationships.Add(relationshipNode relationship)

        root["relationships"] <- relationships

        let findings = JsonArray()

        for finding in compilation.Findings do
            findings.Add(findingNode finding)

        root["findings"] <- findings

        let capabilities = JsonArray()

        for capability in compilation.Capabilities do
            let obj = JsonObject()
            obj["name"] <- stringNode capability.Name
            obj["available"] <- boolNode capability.Available
            obj["reason"] <- stringNode capability.Reason
            capabilities.Add(obj)

        root["capabilities"] <- capabilities

        let redirects = JsonObject()

        for oldUrl, newUrl in compilation.Redirects do
            redirects[oldUrl] <- stringNode newUrl

        root["redirects"] <- redirects

        let options = JsonSerializerOptions()
        options.WriteIndented <- true
        options.TypeInfoResolver <- DefaultJsonTypeInfoResolver()
        root.ToJsonString(options)

    let compileRepository
        (repositoryRoot: string)
        : Compilation =
        let stateDirectory =
            Path.Combine(repositoryRoot, ".research-publisher")

        let inputPath =
            Path.Combine(stateDirectory, "semantic-input.json")

        let outputPath =
            Path.Combine(stateDirectory, "semantic-output.json")

        if not (File.Exists(inputPath)) then
            invalidOp
                (sprintf
                    "Semantic compiler input is missing: %s"
                    inputPath)

        let parsed =
            JsonNode.Parse(File.ReadAllText(inputPath))

        let root =
            match parsed with
            | :? JsonObject as value -> value
            | _ ->
                invalidOp
                    "Semantic compiler input root must be a JSON object."

        let rawDocuments =
            match root["documents"] with
            | :? JsonArray as values ->
                values
                |> Seq.choose rawDocumentFromNode
                |> Seq.toList
            | _ -> []

        let rawDerivedRelationships =
            match root["derivedRelationships"] with
            | :? JsonArray as values ->
                values
                |> Seq.choose rawDerivedRelationshipFromNode
                |> Seq.toList
            | _ -> []

        let compilation =
            Compiler.compileWithDerived
                rawDocuments
                rawDerivedRelationships

        Directory.CreateDirectory(stateDirectory)
        |> ignore

        File.WriteAllText(
            outputPath,
            toJson compilation
        )

        compilation
