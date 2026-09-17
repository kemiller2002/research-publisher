# F. Proposed F# Domain Model

Grounded in B and C. Per §12 and §28, no generalised knowledge-graph
abstraction: the corpus has a small number of concrete shapes, so the types are
concrete.

## F.1 Design rules this model follows

1. **Nothing is discarded.** Unknown front-matter keys and unknown body
   sections are preserved verbatim (§28: "do not discard unknown ROS sections").
2. **Absence is modelled, not defaulted.** No `?? "2026-07-22"`. Missing data is
   `None` and stays `None` through to the output.
3. **Derived ≠ canonical.** Derived relationships carry a distinct type so they
   can never be mistaken for research (§9).
4. **Free text stays free text.** `status` is not an enum (37 observed values).
5. **Parse failure is a state, not an exception.**

## F.2 Identity

```fsharp
/// A declared `id:` from front matter. Never invented (§28).
type ArtifactId = private ArtifactId of string

/// Stable address for an artifact that declares no id: derived from the
/// repository-relative source path, never from the title. Fixes the
/// retitling hazard in A.8 without minting fake ids.
type PathKey = private PathKey of string

type ArtifactKey =
    | Declared of ArtifactId
    | Derived  of PathKey

module ArtifactId =
    /// Accepts every observed grammar: REP-BDE-0001, EVREG-BDE-001,
    /// EX-COMP-011, SPEC-001, RFR-B186B831, ADR-WC-INDEX-0001.
    /// Deliberately permissive: the corpus has no single grammar (B.6).
    let create (raw: string) : Result<ArtifactId, string> = …
```

## F.3 Artifact type

```fsharp
/// Observed families. `Other` is required, not a failure: the corpus has 22
/// document_type values and 27 artifact-type directories, and new ones appear
/// without notice.
type ArtifactType =
    | ResearchExecutionPackage
    | ResearchReport
    | ResearchNote
    | EvidenceRegistry
    | HypothesisRegistry
    | TheoryRegistry
    | ExperimentReport
    | ResearchJournal
    | DecisionRecord
    | FrontierRecord
    | Other of raw: string

/// How the type was established — so the UI can distinguish a declared type
/// from a guess. The current engine cannot, which is why 708 records look
/// identically typed.
type TypeProvenance =
    | DeclaredFrontMatter of key: string   // document_type | artifactType
    | InferredFromDirectory of segment: string
    | InferredFromIdPrefix of prefix: string
    | Unknown
```

## F.4 Status — free text with optional derived reading

```fsharp
/// Verbatim, always. 37 observed values including
/// "computational-pilot-complete-human-study-pending".
type StatusText = StatusText of string

/// A *derived*, best-effort classification for filtering. Never replaces the
/// text, never blocks publication.
type StatusClass =
    | Draft | Active | Complete | Verified | Superseded | Open
    | Unclassified

/// Observed statuses encode stage + outstanding obligation:
///   "computational-pilot-complete" + "human-study-pending"
type StatusReading =
    { Text        : StatusText
      Class       : StatusClass
      Outstanding : string list }
```

## F.5 References — the heterogeneous union from C.2

```fsharp
/// Exactly the value kinds measured in related_documents.
type ReferenceValue =
    | RepoRelativePath of string      // 22 observed
    | FileRelativePath of string      // 18 observed
    | IdReference      of ArtifactId  //  6 observed
    | ProseTitle       of string      //  8 observed — NOT a link
    | Unparsed         of string

type ResolvedReference =
    | Resolves   of ArtifactKey
    | Dangling   of ReferenceValue      // legal; warn only
    | NotALink   of ProseTitle: string   // legal; never warn
```

## F.6 Relationships

```fsharp
/// Canonical: encoded by the research itself (C.1).
type CanonicalRelation =
    | RelatedDocument
    | SourceRep
    | Supersedes
    | SupersededBy
    | Originates      // document -> frontier record (520 observed)
    | Prerequisite    // frontier -> frontier (312 observed)

/// Derived for navigation only (C.5). Separate type so the two can never be
/// confused in code or in output.
type DerivedRelation =
    | Backlink of CanonicalRelation
    | SameProject
    | SharedConcept of string
    | FrontierOf
    | SupersessionChain

type Relation =
    | Canonical of CanonicalRelation
    | Derived   of DerivedRelation

type Edge =
    { From       : ArtifactKey
      To         : ResolvedReference
      Relation   : Relation
      Evidence   : SourceSpan        // where in the file this came from
      Confidence : Confidence }
```

## F.7 Provenance and source location

```fsharp
type SourceSpan =
    { SourcePath : string    // repository-relative, always present
      Line       : int option
      FrontMatterKey : string option
      HeadingPath : string list }   // e.g. ["Evidence trace"]

/// Every published fact points back here (§22.10).
type Provenance =
    { Span       : SourceSpan
      Repository : string
      Commit     : string option }
```

## F.8 The artifact

```fsharp
type Section =
    { Heading : string
      Depth   : int
      Slug    : string
      Body    : string }            // preserved verbatim, including unknown sections

type Artifact =
    { Key          : ArtifactKey
      DeclaredId   : ArtifactId option      // None for 68 of 117 authored docs
      Title        : string option          // None is legal; 64 of 117 declare one
      Type         : ArtifactType
      TypeSource   : TypeProvenance
      Project      : string option
      Status       : StatusReading option
      Created      : System.DateOnly option // None, never a fabricated default
      Updated      : System.DateOnly option
      Purposes     : string list            // 112 of 117 — the most reliable facet
      Audiences    : string list
      Summary      : string option          // summary | abstract
      Confidence   : float option
      References   : (CanonicalRelation * ReferenceValue) list
      Bibliography : string list            // `references:` — NOT links (C.3)
      Sections     : Section list
      UnknownKeys  : Map<string, string>    // every front-matter key we did not model
      Provenance   : Provenance }
```

`UnknownKeys` is the mechanism that satisfies §1.2: an artifact using a key
nobody anticipated still round-trips, and the key is visible in the output
rather than dropped.

## F.9 Parse and validation state

```fsharp
type ParseState =
    | Parsed
    | ParsedWithoutFrontMatter          // 1 observed file
    | ParseFailed of reason: string

type Finding =
    { Code     : string
      Severity : Severity
      Span     : SourceSpan
      Message  : string
      Remedy   : string option }

and Severity =
    | Blocking      // publication must stop
    | Warning       // publish, but surface it
    | Informational

type Corpus =
    { Artifacts : Artifact list
      Edges     : Edge list
      Findings  : Finding list
      Roots     : DiscoveryRoot list }
```

## F.10 What is deliberately NOT modelled

Per §28 and C.4, there is **no** `Finding`/`Claim`/`EvidenceItem`
sub-document research type, because no such object exists in the corpus with an
identity. Introducing one would require inventing IDs.

Sub-document addressing is limited to `Section`, keyed by heading slug, and is
explicitly marked unstable (C.5, `section-anchor`).
