namespace ResearchPublisher.Lifecycle.Core

/// Identity of the capability this package installs into a repository.
module Identity =

    [<Literal>]
    let ToolName = "research-publisher"

    [<Literal>]
    let PackageName = "@echelon-foundry/research-publisher"

    [<Literal>]
    let ExecutableName = "research-publisher"

    /// Directory shared by every Echelon Foundry tool inside a consuming repository.
    [<Literal>]
    let EchelonDirectory = ".echelon"

    /// Repository-relative path of this tool's installation manifest.
    [<Literal>]
    let ManifestPath = ".echelon/research-publisher.json"

    /// Schema identifier of the installation manifest document.
    [<Literal>]
    let ManifestSchema = "echelon.tool-installation/1"

    /// Configuration version written by the current release.
    [<Literal>]
    let CurrentConfigurationVersion = 2

    /// Lowest configuration version this release knows how to migrate from.
    /// Zero means "installed before manifests existed".
    [<Literal>]
    let LowestSupportedConfigurationVersion = 0

/// Stable process exit codes. These are a public contract; see docs/cli.md.
module ExitCode =

    [<Literal>]
    let Success = 0

    /// An unexpected internal failure.
    [<Literal>]
    let InternalError = 1

    /// The command line could not be understood.
    [<Literal>]
    let UsageError = 2

    /// `verify` (or `--check`) found the repository invalid or out of date.
    [<Literal>]
    let VerificationFailed = 3

    /// The installation exists but this release cannot work with it.
    [<Literal>]
    let IncompatibleInstallation = 4

    /// A migration precondition failed, so nothing was changed.
    [<Literal>]
    let MigrationBlocked = 5

    /// The environment is missing something the command needs.
    [<Literal>]
    let PrerequisiteFailed = 6

    /// Reserved for the Node bootstrap when no binary matches the platform.
    [<Literal>]
    let UnsupportedPlatform = 7

/// How a managed path is allowed to change.
type Ownership =
    /// Controlled by the tool; replaced only through explicit version rules.
    | ToolOwned
    /// Derived from authoritative inputs; safe to regenerate or delete.
    | Generated
    /// Controlled by the repository; never overwritten automatically.
    | UserOwned
    /// Managed by both; changes need explicit merge or migration logic.
    | Shared

module Ownership =

    let toWire ownership =
        match ownership with
        | ToolOwned -> "tool-owned"
        | Generated -> "generated"
        | UserOwned -> "user-owned"
        | Shared -> "shared"

    let ofWire (value: string) =
        match value with
        | "tool-owned" -> Some ToolOwned
        | "generated" -> Some Generated
        | "user-owned" -> Some UserOwned
        | "shared" -> Some Shared
        | _ -> None

/// A repository path this tool knows about.
type ManagedArtifact =
    { /// Stable identifier, independent of the path, so paths can move between versions.
      Id: string
      /// Repository-relative path using forward slashes.
      Path: string
      Ownership: Ownership
      /// Whether a valid installation must contain it.
      Required: bool
      Description: string }

/// An npm script this tool contributes to the consuming repository's package.json.
type ManagedScript =
    { Name: string
      Command: string }

type Severity =
    | Error
    | Warning
    | Information

module Severity =

    let toWire severity =
        match severity with
        | Error -> "error"
        | Warning -> "warning"
        | Information -> "information"

    let rank severity =
        match severity with
        | Error -> 2
        | Warning -> 1
        | Information -> 0

/// Something wrong with an installation, expressed so both humans and agents can act on it.
type Problem =
    { Code: string
      Severity: Severity
      Title: string
      Detail: string
      Path: string option
      Remediation: string option }

module Problem =

    let create code severity title detail =
        { Code = code
          Severity = severity
          Title = title
          Detail = detail
          Path = None
          Remediation = None }

    let withPath path (problem: Problem) : Problem = { problem with Path = Some path }

    let withRemediation remediation (problem: Problem) : Problem =
        { problem with Remediation = Some remediation }

/// What the manifest says is installed. `ToolVersion` is absent for installations
/// created before manifests existed, and `ConfigurationVersion` is 0 for them.
type InstalledVersion =
    { ToolVersion: string option
      ConfigurationVersion: int }

type TargetVersion =
    { ToolVersion: string
      ConfigurationVersion: int }

/// The lifecycle state of the capability inside one repository.
type InstallationState =
    | NotInstalled
    | Installed of InstalledVersion
    | UpgradeRequired of current: InstalledVersion * target: TargetVersion
    | Invalid of Problem list

module InstallationState =

    let toWire state =
        match state with
        | NotInstalled -> "not-installed"
        | Installed _ -> "installed"
        | UpgradeRequired _ -> "upgrade-required"
        | Invalid _ -> "invalid"

    let describe state =
        match state with
        | NotInstalled -> "not installed"
        | Installed version ->
            match version.ToolVersion with
            | Some tool -> sprintf "installed (%s, configuration %d)" tool version.ConfigurationVersion
            | None -> sprintf "installed (configuration %d)" version.ConfigurationVersion
        | UpgradeRequired (current, target) ->
            match current.ToolVersion with
            | Some tool ->
                sprintf
                    "upgrade required (%s configuration %d -> %s configuration %d)"
                    tool
                    current.ConfigurationVersion
                    target.ToolVersion
                    target.ConfigurationVersion
            | None ->
                sprintf
                    "upgrade required (installed before installation manifests existed -> %s configuration %d)"
                    target.ToolVersion
                    target.ConfigurationVersion
        | Invalid problems -> sprintf "invalid (%d problem(s))" (List.length problems)
