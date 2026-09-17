namespace ResearchPublisher.Lifecycle.Cli

open System.Text.Json
open ResearchPublisher.Lifecycle.Core

/// The machine-readable interface.
///
/// Every document is a versioned envelope written from an explicit field list, so
/// the schema is a deliberate decision rather than a side effect of an internal
/// record shape. When `--json` is used, stdout contains exactly one JSON document
/// and nothing else; human-facing text goes to stderr.
module JsonOutput =

    [<Literal>]
    let StatusSchema = "research-publisher.status/1"

    [<Literal>]
    let VerifySchema = "research-publisher.verify/1"

    [<Literal>]
    let DoctorSchema = "research-publisher.doctor/1"

    [<Literal>]
    let PlanSchema = "research-publisher.plan/1"

    [<Literal>]
    let ResultSchema = "research-publisher.result/1"

    [<Literal>]
    let ErrorSchema = "research-publisher.error/1"

    let private envelope (writer: Utf8JsonWriter) schema cliVersion =
        Json.writeString writer "schema" schema
        Json.writeString writer "tool" Identity.ToolName
        Json.writeString writer "package" Identity.PackageName
        Json.writeString writer "cliVersion" cliVersion

    let private writeCheck (writer: Utf8JsonWriter) (check: Check) =
        writer.WriteStartObject()
        Json.writeString writer "id" check.Id
        Json.writeString writer "title" check.Title
        Json.writeString writer "status" (CheckStatus.toWire check.Status)
        Json.writeString writer "detail" check.Detail
        Json.writeOptionalString writer "path" check.Path
        Json.writeOptionalString writer "remediation" check.Remediation
        writer.WriteEndObject()

    let private writeProblem (writer: Utf8JsonWriter) (problem: Problem) =
        writer.WriteStartObject()
        Json.writeString writer "code" problem.Code
        Json.writeString writer "severity" (Severity.toWire problem.Severity)
        Json.writeString writer "title" problem.Title
        Json.writeString writer "detail" problem.Detail
        Json.writeOptionalString writer "path" problem.Path
        Json.writeOptionalString writer "remediation" problem.Remediation
        writer.WriteEndObject()

    let private ownershipOf change =
        match change with
        | CreateFile (_, ownership, _)
        | UpdateManagedFile (_, ownership, _) -> Some(Ownership.toWire ownership)
        | _ -> None

    let private writeChange (writer: Utf8JsonWriter) (step: PlanStep) =
        writer.WriteStartObject()
        Json.writeString writer "kind" (PlannedChange.kind step.Change)
        Json.writeString writer "target" (PlannedChange.target step.Change)
        Json.writeString writer "description" (PlannedChange.describe step.Change)
        Json.writeString writer "reason" step.Reason
        Json.writeOptionalString writer "ownership" (ownershipOf step.Change)
        writer.WriteEndObject()

    let private writeSkipped (writer: Utf8JsonWriter) (skipped: SkippedStep) =
        writer.WriteStartObject()
        Json.writeString writer "target" skipped.Target
        Json.writeString writer "reason" skipped.Reason
        writer.WriteEndObject()

    let private writeConflict (writer: Utf8JsonWriter) (conflict: PlanConflict) =
        writer.WriteStartObject()
        Json.writeString writer "target" conflict.Target
        Json.writeString writer "ownership" (Ownership.toWire conflict.Ownership)
        Json.writeString writer "detail" conflict.Detail
        Json.writeString writer "resolution" conflict.Resolution
        writer.WriteEndObject()

    let private writeMigration (writer: Utf8JsonWriter) (id: MigrationId) =
        writer.WriteStartObject()
        writer.WriteNumber("from", id.FromVersion)
        writer.WriteNumber("to", id.ToVersion)
        writer.WriteEndObject()

    let private writeTarget (writer: Utf8JsonWriter) (name: string) (target: TargetVersion) =
        Json.writeObject writer name (fun writer ->
            Json.writeString writer "toolVersion" target.ToolVersion
            writer.WriteNumber("configurationVersion", target.ConfigurationVersion))

    let status (summary: StatusSummary) (exitCode: int) =
        Json.write true (fun writer ->
            writer.WriteStartObject()
            envelope writer StatusSchema summary.CliVersion
            Json.writeString writer "repositoryRoot" summary.RepositoryRoot
            Json.writeString writer "state" (InstallationState.toWire summary.State)
            Json.writeString writer "stateDescription" (InstallationState.describe summary.State)
            Json.writeOptionalString writer "installedVersion" summary.InstalledVersion

            match summary.ConfigurationVersion with
            | Some version -> writer.WriteNumber("configurationVersion", version)
            | None -> writer.WriteNull "configurationVersion"

            writer.WriteNumber("currentConfigurationVersion", Identity.CurrentConfigurationVersion)
            Json.writeString writer "configuration" summary.ConfigurationStatus
            Json.writeString writer "artifacts" summary.ArtifactStatus
            Json.writeString writer "integration" summary.IntegrationStatus

            Json.writeObject writer "verification" (fun writer ->
                writer.WriteBoolean("passed", summary.Verification.Passed)
                writer.WriteBoolean("strict", summary.Verification.Strict)
                Json.writeArray writer "checks" summary.Verification.Checks writeCheck)

            match summary.UpgradeAvailable with
            | Some target -> writeTarget writer "upgradeAvailable" target
            | None -> writer.WriteNull "upgradeAvailable"

            Json.writeString writer "manifestPath" Identity.ManifestPath
            writer.WriteNumber("exitCode", exitCode)
            writer.WriteEndObject())

    let verification (cliVersion: string) (repositoryRoot: string) (report: VerificationReport) (exitCode: int) =
        Json.write true (fun writer ->
            writer.WriteStartObject()
            envelope writer VerifySchema cliVersion
            Json.writeString writer "repositoryRoot" repositoryRoot
            writer.WriteBoolean("strict", report.Strict)
            writer.WriteBoolean("passed", report.Passed)
            Json.writeArray writer "checks" report.Checks writeCheck
            writer.WriteNumber("exitCode", exitCode)
            writer.WriteEndObject())

    let diagnostics (cliVersion: string) (repositoryRoot: string) (strict: bool) (report: DiagnosticReport) (exitCode: int) =
        let countOf severity =
            report.Diagnostics |> List.filter (fun item -> item.Severity = severity) |> List.length

        Json.write true (fun writer ->
            writer.WriteStartObject()
            envelope writer DoctorSchema cliVersion
            Json.writeString writer "repositoryRoot" repositoryRoot
            writer.WriteBoolean("strict", strict)
            writer.WriteBoolean("healthy", report.Healthy)

            Json.writeObject writer "counts" (fun writer ->
                writer.WriteNumber("error", countOf Error)
                writer.WriteNumber("warning", countOf Warning)
                writer.WriteNumber("information", countOf Information))

            Json.writeArray writer "diagnostics" report.Diagnostics writeProblem
            writer.WriteNumber("exitCode", exitCode)
            writer.WriteEndObject())

    let private writePlanBody (writer: Utf8JsonWriter) (plan: Plan) =
        Json.writeString writer "operation" (OperationKind.toWire plan.Operation)
        Json.writeString writer "repositoryRoot" plan.RepositoryRoot
        Json.writeString writer "fromState" (InstallationState.toWire plan.FromState)
        Json.writeString writer "fromStateDescription" (InstallationState.describe plan.FromState)
        writeTarget writer "target" plan.Target
        Json.writeArray writer "migrations" plan.Migrations writeMigration
        writer.WriteNumber("changeCount", List.length plan.Steps)
        Json.writeArray writer "changes" plan.Steps writeChange
        Json.writeArray writer "skipped" plan.Skipped writeSkipped
        Json.writeArray writer "conflicts" plan.Conflicts writeConflict
        Json.writeArray writer "blockers" plan.Blockers writeProblem

    let plan (cliVersion: string) (dryRun: bool) (plan: Plan) (exitCode: int) =
        Json.write true (fun writer ->
            writer.WriteStartObject()
            envelope writer PlanSchema cliVersion
            writer.WriteBoolean("dryRun", dryRun)
            writer.WriteBoolean("executable", Plan.isExecutable plan)
            writePlanBody writer plan
            writer.WriteNumber("exitCode", exitCode)
            writer.WriteEndObject())

    let private outcomeWire outcome =
        match outcome with
        | Applied -> "applied"
        | NotAttempted _ -> "not-attempted"
        | Failed _ -> "failed"

    let executionResult (cliVersion: string) (result: ExecutionResult) (exitCode: int) =
        Json.write true (fun writer ->
            writer.WriteStartObject()
            envelope writer ResultSchema cliVersion
            writer.WriteBoolean("succeeded", result.Succeeded)
            writer.WriteBoolean("changed", not (List.isEmpty result.Plan.Steps))
            writePlanBody writer result.Plan

            Json.writeArray writer "applied" result.Changes (fun writer change ->
                writer.WriteStartObject()
                Json.writeString writer "kind" (PlannedChange.kind change.Change)
                Json.writeString writer "target" (PlannedChange.target change.Change)
                Json.writeString writer "description" (PlannedChange.describe change.Change)
                Json.writeString writer "outcome" (outcomeWire change.Outcome)

                match change.Outcome with
                | NotAttempted reason
                | Failed reason -> Json.writeString writer "reason" reason
                | Applied -> ()

                writer.WriteEndObject())

            Json.writeArray writer "failures" result.Failures writeProblem
            writer.WriteNumber("exitCode", exitCode)
            writer.WriteEndObject())

    let failure (cliVersion: string) (code: string) (message: string) (exitCode: int) =
        Json.write true (fun writer ->
            writer.WriteStartObject()
            envelope writer ErrorSchema cliVersion
            Json.writeString writer "code" code
            Json.writeString writer "message" message
            writer.WriteNumber("exitCode", exitCode)
            writer.WriteEndObject())
