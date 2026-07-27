# Document Purpose And Project Guide Architecture

## Decision

Research Publisher separates five questions that are often collapsed into tags:

| Field | Question | Cardinality |
| --- | --- | --- |
| `artifactType` | What kind of research artifact is this? | One |
| `project` | Which stable primary project owns it? | Zero or one |
| `purposes` | What reader jobs does it directly support? | Zero or many |
| `audiences` | Which readers is it intentionally written for? | Zero or many |
| `entryPoint` | Should it appear at the front of its project guide? | Boolean |

`tags` remain available for subjects and informal discovery. They do not control navigation.

## Controlled Reader Purposes

- `orient`: establish scope, vocabulary, state, or a starting point
- `decide`: support a decision or fast executive understanding
- `apply`: explain practical use or action
- `verify`: expose evidence, limitations, confidence, or support
- `reproduce`: provide method, procedure, data, or replication detail
- `reference`: support lookup, specification use, or definition
- `integrate`: connect or synthesize findings across documents
- `chronicle`: preserve progress, decisions, history, or audit trail

The vocabulary describes reader jobs rather than document genres. For example, a research package may support `orient` and `integrate`, while an evidence report may support `verify` and `reproduce`.

## Controlled Audiences

- `general`
- `executive`
- `practitioner`
- `researcher`
- `contributor`

Machines are catalog consumers, not a document audience. All published records are included in the public JSON catalog. A data-bearing source should use an appropriate `artifactType` and normally the `reproduce` purpose.

## Entry Points

An entry point is an explicit navigation decision, not a synonym for importance.

```yaml
project: visual-engineering
purposes:
  - orient
  - integrate
audiences:
  - executive
  - practitioner
  - researcher
entryPoint: true
entryPointOrder: 10
entryPointLabel: Start here
```

Entry points should be independently understandable and few enough to preserve hierarchy. Ordering uses increments of 10. The build groups them by project in `data/research-guides.json`.

## Alternatives Considered

### Free-form tags only

Rejected. Tags cannot reliably distinguish topic, audience, lifecycle, epistemic type, and navigation intent. Cross-project vocabulary drift would make them an unstable interface.

### Executive summary as an artifact type

Rejected. “Executive” describes an audience and “summary” describes presentation. A theory, decision, research package, or synthesis can each serve executives without changing its underlying artifact type.

### Folder-driven navigation

Rejected. Folder conventions vary across repositories and reflect authoring or ownership more often than reader sequence. Moving a file should not silently change its navigational role.

### A single `featured` flag

Rejected as insufficient. It provides no project boundary, reader purpose, audience, or deterministic order. `entryPoint` is therefore paired with project and purpose metadata.

### Fully configurable vocabularies in version 1

Deferred. Local vocabularies would improve domain flexibility but weaken cross-project interoperability before extension governance exists. Unknown terms currently produce warnings rather than build failures, preserving an escape hatch.

## Assumptions And Failure Conditions

- A document has one primary owning project. Cross-project relevance continues to use `relatedProjects`.
- Documents may serve several reader jobs, but excessive purpose assignment is treated as classification failure.
- Entry points can be derived only from source content; the publisher cannot manufacture a trustworthy synthesis.
- The controlled vocabulary should change only when repeated real documents cannot be classified without distortion.

Reconsider this model if multi-project co-ownership becomes common, if audience-specific variants require content negotiation, or if more than a small minority of documents need unrecognized reader jobs.

## Installation

Consumer repositories can install the reusable classification prompt with:

```bash
research-publisher install-prompt --config ./research-publisher.config.mjs
```

The command creates `prompts/research-publisher-mark-documents.md` and will not overwrite an existing copy.
