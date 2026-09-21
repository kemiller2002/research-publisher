export function buildRelationshipGraph(documents, relationships = []) {
  const nodes = documents.map((document) => ({
    id: document.key,
    declaredId: document.id,
    title: document.title,
    artifactType: document.artifactType,
    url: document.url,
    researchArea: document.researchArea,
    sourcePath: document.sourcePath
  }));

  const resolvedEdges = relationships
    .filter((relationship) => relationship.resolution === "resolved" && relationship.targetKey)
    .map((relationship) => ({
      source: relationship.sourceKey,
      target: relationship.targetKey,
      type: relationship.relation,
      authority: relationship.authority,
      field: relationship.field
    }));

  const backlinks = Object.fromEntries(documents.map((document) => [document.key, []]));
  for (const edge of resolvedEdges) {
    backlinks[edge.target]?.push(edge.source);
  }

  return {
    schemaVersion: "2.0",
    nodes,
    edges: resolvedEdges,
    backlinks,
    unresolved: relationships.filter((relationship) => relationship.resolution !== "resolved")
  };
}
