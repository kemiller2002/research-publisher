# Research Metadata Schema

## Canonical Fields

The publisher normalizes front matter into a versioned `1.3` record (catalog `schemaVersion` 1.3; relationship graph 1.1) with these core fields:

- `schemaVersion`
- `id`
- `title`
- `slug`
- `url`
- `artifactType`
- `project`
- `purposes`
- `audiences`
- `entryPoint`
- `entryPointOrder`
- `entryPointLabel`
- `researchArea`
- `discipline`
- `summary`
- `status`
- `version`
- `confidence`
- `completion`
- `priority`
- `authorAgent`
- `created`
- `updated`
- `tags`
- `keywords`
- `relatedProjects`
- `relatedDocuments`
- `supersedes`
- `supersededBy`
- `evidenceIds`
- `hypothesisIds`
- `theoryIds`
- `provenance`
- `provenanceStatus`
- `provenanceWithheld`
- `derivedFrom`
- `lineageWithheld`
- `lineageProblems`
- `selfDeclaredAuthors`
- `headings`
- `sourcePath`
- `contentHash`

## Legacy Aliases

The first version supports these metadata aliases:

- `identifier` -> `id`
- `stableId` -> `id`
- `research_area` -> `researchArea`
- `artifact_type` -> `artifactType`
- `updated_at` -> `updated`
- `created_at` -> `created`
- `author` -> `authorAgent`
- `author_agent` -> `authorAgent`
- `projectId` -> `project`
- `purpose` -> `purposes`
- `documentPurpose` -> `purposes`
- `audience` -> `audiences`

## Reader-Purpose And Navigation Fields

These fields organize documents without changing their epistemic type:

- `project`: stable lowercase key for the primary owning project
- `purposes`: controlled reader jobs; see [Document Purpose And Project Guide Architecture](./document-purpose-taxonomy.md)
- `audiences`: controlled intended readers
- `entryPoint`: explicit opt-in to the front of a project guide
- `entryPointOrder`: numeric order within that guide
- `entryPointLabel`: optional short presentation label

The build emits project entry points at `data/research-guides.json`. Unknown purpose or audience values produce warnings so vocabulary extensions are visible but do not silently remove content.

## Provenance And Lineage

The publisher carries Praxis provenance; it does not define it. See
[Provenance Transport Requirements](./vnext/19-provenance-transport-requirements.md)
(`REQ-RP-PROV`).

- `provenance`: the front matter's `provenance` block, verbatim (unknown fields
  and exact timestamps kept), when it classifies as `praxis.provenance/1`
  (`supported`) or as another major version (`unsupported`, not interpreted).
  `null` when absent (unattributed). When the block is malformed (including a
  declared `provenance: null` or any `null` inside it) the `provenance` key is
  omitted, `provenanceWithheld` is `true`, and the build reports a
  `malformed-provenance` warning; the document itself is still published.
- `provenanceWithheld`: `true` only when a malformed block was withheld.
- `provenanceStatus`: `null`, or `{verdict, schema, warnings, problems}`.
- `derivedFrom`: lineage from `derived_from` / `derivedFrom` (and a supported
  block's own `derivedFrom`). A scalar is one reference; only a list holds
  several. Lineage is not authorship. References to published
  documents also become `derived-from` edges in the relationship graph.
  Every reference passes the Praxis `addLineage` check; a credential, blank,
  non-string or null reference withholds the lineage (see below).
- `lineageWithheld`: `true` when the lineage was refused; `derivedFrom` is then
  `[]`, no graph edge is produced, and the build reports a `rejected-lineage`
  warning.
- `lineageProblems`: why the lineage was refused (never the refused values);
  `[]` otherwise.
- `selfDeclaredAuthors`: legacy free-text author fields (`authorAgent`,
  `author`, `author_agent`, `created_by_agent`, `owner_agent`,
  `source_author`) as `{field, value, status: "self-declared-unverified"}`.
  A scalar is one claim (`"Doe, Jane"` stays one author). They are never
  converted into structured provenance.

JSON front matter (`---json`) is classified as JSON text: a repeated member
name or an unpaired surrogate in `provenance` makes it malformed and withheld.

Version 1.3 (from 1.2) adds `lineageWithheld` and `lineageProblems`.
Version 1.2 (from 1.1) adds these fields and makes absent `created` / `updated`
`null` instead of a substituted date; consumers must handle `null` dates.

## Compatibility Mode

When a Markdown file has no front matter, the build still publishes it in compatibility mode:

- Title is inferred from the first H1 or filename.
- Identifier is inferred only when a stable prefix already exists in the filename.
- Artifact type is inferred from the directory or identifier prefix.
- Absent `created` and `updated` dates are `null`; no date is substituted.

Compatibility mode never invents canonical research claims, stable IDs, or relationship assertions.
