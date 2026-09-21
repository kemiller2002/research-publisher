namespace ResearchPublisher.Semantics.Core

open System.Text.Json.Nodes

type Severity = Blocking | Warning | Informational
module Severity =
    let toWire = function Blocking -> "blocking" | Warning -> "warning" | Informational -> "informational"

type RelationAuthority = Canonical | DeclaredUnclassified | Derived
module RelationAuthority =
    let toWire = function Canonical -> "canonical" | DeclaredUnclassified -> "declared-unclassified" | Derived -> "derived"

type ResolutionStatus = Resolved | Dangling | NotALink | Ambiguous
module ResolutionStatus =
    let toWire = function Resolved -> "resolved" | Dangling -> "dangling" | NotALink -> "not-a-link" | Ambiguous -> "ambiguous"

type ReferenceKind = IdReference | RepoRelativePath | FileRelativePath | BareFileName | ProseTitle | Unparsed
module ReferenceKind =
    let toWire = function
        | IdReference -> "id"
        | RepoRelativePath -> "repo-relative-path"
        | FileRelativePath -> "file-relative-path"
        | BareFileName -> "bare-filename"
        | ProseTitle -> "prose-title"
        | Unparsed -> "unparsed"

type Finding =
    { Code: string; Severity: Severity; SourcePath: string; FrontMatterKey: string option; Message: string; Remedy: string option }

type Relationship =
    { SourceKey: string; SourcePath: string; Field: string; Relation: string; Authority: RelationAuthority
      RawTarget: string; ReferenceKind: ReferenceKind; Resolution: ResolutionStatus; TargetKey: string option
      TargetId: string option; TargetSourcePath: string option; TargetTitle: string option }

type Artifact =
    { Key: string; KeyKind: string; DeclaredId: string option; Title: string; TitleSource: string
      ArtifactType: string option; TypeSource: string; Project: string option; Purposes: string list; Audiences: string list
      EntryPoint: bool; EntryPointOrder: float option; EntryPointLabel: string option; ResearchArea: string option
      Discipline: string list; Summary: string option; Status: string option; Version: string option; Confidence: float option
      Completion: float option; Priority: string option; AuthorAgent: string option; Created: string option; Updated: string option
      Tags: string list; Keywords: string list; RelatedProjects: string list; Bibliography: string list; SourcePath: string
      CanonicalUrl: string; LegacyUrls: string list; FrontMatter: JsonObject; UnknownFrontMatter: JsonObject }

type ViewCapability = { Name: string; Available: bool; Reason: string }
type Compilation =
    { Artifacts: Artifact list; Relationships: Relationship list; Findings: Finding list
      Capabilities: ViewCapability list; Redirects: (string * string) list }
