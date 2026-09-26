export function buildRelationshipGraph(documents) {
  const byId = new Map(documents.filter((document) => document.id).map((document) => [document.id, document]));
  const nodes = documents.map((document) => ({
    id: document.id ?? document.slug,
    title: document.title,
    artifactType: document.artifactType,
    url: document.url,
    researchArea: document.researchArea,
    sourcePath: document.sourcePath
  }));
  const edges = [];

  for (const document of documents) {
    const source = document.id ?? document.slug;
    for (const relatedId of document.relatedDocuments) {
      if (byId.has(relatedId)) {
        edges.push({
          source,
          target: relatedId,
          type: "related-document"
        });
      }
    }
    for (const evidenceId of document.evidenceIds) {
      if (byId.has(evidenceId)) {
        edges.push({
          source,
          target: evidenceId,
          type: "evidence"
        });
      }
    }
    for (const hypothesisId of document.hypothesisIds) {
      if (byId.has(hypothesisId)) {
        edges.push({
          source,
          target: hypothesisId,
          type: "hypothesis"
        });
      }
    }
    // Lineage (R14.5): typed separately from every other relation and never read as
    // authorship. Only references to published documents become edges; foreign or
    // unresolved references stay on the record's `derivedFrom`.
    for (const sourceId of document.derivedFrom ?? []) {
      if (byId.has(sourceId)) {
        edges.push({
          source,
          target: sourceId,
          type: "derived-from"
        });
      }
    }
    for (const theoryId of document.theoryIds) {
      if (byId.has(theoryId)) {
        edges.push({
          source,
          target: theoryId,
          type: "theory"
        });
      }
    }
  }

  const backlinks = Object.fromEntries(documents.map((document) => [document.id ?? document.slug, []]));
  for (const edge of edges) {
    backlinks[edge.target]?.push(edge.source);
  }

  return {
    schemaVersion: "1.1",
    nodes,
    edges,
    backlinks
  };
}

