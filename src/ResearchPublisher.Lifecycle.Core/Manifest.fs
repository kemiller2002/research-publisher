namespace ResearchPublisher.Lifecycle.Core

open System
open System.Text.Json

/// One entry of the installation record.
type ManifestArtifact =
    { Id: string
      Path: string
      Ownership: Ownership
      /// Content hash captured when the tool last wrote or adopted the file.
      /// Absent for directories and for files the tool never wrote.
      Hash: string option }

type ManifestScript =
    { Name: string
      Command: string }

/// The machine-readable installation record stored at `.echelon/research-publisher.json`.
///
/// Deliberately excluded: secrets, tokens, absolute paths, machine identifiers and
/// timestamps. Nothing here is time-sensitive, which is what lets `init` be
/// byte-for-byte idempotent.
type Manifest =
    { Schema: string
      Tool: string
      Package: string
      InstalledVersion: string
      ConfigurationVersion: int
      ManagedArtifacts: ManifestArtifact list
      ManagedScripts: ManifestScript list }

module Manifest =

    let empty version configurationVersion =
        { Schema = Identity.ManifestSchema
          Tool = Identity.ToolName
          Package = Identity.PackageName
          InstalledVersion = version
          ConfigurationVersion = configurationVersion
          ManagedArtifacts = []
          ManagedScripts = [] }

    let tryFindArtifact id manifest =
        manifest.ManagedArtifacts |> List.tryFind (fun artifact -> artifact.Id = id)

    let render (manifest: Manifest) =
        let body (writer: Utf8JsonWriter) =
            writer.WriteStartObject()
            Json.writeString writer "schema" manifest.Schema
            Json.writeString writer "tool" manifest.Tool
            Json.writeString writer "package" manifest.Package
            Json.writeString writer "installedVersion" manifest.InstalledVersion
            writer.WriteNumber("configurationVersion", manifest.ConfigurationVersion)

            Json.writeArray writer "managedArtifacts" manifest.ManagedArtifacts (fun writer artifact ->
                writer.WriteStartObject()
                Json.writeString writer "id" artifact.Id
                Json.writeString writer "path" artifact.Path
                Json.writeString writer "ownership" (Ownership.toWire artifact.Ownership)
                match artifact.Hash with
                | Some hash -> Json.writeString writer "hash" hash
                | None -> ()
                writer.WriteEndObject())

            Json.writeArray writer "managedScripts" manifest.ManagedScripts (fun writer script ->
                writer.WriteStartObject()
                Json.writeString writer "name" script.Name
                Json.writeString writer "command" script.Command
                writer.WriteEndObject())

            writer.WriteEndObject()

        Json.write true body + "\n"

    /// Parse a manifest, reporting why it is unusable rather than throwing.
    let parse (text: string) : Result<Manifest, Problem> =
        let invalid detail =
            Problem.create "manifest-unreadable" Error "The installation manifest is not valid." detail
            |> Problem.withPath Identity.ManifestPath
            |> Problem.withRemediation (
                sprintf "Delete %s and run `npx %s init` to rebuild it." Identity.ManifestPath Identity.PackageName
            )

        match Json.tryParse text with
        | Result.Error message -> Result.Error(invalid message)
        | Ok document ->
            use document = document
            let root = document.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                Result.Error(invalid "The manifest root must be a JSON object.")
            else

            let schema = Json.tryStringProperty "schema" root
            let tool = Json.tryStringProperty "tool" root
            let installedVersion = Json.tryStringProperty "installedVersion" root
            let configurationVersion = Json.tryIntProperty "configurationVersion" root

            match schema, tool, installedVersion, configurationVersion with
            | None, _, _, _ -> Result.Error(invalid "The manifest is missing the 'schema' field.")
            | Some schema, _, _, _ when schema <> Identity.ManifestSchema ->
                Result.Error(
                    Problem.create
                        "manifest-schema-unsupported"
                        Error
                        "The installation manifest uses an unsupported schema."
                        (sprintf "Found '%s'; this release understands '%s'." schema Identity.ManifestSchema)
                    |> Problem.withPath Identity.ManifestPath
                    |> Problem.withRemediation "Upgrade the tool, or delete the manifest and re-run `init`."
                )
            | _, Some tool, _, _ when tool <> Identity.ToolName ->
                Result.Error(invalid (sprintf "The manifest belongs to the '%s' tool." tool))
            | _, _, None, _ -> Result.Error(invalid "The manifest is missing the 'installedVersion' field.")
            | _, _, _, None -> Result.Error(invalid "The manifest is missing a numeric 'configurationVersion' field.")
            | Some schema, _, Some installedVersion, Some configurationVersion ->
                let artifacts =
                    Json.tryProperty "managedArtifacts" root
                    |> Option.map Json.arrayItems
                    |> Option.defaultValue []
                    |> List.choose (fun element ->
                        match Json.tryStringProperty "id" element, Json.tryStringProperty "path" element with
                        | Some id, Some path ->
                            let ownership =
                                Json.tryStringProperty "ownership" element
                                |> Option.bind Ownership.ofWire
                                |> Option.defaultValue ToolOwned

                            Some
                                { Id = id
                                  Path = RepositoryPath.normalize path
                                  Ownership = ownership
                                  Hash = Json.tryStringProperty "hash" element }
                        | _ -> None)

                let scripts =
                    Json.tryProperty "managedScripts" root
                    |> Option.map Json.arrayItems
                    |> Option.defaultValue []
                    |> List.choose (fun element ->
                        match Json.tryStringProperty "name" element, Json.tryStringProperty "command" element with
                        | Some name, Some command -> Some { Name = name; Command = command }
                        | _ -> None)

                Ok
                    { Schema = schema
                      Tool = Identity.ToolName
                      Package =
                        Json.tryStringProperty "package" root
                        |> Option.defaultValue Identity.PackageName
                      InstalledVersion = installedVersion
                      ConfigurationVersion = configurationVersion
                      ManagedArtifacts = artifacts
                      ManagedScripts = scripts }
