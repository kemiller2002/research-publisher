namespace ResearchPublisher.Semantics.Core

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Text

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

    let private writeOptionalString
        (writer: Utf8JsonWriter)
        (name: string)
        (value: string option)
        =
        match value with
        | Some text -> writer.WriteString(name, text)
        | None -> writer.WriteNull(name)

    let private writeOptionalNumber
        (writer: Utf8JsonWriter)
        (name: string)
        (value: float option)
        =
        match value with
        | Some number -> writer.WriteNumber(name, number)
        | None -> writer.WriteNull(name)

    let private writeStringArray
        (writer: Utf8JsonWriter)
        (name: string)
        (values: string list)
        =
        writer.WriteStartArray(name)

        for value in values do
            writer.WriteStringValue(value)

        writer.WriteEndArray()

    let private writeFinding
        (writer: Utf8JsonWriter)
        (finding: Finding)
        =
        writer.WriteStartObject()
        writer.WriteString("code", finding.Code)
        writer.WriteString("severity", Severity.toWire finding.Severity)
        writer.WriteString("sourcePath", finding.SourcePath)
        writeOptionalString writer "frontMatterKey" finding.FrontMatterKey
        writer.WriteString("message", finding.Message)
        writeOptionalString writer "remedy" finding.Remedy
        writer.WriteEndObject()

    let private writeRelationship
        (writer: Utf8JsonWriter)
        (relationship: Relationship)
        =
        writer.WriteStartObject()
        writer.WriteString("sourceKey", relationship.SourceKey)
        writer.WriteString("sourcePath", relationship.SourcePath)
        writeOptionalString writer "rawSource" relationship.RawSource
        writeOptionalString writer "evidenceSource" relationship.EvidenceSource
        writer.WriteString("field", relationship.Field)
        writer.WriteString("relation", relationship.Relation)
        writer.WriteString("authority", RelationAuthority.toWire relationship.Authority)
        writer.WriteString("rawTarget", relationship.RawTarget)
        writer.WriteString("referenceKind", ReferenceKind.toWire relationship.ReferenceKind)
        writer.WriteString("resolution", ResolutionStatus.toWire relationship.Resolution)
        writeOptionalString writer "targetKey" relationship.TargetKey
        writeOptionalString writer "targetId" relationship.TargetId
        writeOptionalString writer "targetSourcePath" relationship.TargetSourcePath
        writeOptionalString writer "targetTitle" relationship.TargetTitle
        writer.WriteEndObject()

    let private writeArtifact
        (writer: Utf8JsonWriter)
        (relationships: Relationship list)
        (artifact: Artifact)
        =
        writer.WriteStartObject()
        writer.WriteString("key", artifact.Key)
        writer.WriteString("keyKind", artifact.KeyKind)
        writeOptionalString writer "id" artifact.DeclaredId
        writer.WriteString("title", artifact.Title)
        writer.WriteString("titleSource", artifact.TitleSource)
        writeOptionalString writer "artifactType" artifact.ArtifactType
        writer.WriteString("typeSource", artifact.TypeSource)
        writeOptionalString writer "project" artifact.Project
        writeStringArray writer "purposes" artifact.Purposes
        writeStringArray writer "audiences" artifact.Audiences
        writer.WriteBoolean("entryPoint", artifact.EntryPoint)
        writeOptionalNumber writer "entryPointOrder" artifact.EntryPointOrder
        writeOptionalString writer "entryPointLabel" artifact.EntryPointLabel
        writeOptionalString writer "researchArea" artifact.ResearchArea
        writeStringArray writer "discipline" artifact.Discipline
        writeOptionalString writer "summary" artifact.Summary
        writeOptionalString writer "status" artifact.Status
        writeOptionalString writer "version" artifact.Version
        writeOptionalNumber writer "confidence" artifact.Confidence
        writeOptionalNumber writer "completion" artifact.Completion
        writeOptionalString writer "priority" artifact.Priority
        writeOptionalString writer "authorAgent" artifact.AuthorAgent
        writeOptionalString writer "created" artifact.Created
        writeOptionalString writer "updated" artifact.Updated
        writeStringArray writer "tags" artifact.Tags
        writeStringArray writer "keywords" artifact.Keywords
        writeStringArray writer "relatedProjects" artifact.RelatedProjects
        writeStringArray writer "bibliography" artifact.Bibliography
        writer.WriteString("sourcePath", artifact.SourcePath)
        writer.WriteString("url", artifact.CanonicalUrl)
        writeStringArray writer "legacyUrls" artifact.LegacyUrls

        writer.WritePropertyName("rawFrontmatter")
        artifact.FrontMatter.WriteTo(writer)

        writer.WritePropertyName("unknownFrontmatter")
        artifact.UnknownFrontMatter.WriteTo(writer)

        writer.WriteStartArray("relationships")

        for relationship in relationships do
            if relationship.SourceKey = artifact.Key then
                writeRelationship writer relationship

        writer.WriteEndArray()
        writer.WriteEndObject()

    let toJson (compilation: Compilation) : string =
        use stream = new MemoryStream()
        let writerOptions = JsonWriterOptions(Indented = true)
        use writer = new Utf8JsonWriter(stream, writerOptions)

        writer.WriteStartObject()
        writer.WriteString("schemaVersion", "2.0")

        writer.WriteStartArray("artifacts")

        for artifact in compilation.Artifacts do
            writeArtifact writer compilation.Relationships artifact

        writer.WriteEndArray()

        writer.WriteStartArray("relationships")

        for relationship in compilation.Relationships do
            writeRelationship writer relationship

        writer.WriteEndArray()

        writer.WriteStartArray("findings")

        for finding in compilation.Findings do
            writeFinding writer finding

        writer.WriteEndArray()

        writer.WriteStartArray("capabilities")

        for capability in compilation.Capabilities do
            writer.WriteStartObject()
            writer.WriteString("name", capability.Name)
            writer.WriteBoolean("available", capability.Available)
            writer.WriteString("reason", capability.Reason)
            writer.WriteEndObject()

        writer.WriteEndArray()

        writer.WriteStartObject("redirects")

        for oldUrl, newUrl in compilation.Redirects do
            writer.WriteString(oldUrl, newUrl)

        writer.WriteEndObject()
        writer.WriteEndObject()
        writer.Flush()

        Encoding.UTF8.GetString(stream.ToArray())

    let private compileRoot (root: JsonObject) : Compilation =
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

        Compiler.compileWithDerived
            rawDocuments
            rawDerivedRelationships

    let compileText (text: string) : string =
        let parsed = JsonNode.Parse(text)

        let root =
            match parsed with
            | :? JsonObject as value -> value
            | _ ->
                invalidOp
                    "Semantic compiler input root must be a JSON object."

        compileRoot root |> toJson

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

        let compilation = compileRoot root

        Directory.CreateDirectory(stateDirectory)
        |> ignore

        File.WriteAllText(
            outputPath,
            toJson compilation
        )

        compilation
