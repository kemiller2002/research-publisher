# Research Metadata Schema

## Canonical Fields

The publisher normalizes front matter into a versioned `1.1` record with these core fields:

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
- `derivedFrom`
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
  `null` when absent, and `null` when malformed: a malformed block is not
  published and produces a `malformed-provenance` warning.
- `provenanceStatus`: `null`, or `{verdict, schema, warnings, problems}`.
- `derivedFrom`: lineage from `derived_from` / `derivedFrom` (and a supported
  block's own `derivedFrom`). Lineage is not authorship. References to published
  documents also become `derived-from` edges in the relationship graph.
- `selfDeclaredAuthors`: legacy free-text author fields (`authorAgent`,
  `author`, `author_agent`, `created_by_agent`, `owner_agent`,
  `source_author`) as `{field, value, status: "self-declared-unverified"}`.
  They are never converted into structured provenance.

## Compatibility Mode

When a Markdown file has no front matter, the build still publishes it in compatibility mode:

- Title is inferred from the first H1 or filename.
- Identifier is inferred only when a stable prefix already exists in the filename.
- Artifact type is inferred from the directory or identifier prefix.
- Absent `created` and `updated` dates are `null`; no date is substituted.

Compatibility mode never invents canonical research claims, stable IDs, or relationship assertions.
