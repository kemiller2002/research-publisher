# Research Metadata Schema

## Canonical Fields

The publisher normalizes front matter into a versioned record with these core fields:

- `schemaVersion`
- `id`
- `title`
- `slug`
- `url`
- `artifactType`
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

## Compatibility Mode

When a Markdown file has no front matter, the build still publishes it in compatibility mode:

- Title is inferred from the first H1 or filename.
- Identifier is inferred only when a stable prefix already exists in the filename.
- Artifact type is inferred from the directory or identifier prefix.
- Dates fall back to the current repository implementation date.

Compatibility mode never invents canonical research claims, stable IDs, or relationship assertions.

