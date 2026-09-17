namespace ResearchPublisher.Lifecycle.Core

open System
open System.IO
open System.Text
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Nodes

/// Minimal JSON plumbing.
///
/// Writing goes through `Utf8JsonWriter` and explicit field lists rather than
/// reflection-based serialization so the published schemas stay stable, stay
/// trimmable, and cannot drift when an internal record gains a field.
module Json =

    let private writerOptions indented =
        JsonWriterOptions(Indented = indented, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

    let private nodeOptions indented =
        JsonSerializerOptions(WriteIndented = indented, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

    let private documentOptions =
        JsonDocumentOptions(AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip)

    let private nodeDocumentOptions =
        JsonNodeOptions(PropertyNameCaseInsensitive = false)

    /// Render a document through an explicit writer callback.
    let write (indented: bool) (writeBody: Utf8JsonWriter -> unit) =
        use stream = new MemoryStream()
        (use writer = new Utf8JsonWriter(stream, writerOptions indented)
         writeBody writer
         writer.Flush())
        Encoding.UTF8.GetString(stream.ToArray())

    let writeString (writer: Utf8JsonWriter) (name: string) (value: string) =
        writer.WriteString(name, value)

    let writeOptionalString (writer: Utf8JsonWriter) (name: string) (value: string option) =
        match value with
        | Some text -> writer.WriteString(name, text)
        | None -> writer.WriteNull(name)

    let writeStringArray (writer: Utf8JsonWriter) (name: string) (values: string seq) =
        writer.WriteStartArray(name)
        for value in values do
            writer.WriteStringValue(value)
        writer.WriteEndArray()

    /// Write `name: [ ... ]` where each element is produced by `writeItem`.
    let writeArray (writer: Utf8JsonWriter) (name: string) (items: 'a seq) (writeItem: Utf8JsonWriter -> 'a -> unit) =
        writer.WriteStartArray(name)
        for item in items do
            writeItem writer item
        writer.WriteEndArray()

    /// Write `name: { ... }`.
    let writeObject (writer: Utf8JsonWriter) (name: string) (writeBody: Utf8JsonWriter -> unit) =
        writer.WriteStartObject(name)
        writeBody writer
        writer.WriteEndObject()

    let tryParse (text: string) =
        try
            Ok(JsonDocument.Parse(text, documentOptions))
        with :? JsonException as error ->
            Result.Error error.Message

    let tryParseNode (text: string) =
        try
            match JsonNode.Parse(text, nodeDocumentOptions, documentOptions) with
            | null -> Result.Error "The document is empty."
            | node -> Ok node
        with :? JsonException as error ->
            Result.Error error.Message

    let tryProperty (name: string) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            None
        else
            match element.TryGetProperty name with
            | true, value -> Some value
            | _ -> None

    let tryStringProperty (name: string) (element: JsonElement) =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
        | _ -> None

    let tryIntProperty (name: string) (element: JsonElement) =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt32() with
            | true, number -> Some number
            | _ -> None
        | _ -> None

    let arrayItems (element: JsonElement) =
        if element.ValueKind = JsonValueKind.Array then
            element.EnumerateArray() |> Seq.toList
        else
            []

    /// Serialize a mutable node tree, preserving property order.
    let renderNode (indented: bool) (node: JsonNode) =
        node.ToJsonString(nodeOptions indented)

    /// Guess the indentation width of an existing JSON document so rewriting it
    /// does not reformat a consuming repository's file.
    let detectIndentWidth (text: string) =
        let lines = text.Replace("\r\n", "\n").Split('\n')
        lines
        |> Array.tryPick (fun line ->
            let trimmed = line.TrimStart(' ')
            let indent = line.Length - trimmed.Length
            if indent > 0 && trimmed.StartsWith("\"", StringComparison.Ordinal) then Some indent else None)
        |> Option.defaultValue 2

    /// .NET 8's `Utf8JsonWriter` always indents with two spaces. Re-indent when the
    /// source document used a different width so diffs stay minimal.
    let reindent (width: int) (text: string) =
        if width = 2 then
            text
        else
            let unit = String(' ', width)
            text.Split('\n')
            |> Array.map (fun line ->
                let trimmed = line.TrimStart(' ')
                let depth = (line.Length - trimmed.Length) / 2
                if depth = 0 then line else String.replicate depth unit + trimmed)
            |> String.concat "\n"
