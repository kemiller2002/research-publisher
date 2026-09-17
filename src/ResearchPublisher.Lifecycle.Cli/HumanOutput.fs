namespace ResearchPublisher.Lifecycle.Cli

open System
open ResearchPublisher.Lifecycle.Core

/// Human-facing rendering. Normal runs stay short; detail lives behind `--verbose`.
module HumanOutput =

    let private label (name: string) (value: string) = sprintf "  %-22s %s" (name + ":") value

    let private statusMark status =
        match status with
        | Pass -> "ok  "
        | Warn -> "warn"
        | Fail -> "FAIL"
        | Skipped -> "skip"

    let private severityMark severity =
        match severity with
        | Error -> "ERROR"
        | Warning -> "WARN "
        | Information -> "INFO "

    let status (verbose: bool) (summary: StatusSummary) =
        let verificationLine =
            if summary.Verification.Passed then "passed" else "failed"

        let upgradeLine =
            match summary.UpgradeAvailable with
            | Some target -> sprintf "%s available" target.ToolVersion
            | None -> "up to date"

        let checks =
            if verbose then
                [ yield ""
                  yield "  Checks"
                  for check in summary.Verification.Checks ->
                      sprintf "    [%s] %s - %s" (statusMark check.Status) check.Title check.Detail ]
            else
                summary.Verification.Checks
                |> List.filter (fun check -> check.Status = Fail)
                |> function
                    | [] -> []
                    | failures ->
                        [ yield ""
                          yield "  Failing checks"
                          for check in failures -> sprintf "    [FAIL] %s - %s" check.Title check.Detail ]

        String.concat
            "\n"
            [ yield sprintf "%s (%s)" Identity.ToolName Identity.PackageName
              yield ""
              yield label "CLI version" summary.CliVersion
              yield
                  label
                      "Installed version"
                      (summary.InstalledVersion |> Option.defaultValue "not recorded")
              yield
                  label
                      "Configuration"
                      (match summary.ConfigurationVersion with
                       | Some version ->
                           sprintf "%s (version %d of %d)" summary.ConfigurationStatus version Identity.CurrentConfigurationVersion
                       | None -> summary.ConfigurationStatus)
              yield label "Installation" (InstallationState.describe summary.State)
              yield label "Required artifacts" summary.ArtifactStatus
              yield label "Integration" summary.IntegrationStatus
              yield label "Verification" verificationLine
              yield label "Upgrade" upgradeLine
              yield! checks
              yield "" ]

    let verification (verbose: bool) (report: VerificationReport) =
        let shown =
            if verbose then
                report.Checks
            else
                report.Checks |> List.filter (fun check -> check.Status <> Pass)

        let lines =
            [ for check in shown do
                  yield sprintf "  [%s] %s" (statusMark check.Status) check.Title
                  yield sprintf "         %s" check.Detail

                  match check.Path with
                  | Some path -> yield sprintf "         path: %s" path
                  | None -> ()

                  match check.Remediation with
                  | Some remediation when check.Status <> Pass -> yield sprintf "         fix:  %s" remediation
                  | _ -> () ]

        let headline =
            if report.Passed then
                sprintf
                    "Verification passed (%d checks%s)."
                    (List.length report.Checks)
                    (if report.Strict then ", strict" else "")
            else
                sprintf
                    "Verification failed (%d of %d checks%s)."
                    (report.Checks |> List.filter (fun check -> check.Status = Fail) |> List.length)
                    (List.length report.Checks)
                    (if report.Strict then ", strict" else "")

        String.concat "\n" [ yield headline; yield! lines; yield "" ]

    let diagnostics (verbose: bool) (report: DiagnosticReport) =
        let shown =
            if verbose then
                report.Diagnostics
            else
                report.Diagnostics |> List.filter (fun item -> item.Severity <> Information)

        let lines =
            [ for item in shown do
                  yield sprintf "  [%s] %s" (severityMark item.Severity) item.Title
                  yield sprintf "          %s" item.Detail

                  match item.Path with
                  | Some path -> yield sprintf "          path: %s" path
                  | None -> ()

                  match item.Remediation with
                  | Some remediation -> yield sprintf "          fix:  %s" remediation
                  | None -> () ]

        let counts severity =
            report.Diagnostics |> List.filter (fun item -> item.Severity = severity) |> List.length

        let headline =
            sprintf
                "%d error(s), %d warning(s), %d informational."
                (counts Error)
                (counts Warning)
                (counts Information)

        let hint =
            if not verbose && List.length shown < List.length report.Diagnostics then
                [ "  Run with --verbose to see informational diagnostics." ]
            else
                []

        String.concat "\n" [ yield headline; yield! lines; yield! hint; yield "" ]

    let private changeLines (plan: Plan) =
        [ for step in plan.Steps do
              match step.Change with
              | RunMigration (id, title) -> yield sprintf "  migration %s  %s" (MigrationId.describe id) title
              | change -> yield sprintf "  %s" (PlannedChange.describe change) ]

    let private extraLines (verbose: bool) (plan: Plan) =
        [ if verbose && not (List.isEmpty plan.Skipped) then
              yield ""
              yield "Unchanged:"

              for skipped in plan.Skipped do
                  yield sprintf "  %s - %s" skipped.Target skipped.Reason

          if not (List.isEmpty plan.Conflicts) then
              yield ""
              yield "Conflicts (nothing was overwritten):"

              for conflict in plan.Conflicts do
                  yield sprintf "  %s" conflict.Target
                  yield sprintf "    %s" conflict.Detail
                  yield sprintf "    %s" conflict.Resolution

          if not (List.isEmpty plan.Blockers) then
              yield ""
              yield "Blocked:"

              for blocker in plan.Blockers do
                  yield sprintf "  %s" blocker.Title
                  yield sprintf "    %s" blocker.Detail

                  match blocker.Remediation with
                  | Some remediation -> yield sprintf "    fix: %s" remediation
                  | None -> () ]

    let plan (verbose: bool) (dryRun: bool) (plan: Plan) =
        let operation =
            match plan.Operation with
            | Initialize -> "init"
            | Upgrade -> "upgrade"

        let headline =
            if not (Plan.isExecutable plan) then
                sprintf "%s cannot run." operation
            elif List.isEmpty plan.Steps then
                sprintf "%s: no changes needed." operation
            elif dryRun then
                sprintf "%s --dry-run: %d change(s) would be made." operation (List.length plan.Steps)
            else
                sprintf "%s: %d change(s) planned." operation (List.length plan.Steps)

        String.concat "\n" [ yield headline; yield! changeLines plan; yield! extraLines verbose plan; yield "" ]

    let executionResult (verbose: bool) (result: ExecutionResult) =
        let applied =
            result.Changes |> List.filter (fun change -> change.Outcome = Applied)

        let headline =
            if not result.Succeeded then
                "The operation stopped before completing."
            elif List.isEmpty result.Plan.Steps then
                sprintf
                    "%s: no changes needed."
                    (match result.Plan.Operation with
                     | Initialize -> "init"
                     | Upgrade -> "upgrade")
            else
                sprintf "Applied %d change(s)." (List.length applied)

        let appliedLines =
            [ for change in applied do
                  match change.Change with
                  | RunMigration (id, title) -> yield sprintf "  migration %s  %s" (MigrationId.describe id) title
                  | plannedChange -> yield sprintf "  %s" (PlannedChange.describe plannedChange) ]

        let failureLines =
            [ if not (List.isEmpty result.Failures) then
                  yield ""
                  yield "Failures:"

                  for failure in result.Failures do
                      yield sprintf "  %s" failure.Title
                      yield sprintf "    %s" failure.Detail

              let unattempted =
                  result.Changes
                  |> List.choose (fun change ->
                      match change.Outcome with
                      | NotAttempted reason -> Some(PlannedChange.target change.Change, reason)
                      | _ -> None)

              if not (List.isEmpty unattempted) then
                  yield ""
                  yield "Not attempted:"

                  for (target, reason) in unattempted do
                      yield sprintf "  %s - %s" target reason ]

        String.concat
            "\n"
            [ yield headline
              yield! appliedLines
              yield! extraLines verbose result.Plan
              yield! failureLines
              yield "" ]
