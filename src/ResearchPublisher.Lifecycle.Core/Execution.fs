namespace ResearchPublisher.Lifecycle.Core

open System
open System.IO
open System.Text.Json.Nodes

type ChangeOutcome =
    | Applied
    | NotAttempted of reason: string
    | Failed of reason: string

type ExecutedChange =
    { Change: PlannedChange
      Outcome: ChangeOutcome }

type ExecutionResult =
    { Plan: Plan
      Changes: ExecutedChange list
      /// True only when every planned change was applied.
      Succeeded: bool
      /// Populated when the run stopped part-way; says exactly what was done.
      Failures: Problem list }

module Execution =

    /// A file write that has already been validated and resolved to an absolute path.
    type private StagedWrite =
        { RelativePath: string
          AbsolutePath: string
          Contents: string }

    let private resolveAll (root: string) (plan: Plan) =
        let errors = ResizeArray<Problem>()

        let resolve relativePath =
            match RepositoryPath.resolve root relativePath with
            | Ok absolute -> Some absolute
            | Result.Error message ->
                errors.Add(
                    Problem.create "unsafe-path" Error "A planned change targets an unsafe path." message
                    |> Problem.withPath relativePath
                )

                None

        let resolved =
            plan.Steps
            |> List.map (fun step ->
                match step.Change with
                | CreateDirectory path -> step, resolve path
                | CreateFile (path, _, _) -> step, resolve path
                | UpdateManagedFile (path, _, _) -> step, resolve path
                | WriteManifest _ -> step, resolve Identity.ManifestPath
                | AddPackageScript _
                | RunMigration _ -> step, None)

        resolved, List.ofSeq errors

    /// Add scripts to package.json in one read-modify-write, preserving the
    /// repository's key order, indentation and any script it already defines.
    let private applyScriptAdditions (packageJsonPath: string) (additions: (string * string) list) =
        if List.isEmpty additions then
            Ok []
        else
            match FileSystem.tryReadText packageJsonPath with
            | Result.Error message -> Result.Error message
            | Ok None -> Result.Error "package.json disappeared while the plan was running."
            | Ok (Some text) ->
                match Json.tryParseNode text with
                | Result.Error message -> Result.Error message
                | Ok node ->
                    match node with
                    | :? JsonObject as root ->
                        let scripts =
                            match root.["scripts"] with
                            | :? JsonObject as existing -> existing
                            | _ ->
                                let created = JsonObject()
                                root.["scripts"] <- created
                                created

                        let mutable added = []

                        for (name, command) in additions do
                            if isNull scripts.[name] then
                                scripts.[name] <- JsonValue.Create(command)
                                added <- added @ [ name ]

                        if List.isEmpty added then
                            Ok []
                        else
                            let indent = Json.detectIndentWidth text
                            let rendered = Json.renderNode true (root :> JsonNode)
                            FileSystem.writeTextAtomic packageJsonPath (Json.reindent indent rendered + "\n")
                            Ok added
                    | _ -> Result.Error "package.json does not contain a JSON object."

    /// Apply a plan.
    ///
    /// Everything is validated before anything is written. Writes go through a
    /// sibling temporary file and a rename, and the installation manifest is written
    /// last so an interrupted run leaves a repository that `init` can finish.
    let execute (plan: Plan) : ExecutionResult =
        let notAttempted reason =
            { Plan = plan
              Changes = [ for step in plan.Steps -> { Change = step.Change; Outcome = NotAttempted reason } ]
              Succeeded = false
              Failures = plan.Blockers }

        if not (Plan.isExecutable plan) then
            notAttempted "The plan has unresolved blockers."
        else

        let root = plan.RepositoryRoot
        let resolved, resolutionErrors = resolveAll root plan

        if not (List.isEmpty resolutionErrors) then
            { notAttempted "A planned change could not be resolved to a safe path." with
                Failures = resolutionErrors }
        else

        let outcomes = ResizeArray<ExecutedChange>()
        let failures = ResizeArray<Problem>()
        let mutable halted = false

        let record change outcome = outcomes.Add { Change = change; Outcome = outcome }

        let fail change code title detail =
            failures.Add(
                Problem.create code Error title detail
                |> Problem.withPath (PlannedChange.target change)
            )

            record change (Failed detail)
            halted <- true

        // Directories and files first, manifest last.
        let ordered =
            let rank (step: PlanStep, _) =
                match step.Change with
                | CreateDirectory _ -> 0
                | RunMigration _ -> 1
                | CreateFile _
                | UpdateManagedFile _ -> 2
                | AddPackageScript _ -> 3
                | WriteManifest _ -> 4

            resolved |> List.sortBy rank

        let scriptAdditions =
            ordered
            |> List.choose (fun (step, _) ->
                match step.Change with
                | AddPackageScript (name, command) -> Some(name, command)
                | _ -> None)

        let mutable scriptsApplied = false

        for (step, absolutePath) in ordered do
            if halted then
                record step.Change (NotAttempted "An earlier change failed.")
            else
                match step.Change, absolutePath with
                | RunMigration _, _ ->
                    // A marker in the plan; the migration's effects are the steps
                    // that follow it.
                    record step.Change Applied
                | CreateDirectory _, Some path ->
                    try
                        FileSystem.ensureDirectory path
                        record step.Change Applied
                    with error ->
                        fail step.Change "directory-create-failed" "A directory could not be created." error.Message
                | CreateFile (_, _, contents), Some path
                | UpdateManagedFile (_, _, contents), Some path ->
                    try
                        FileSystem.writeTextAtomic path contents
                        record step.Change Applied
                    with error ->
                        fail step.Change "file-write-failed" "A file could not be written." error.Message
                | AddPackageScript _, _ ->
                    if scriptsApplied then
                        record step.Change Applied
                    else
                        match applyScriptAdditions (Path.Combine(root, "package.json")) scriptAdditions with
                        | Ok _ ->
                            scriptsApplied <- true
                            record step.Change Applied
                        | Result.Error message ->
                            fail step.Change "package-json-update-failed" "package.json could not be updated." message
                | WriteManifest manifest, Some path ->
                    try
                        FileSystem.writeTextAtomic path (Manifest.render manifest)
                        record step.Change Applied
                    with error ->
                        fail
                            step.Change
                            "manifest-write-failed"
                            "The installation manifest could not be written."
                            error.Message
                | change, None ->
                    fail change "unresolved-path" "A planned change had no resolved path." "Internal planning error."

        { Plan = plan
          Changes = List.ofSeq outcomes
          Succeeded = not halted
          Failures = List.ofSeq failures }
