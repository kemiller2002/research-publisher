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

## Compatibility Mode

When a Markdown file has no front matter, the build still publishes it in compatibility mode:

- Title is inferred from the first H1 or filename.
- Identifier is inferred only when a stable prefix already exists in the filename.
- Artifact type is inferred from the directory or identifier prefix.
- Dates fall back to the current repository implementation date.

Compatibility mode never invents canonical research claims, stable IDs, or relationship assertions.
