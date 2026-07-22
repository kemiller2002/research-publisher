# Autonomous Engineering Prompt: Reusable Research Publishing System

**Purpose:** Give this prompt to Codex or another autonomous engineering agent to design and implement a reusable Markdown-to-web publishing system for Visual Engineering and future research frameworks.

---

## Role

You are an autonomous senior software architect and implementation agent.

Your task is to design, implement, test, and document a reusable research publishing system that transforms structured Markdown research repositories into a mobile-friendly, searchable static website.

This is not a one-off Visual Engineering website.

The first implementation will be used by the Visual Engineering repository, but the underlying publishing system must be reusable by other research repositories and research frameworks with minimal configuration and without copying or forking the core implementation.

You are responsible for making reasonable engineering decisions, testing your assumptions, and completing the implementation without repeatedly asking for permission.

Do not stop at an architecture proposal. Build the working system.

---

# Primary Objective

Create an npm-driven publishing tool that:

1. Discovers Markdown files in configurable source directories.
2. Parses and validates document metadata.
3. Converts Markdown into semantic, accessible HTML.
4. Applies reusable page layouts and CSS.
5. Generates navigation, indexes, relationship links, and collection pages.
6. Generates a machine-readable JSON metadata catalog during every build.
7. Generates a static full-text search index during every build.
8. Produces a deployable static site.
9. Runs locally through npm commands.
10. Runs automatically through GitHub Actions.
11. Can be reused by future research repositories through configuration rather than duplicated code.
12. Preserves Markdown as the canonical research source.

The completed system must work without a database or application server.

---

# Canonical Architectural Direction

Begin with this architecture as the leading hypothesis, but verify it against the repository and challenge it before finalizing:

- **Static site framework:** Astro
- **Markdown/content validation:** Astro Content Collections or a framework-neutral schema layer using Zod
- **Markdown transformations:** remark/rehype plugins where transformations are required
- **Static full-text search:** Pagefind
- **Metadata catalog:** a separately generated `research-catalog.json`
- **Package execution:** npm scripts
- **Automation:** GitHub Actions
- **Deployment target:** GitHub Pages by default, while keeping generated output portable to any static host
- **Core reusable implementation:** repository subfolder or workspace package such as `tools/research-publisher/`
- **Repository-specific behavior:** configuration file such as `research-publisher.config.mjs`

Do not adopt a different stack merely because it is familiar. Change the architecture only when repository evidence or a tested requirement makes the alternative materially better.

---

# Important Distinction: Search Index vs. Metadata Catalog

Do not treat the search engine's private index as the only searchable data artifact.

Generate two complementary artifacts:

## 1. Full-text search index

Use Pagefind or an equally defensible static search engine to index the generated HTML.

It should support:

- Search across document body text
- Search-result excerpts
- Metadata returned with results
- Filters where practical
- Static hosting
- Progressive loading
- No hosted search service
- No database

## 2. Public research metadata catalog

Generate a stable JSON document, preferably:

```text
dist/data/research-catalog.json
```

This catalog is part of the public contract of the publishing system. It must be readable by future websites, scripts, research agents, visualization tools, and knowledge-graph builders.

It should not depend on Pagefind internals.

At minimum, each normalized record should support:

```json
{
  "schemaVersion": "1.0",
  "id": "RP-...",
  "title": "Document title",
  "slug": "stable-url-slug",
  "url": "/research/...",
  "artifactType": "research-package",
  "researchArea": "Visual Engineering",
  "discipline": ["Perception", "Typography"],
  "summary": "Concise generated or supplied summary",
  "status": "active",
  "version": "2.0",
  "confidence": 0.82,
  "completion": 0.75,
  "priority": "high",
  "authorAgent": "agent-name",
  "created": "2026-07-22",
  "updated": "2026-07-22",
  "tags": [],
  "keywords": [],
  "relatedProjects": [],
  "relatedDocuments": [],
  "supersedes": [],
  "supersededBy": [],
  "evidenceIds": [],
  "hypothesisIds": [],
  "theoryIds": [],
  "headings": [],
  "sourcePath": "relative/path/document.md",
  "contentHash": "sha256-or-equivalent"
}
```

The exact shape may be improved, but it must be:

- Versioned
- Documented
- Deterministic
- Stable across builds
- Extensible without silently breaking consumers
- Suitable for filtering, linking, graph generation, and AI consumption

Also generate a JSON Schema for the catalog if practical.

---

# Research Execution Package Compatibility

The publishing system must support the canonical Research Execution Package metadata and identifiers.

Recognize these artifact prefixes:

| Artifact | Prefix |
|---|---|
| Research Package | `RP-` |
| Journal Entry | `JR-` |
| Evidence | `EV-` |
| Hypothesis | `HY-` |
| Theory | `TH-` |
| Experiment | `EX-` |
| Decision Framework | `DF-` |
| Concept | `CN-` |
| Glossary | `GL-` |

Support these metadata concepts where present:

- Identifier
- Title
- Research Area
- Discipline
- Author Agent
- Version
- Confidence
- Completion
- Priority
- Related Projects
- Related Documents
- Supersedes
- Superseded By
- Tags
- Keywords

Documents that predate the metadata standard must not become invisible. Implement a controlled compatibility mode that can infer safe fields such as title, slug, headings, dates, and artifact type while clearly reporting missing canonical metadata.

Do not silently invent research claims, confidence values, identifiers, or relationships.

---

# Required Research and Hypothesis Phase

Before implementation, inspect the repository and perform a focused architecture investigation.

Record the investigation in:

```text
docs/research-publisher-architecture.md
```

For every major decision:

1. State the hypothesis.
2. Explain why it might be correct.
3. Identify evidence that would support it.
4. Identify failure modes or counterexamples.
5. Test it where practical.
6. Record the conclusion.
7. Record confidence.
8. Record what could cause the decision to change.

At minimum, test these hypotheses.

---

## Hypothesis 1: A static site is sufficient

**Claim:** The research corpus can be published as generated static HTML without a backend.

Challenge this by checking:

- Expected document count
- Expected corpus size
- Search performance
- Relationship traversal
- Build time
- GitHub Pages limitations
- Whether authenticated or mutable server-side features are required

Expected conclusion unless contradicted:

A static site is the correct default because the corpus is Markdown-based, version-controlled, deployable through GitHub, and primarily read/search oriented.

---

## Hypothesis 2: Astro is preferable to a custom HTML generator

**Claim:** Astro provides reusable layouts, static output, content schemas, routing, and low client-side JavaScript without forcing the project into a large application framework.

Compare it with at least:

- A custom Node + remark/rehype generator
- Eleventy
- Vite with custom generation
- Another credible static site alternative

Evaluate:

- Reusability
- Content validation
- Template flexibility
- Build complexity
- Long-term maintenance
- Ability to package the publishing engine
- GitHub Pages deployment
- Search integration
- Dependence on framework-specific content placement

Do not select a fully custom generator merely because its first version appears smaller. Include the maintenance cost of recreating routing, layouts, collections, pagination, feeds, asset handling, development server behavior, and error reporting.

---

## Hypothesis 3: The reusable system should be a package, not copied scaffolding

**Claim:** The core publishing implementation should live in one reusable package or workspace, while each research repository supplies content and configuration.

Challenge this against:

- Ease of use on iPhone/iPad workflows
- npm installation behavior
- local workspace linking
- Git submodules
- Git subtree
- npm package publishing
- monorepo workspaces
- version upgrades
- offline use

Prefer a staged design:

### Stage A

Place the reusable engine inside the repository:

```text
tools/research-publisher/
```

Keep clear package boundaries from the beginning.

### Stage B

Make the same package installable from a Git URL or npm registry when more repositories adopt it.

Avoid making Git submodules the default unless repository constraints require them. They add operational friction and are easy to leave uninitialized.

---

## Hypothesis 4: Pagefind should power full-text search

**Claim:** Pagefind is a strong fit because it builds after static HTML generation and creates a static, low-bandwidth search bundle.

Challenge it with:

- Corpus scale
- Filters
- metadata return
- excerpt quality
- non-Latin text requirements
- indexing control
- GitHub Pages base paths
- offline/local preview
- client bundle behavior

Compare it with:

- Lunr
- FlexSearch
- MiniSearch
- A custom pre-generated JSON text index

Expected conclusion unless tests contradict it:

Use Pagefind for full-text search, but do not expose its private index format as the research catalog.

---

## Hypothesis 5: Metadata must be normalized through a schema

**Claim:** A formal schema is necessary because Markdown front matter will drift across autonomous research agents and projects.

Test:

- Existing files with missing fields
- Different key casing
- Scalars vs arrays
- inconsistent dates
- confidence represented as words vs numbers
- relationship fields using filenames vs stable identifiers
- duplicate identifiers

Implement:

- Canonical field names
- Aliases for known legacy names
- validation severity levels
- actionable errors
- strict CI mode
- optional migration report
- deterministic normalization

Do not allow permissive parsing to turn corrupted metadata into a successful build without warnings.

---

## Hypothesis 6: The public catalog should be generated independently

**Claim:** A normalized JSON catalog is required in addition to the full-text search index.

Challenge whether Pagefind metadata alone is enough.

Evaluate future uses:

- faceted exploration
- document relationship graphs
- cross-repository aggregation
- autonomous-agent retrieval
- theory/evidence registries
- dashboards
- API generation
- broken-link validation
- change detection

Expected conclusion:

Generate the public catalog as a first-class build artifact.

---

## Hypothesis 7: Templates should use semantic slots, not document-specific HTML

**Claim:** Layouts should be reusable based on artifact type and metadata rather than individual filenames.

Implement a template hierarchy such as:

```text
BaseLayout
├── ResearchDocumentLayout
├── RegistryLayout
├── JournalLayout
├── IndexLayout
└── RelationshipGraphLayout
```

Allow repository configuration to map artifact types or path patterns to layouts.

Do not put Visual Engineering-specific names or assumptions into the core templates.

---

# Repository Discovery

Before modifying code, inspect:

- All Markdown files
- Existing front matter patterns
- Existing HTML/CSS/build scripts
- Existing `package.json`
- Existing GitHub Actions
- Current deployment conventions
- `input-documents/` or equivalent intake directories
- Duplicate or generated files
- REP, journal, evidence, hypothesis, theory, concept, and glossary documents
- Internal Markdown links
- Stable identifier usage
- File naming conventions
- Asset directories
- Existing site folders

Produce a machine-readable inventory:

```text
build-reports/content-inventory.json
```

Produce a human-readable report:

```text
build-reports/content-inventory.md
```

The inventory should classify files as:

- publishable source
- draft
- intake/unprocessed
- generated output
- archived
- ignored
- invalid
- ambiguous

Do not publish generated output or intake documents accidentally.

---

# Recommended Repository Structure

Adapt this structure to the existing repository rather than rearranging files without need:

```text
/
├── research/
│   ├── visual-engineering/
│   ├── evidence/
│   ├── hypotheses/
│   ├── theories/
│   ├── journals/
│   ├── concepts/
│   └── glossary/
├── input-documents/
├── tools/
│   └── research-publisher/
│       ├── package.json
│       ├── src/
│       │   ├── build/
│       │   ├── content/
│       │   ├── metadata/
│       │   ├── relationships/
│       │   ├── search/
│       │   ├── validation/
│       │   └── cli/
│       ├── site/
│       │   ├── components/
│       │   ├── layouts/
│       │   ├── pages/
│       │   └── styles/
│       ├── schemas/
│       ├── tests/
│       └── README.md
├── research-publisher.config.mjs
├── package.json
├── .github/
│   └── workflows/
│       ├── validate-research.yml
│       └── publish-research.yml
├── dist/
└── build-reports/
```

`dist/` and temporary build artifacts should generally be ignored by Git unless the repository's publishing method explicitly requires committed output.

---

# Configuration Contract

Create a documented configuration contract similar to:

```js
export default {
  site: {
    title: "Visual Engineering Research",
    description: "Searchable Visual Engineering research repository",
    baseUrl: "/visual-engineering/",
    language: "en",
  },

  content: {
    include: [
      "research/**/*.md",
      "docs/**/*.md"
    ],
    exclude: [
      "input-documents/**",
      "dist/**",
      "node_modules/**",
      "**/archive/**"
    ],
    drafts: false
  },

  metadata: {
    mode: "compatible",
    strictInCI: true,
    required: ["title"],
    stableIdPrefixes: [
      "RP", "JR", "EV", "HY", "TH", "EX", "DF", "CN", "GL"
    ]
  },

  output: {
    directory: "dist",
    catalog: "data/research-catalog.json",
    diagnostics: "data/build-diagnostics.json"
  },

  features: {
    search: true,
    filters: true,
    relatedDocuments: true,
    backlinks: true,
    tableOfContents: true,
    readingProgress: false,
    graphData: true
  },

  branding: {
    cssVariables: {},
    logo: null
  }
};
```

The implementation may refine this API. Keep it small, stable, and framework-neutral from the consumer's perspective.

---

# Build Pipeline

Implement the build as explicit stages.

## Stage 1: Discover

- Resolve configured glob patterns.
- Normalize paths.
- Exclude generated, intake, ignored, and archived content.
- Detect duplicate source inclusion.
- Sort deterministically.

## Stage 2: Parse

- Parse YAML front matter.
- Parse Markdown into an AST.
- Extract headings.
- Extract explicit links and stable identifiers.
- Preserve source positions for diagnostics where possible.

## Stage 3: Normalize

- Map legacy metadata aliases to canonical fields.
- Normalize arrays, dates, status values, confidence, completion, and paths.
- Generate slugs only when absent.
- Never generate canonical research IDs silently.
- Calculate content hashes.

## Stage 4: Validate

Validate:

- required metadata
- duplicate identifiers
- duplicate output URLs
- malformed dates
- unsupported artifact types
- invalid relationship targets
- broken internal links
- contradictory supersession relationships
- missing referenced evidence/hypotheses/theories
- unsafe raw HTML
- invalid heading hierarchy where practical
- inaccessible image metadata where practical

Support:

```text
npm run research:validate
```

CI validation should fail for errors and report warnings clearly.

## Stage 5: Build relationship model

Create indexes for:

- outbound links
- backlinks
- related documents
- evidence references
- hypothesis references
- theory references
- supersession chains
- project membership
- tags
- disciplines
- artifact types

Write a reusable graph artifact:

```text
dist/data/research-graph.json
```

Do not require a graphical network visualization in the first release. Produce correct graph data first.

## Stage 6: Render HTML

Generate:

- home/dashboard page
- document pages
- artifact-type indexes
- research-area indexes
- discipline indexes
- tag indexes
- status indexes
- recent-updates page
- open-questions page where metadata permits
- document relationships and backlinks
- not-found page
- sitemap
- optional RSS/Atom feed

## Stage 7: Generate public JSON artifacts

Generate:

- `research-catalog.json`
- `research-graph.json`
- `build-diagnostics.json`
- optional per-collection JSON files
- JSON Schema files where practical

## Stage 8: Generate search index

Run Pagefind after HTML output exists.

Ensure:

- only meaningful page content is indexed
- navigation and repeated chrome are excluded
- title, summary, tags, discipline, artifact type, confidence, status, and stable ID can be returned as metadata
- useful filters are available
- GitHub Pages base paths work

## Stage 9: Verify output

Run post-build assertions:

- expected index files exist
- every catalog URL maps to generated HTML
- every published HTML document has title and canonical metadata
- search bundle exists
- asset paths respect configured base URL
- no source filesystem paths leak into public pages
- no output contains absolute local machine paths
- catalog order and output are deterministic

---

# User Interface Requirements

The website should prioritize research comprehension rather than resemble a generic blog.

## Global interface

Include:

- prominent search
- filter controls
- artifact-type navigation
- research-area navigation
- mobile navigation
- clear active filters
- result counts
- accessible keyboard navigation
- visible focus states
- light/dark color-scheme support where practical
- print-friendly document pages

## Document page

Include:

- title
- stable identifier
- artifact type
- research area
- discipline
- status
- version
- confidence
- completion
- priority
- tags
- updated date
- table of contents
- main document body
- related documents
- referenced evidence
- referenced hypotheses
- referenced theories
- backlinks
- supersession status
- source link when repository URL is configured

Do not display empty metadata panels.

## Search result

Include:

- title
- excerpt
- stable identifier
- artifact type
- research area
- discipline
- tags
- confidence or status where useful
- direct document link

Search must remain usable on a phone without horizontal scrolling.

---

# HTML Requirements

Generate semantic HTML.

Prefer:

- `header`
- `nav`
- `main`
- `article`
- `section`
- `aside`
- `footer`
- ordered heading levels
- real buttons for actions
- real links for navigation
- definition lists for metadata where appropriate

Requirements:

- A unique page title
- Logical heading hierarchy
- Skip link
- Landmark regions
- Accessible labels
- Responsive tables
- Accessible code blocks
- Anchored headings
- Safe external-link behavior
- Sanitized or explicitly controlled raw HTML
- No required client-side framework for reading content

The core document content must remain available when JavaScript is disabled. Search may require JavaScript.

---

# CSS Requirements

Create reusable CSS organized by responsibility rather than one large file.

Recommended layers:

```text
styles/
├── tokens.css
├── reset.css
├── base.css
├── typography.css
├── layout.css
├── components.css
├── utilities.css
├── print.css
└── themes/
```

Use CSS custom properties for:

- colors
- spacing
- typography
- content widths
- borders
- radii
- shadows
- responsive breakpoints where appropriate
- status and confidence indicators

Requirements:

- mobile-first
- responsive without device-specific hacks
- readable line length
- fluid typography where appropriate
- robust long URLs and code
- responsive tables
- touch-friendly controls
- reduced-motion support
- forced-colors/high-contrast compatibility where practical
- print styles
- no dependency on Bootstrap
- no Visual Engineering-specific styling embedded in layout logic

Brand customization should be possible through configuration and CSS variables without editing core components.

---

# npm Interface

At the repository root, provide a clear command interface:

```json
{
  "scripts": {
    "research:dev": "...",
    "research:build": "...",
    "research:validate": "...",
    "research:test": "...",
    "research:clean": "...",
    "research:inventory": "...",
    "research:check-links": "...",
    "research:preview": "..."
  }
}
```

Also support standard aliases where sensible:

```text
npm run dev
npm run build
npm test
```

Commands must work from a clean clone after:

```text
npm ci
```

Use the Node version already standardized by the repository. If none exists, select and document an active supported Node LTS version and add the appropriate version file or `engines` constraint.

Commit the lockfile.

---

# Git Workflow

Implement two focused workflows.

## Validation workflow

Trigger on pull requests and relevant pushes.

It should:

1. Check out the repository.
2. Set up Node.
3. Use dependency caching.
4. Run `npm ci`.
5. Run metadata validation.
6. Run tests.
7. Run a production build.
8. Run link and output verification.
9. Upload diagnostics or generated preview artifacts when useful.

Use least-privilege permissions.

Do not deploy from untrusted pull-request code.

## Publishing workflow

Trigger on the default branch after successful changes or through a safe manual dispatch.

It should:

1. Check out the repository.
2. Set up Node.
3. Run `npm ci`.
4. Run the complete production build.
5. Upload the static artifact.
6. Deploy using the official GitHub Pages workflow actions.
7. Use GitHub Pages environment protections and least-privilege permissions.

Pin actions to major or immutable versions according to the repository's security policy.

Avoid an oversized workflow. Keep reusable logic in npm scripts, not duplicated YAML shell sequences.

---

# Incremental and Deterministic Builds

The first release may perform a full build if the corpus is modest, but the architecture must not prevent future incremental builds.

Implement deterministic output now:

- stable sorting
- stable slugs
- stable catalog ordering
- normalized dates
- no build timestamps inside content artifacts unless clearly separated
- content hashes
- reproducible generated JSON formatting

Measure build performance and record:

- document count
- parse time
- render time
- search-index time
- total build time
- output size

Write measurements to diagnostics.

Do not introduce a complex cache until measurements show it is needed.

---

# Compatibility and Migration

Existing Markdown files may not follow the canonical schema.

Create:

```text
npm run research:inventory
```

and, if safe:

```text
npm run research:migrate -- --dry-run
```

Migration must:

- default to dry-run
- produce a diff or migration plan
- never overwrite documents without explicit command flags
- preserve content
- avoid inventing canonical IDs
- normalize only high-confidence mechanical fields
- report ambiguous cases for review

The normal build may operate in compatibility mode, but CI should be able to become stricter over time.

---

# Testing Requirements

Use automated tests at multiple levels.

## Unit tests

Test:

- metadata normalization
- legacy aliases
- slug generation
- stable sorting
- identifier parsing
- relationship extraction
- link resolution
- catalog serialization
- config merging
- path handling across operating systems

## Fixture tests

Create representative fixture documents for:

- complete REP
- legacy Markdown with no front matter
- evidence record
- hypothesis record
- theory record
- duplicate ID
- broken relation
- malformed metadata
- Unicode title
- nested directories
- long tables
- code blocks
- images
- draft and ignored files

## Integration tests

Test:

- clean build
- expected pages
- expected catalog records
- Pagefind output
- base URL handling
- broken-link failure
- GitHub Pages path behavior

## Accessibility and HTML checks

Use pragmatic automated checks for:

- invalid HTML
- missing titles
- missing image alt text where detectable
- heading hierarchy
- landmark presence
- obvious color-independent status labeling

Do not claim full accessibility compliance solely from automated tests.

---

# Documentation Requirements

Create:

```text
tools/research-publisher/README.md
docs/research-publisher-architecture.md
docs/research-metadata-schema.md
docs/research-publishing-workflow.md
docs/adding-a-research-repository.md
docs/troubleshooting-research-builds.md
```

Document:

- architecture
- why decisions were made
- alternatives rejected
- configuration
- metadata schema
- local development
- build commands
- GitHub Actions
- GitHub Pages
- adding layouts
- adding artifact types
- branding
- search behavior
- catalog contract
- migration
- diagnostics
- upgrading the reusable package
- known limitations

Include a five-minute setup path for a new repository.

---

# Reusability Acceptance Test

Prove reuse rather than merely claiming it.

Create a small second fixture research project with different:

- site title
- branding variables
- source folders
- metadata mix
- artifact types or collection organization

Build both Visual Engineering and the fixture project using the same core publishing package without duplicating core templates or build logic.

Repository-specific code should be limited to:

- configuration
- optional theme overrides
- optional extension components
- content

If the second project requires copying internal build files, the reuse goal has failed.

---

# Security and Integrity Requirements

- Treat Markdown and front matter as untrusted input.
- Avoid arbitrary code execution from content files.
- Do not enable MDX by default.
- Sanitize or disallow raw HTML by default.
- Do not interpolate unescaped metadata into HTML.
- Prevent path traversal in configured content paths and output paths.
- Prevent duplicate URLs.
- Use least-privilege GitHub Actions permissions.
- Do not expose secrets in the static site.
- Do not execute scripts discovered inside the research content.
- Report external links but do not make network availability a mandatory local-build requirement unless explicitly configured.
- Record dependency rationale and keep the dependency set restrained.

---

# Scope Control

## Required for Version 1

- reusable package boundary
- configurable content discovery
- metadata normalization and validation
- static HTML generation
- reusable layouts
- reusable mobile-first CSS
- public JSON metadata catalog
- relationship/backlink model
- Pagefind full-text search
- local npm commands
- tests
- GitHub Actions validation
- GitHub Pages deployment
- documentation
- second-project reuse demonstration

## Valuable but optional after Version 1 is stable

- interactive relationship graph
- PWA/offline caching
- cross-repository federation
- semantic/vector search
- client-side annotations
- authenticated content
- editorial CMS
- automatic AI summaries
- PDF generation
- incremental cache
- remote content adapters

Do not delay the core system to implement optional features.

Design extension seams for them, but finish Version 1 first.

---

# Engineering Decision Principles

1. Markdown is the canonical source.
2. Generated artifacts are disposable and reproducible.
3. Search and catalogs are build outputs, not manually edited sources.
4. Stable identifiers outrank filenames.
5. Relationships should resolve through identifiers when available.
6. Configuration should replace copying.
7. The core package must not encode Visual Engineering assumptions.
8. HTML must be useful without client-side application hydration.
9. Build failures must be actionable.
10. Warnings must not hide data loss.
11. Start with the simplest architecture that preserves future extensibility.
12. Measure before optimizing.
13. Prefer boring, supported tools over clever custom infrastructure.
14. Keep CI orchestration thin and application logic testable locally.
15. Never claim reuse until a second project successfully builds.

---

# Required Deliverables

Complete all of the following:

1. Working reusable publishing package
2. Visual Engineering configuration
3. Responsive static website
4. Reusable HTML/Astro layouts
5. Reusable CSS system
6. Generated `research-catalog.json`
7. Generated `research-graph.json`
8. Pagefind search index and search interface
9. Metadata schema and validators
10. Content inventory
11. Build diagnostics
12. Unit and integration tests
13. Validation GitHub Action
14. GitHub Pages publishing GitHub Action
15. Architecture decision document
16. Metadata documentation
17. Setup and workflow documentation
18. Second-project reuse fixture
19. Final implementation report
20. Research Execution Package for the engineering effort

---

# Final Implementation Report

Create:

```text
docs/research-publisher-implementation-report.md
```

Include:

- what was built
- architecture
- repository changes
- commands
- generated artifacts
- test results
- build measurements
- hypotheses tested
- rejected alternatives
- failed assumptions
- compromises
- security considerations
- accessibility considerations
- known limitations
- upgrade path
- next recommended work

---

# Research Execution Package

At completion, create a REP that follows the repository's canonical REP specification.

It must include:

- Executive Summary
- Original Objective
- Scope
- Repository Context
- Current Understanding
- Key Discoveries
- Evidence Registry
- Hypothesis Registry
- Failed Assumptions
- Open Questions
- Recommended Next Research
- Research Backlog
- Risks
- Cross-Discipline Opportunities
- Knowledge Relationships
- Repository Updates
- Website Updates
- AI Consumption Notes
- Handoff Instructions
- Research Journal
- Completion Checklist

Record stable evidence, hypothesis, theory, experiment, and decision-framework identifiers where appropriate.

---

# Completion Criteria

Do not declare completion until all of the following are true:

- A clean clone installs with `npm ci`.
- Validation passes.
- Tests pass.
- The production build succeeds.
- The generated site is mobile friendly.
- Search works against generated content.
- The public metadata catalog contains every published document.
- Catalog URLs resolve to generated pages.
- Relationships and backlinks are generated.
- Missing or invalid metadata is reported.
- GitHub Pages base paths work.
- GitHub Actions workflows are valid.
- Visual Engineering builds through configuration.
- A second fixture project builds through the same package.
- Documentation is sufficient for another agent to operate and extend the system.
- The final implementation report exists.
- The REP exists.

If a criterion cannot be completed, do not hide it. Record the exact blocker, evidence, attempted solutions, and the smallest next action required.

---

# Execution Behavior

Work autonomously.

Inspect before changing.

Research enough to avoid foundational mistakes, then implement incrementally.

Use small, testable milestones:

1. Inventory
2. Architecture decision
3. Package skeleton
4. Metadata normalization
5. Catalog generation
6. HTML rendering
7. CSS and layouts
8. Relationships
9. Search
10. Tests
11. GitHub workflows
12. Reuse proof
13. Documentation
14. REP

After each milestone:

- run relevant tests
- inspect generated output
- record discoveries
- correct flawed assumptions
- proceed to the highest-value next step

Do not stop after creating plans or placeholders.

Do not leave core behavior as TODO comments.

Do not ask for approval for normal implementation decisions.

The evidence wins over the initial architecture when they conflict.
