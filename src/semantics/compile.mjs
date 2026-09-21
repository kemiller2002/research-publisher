import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { runLifecycle } from "../../bin/lifecycle-runtime.js";

function compatibilityRelationshipLists(artifact) {
  const relationships = artifact.relationships ?? [];
  const resolvedTargetIds = (relation) =>
    relationships
      .filter((item) => item.relation === relation && item.targetId)
      .map((item) => item.targetId);

  return {
    relatedDocuments: resolvedTargetIds("related-document"),
    evidenceIds: relationships
      .filter((item) => item.relation === "evidence-reference")
      .map((item) => item.targetId ?? item.rawTarget),
    hypothesisIds: relationships
      .filter((item) => item.relation === "hypothesis-reference")
      .map((item) => item.targetId ?? item.rawTarget),
    theoryIds: relationships
      .filter((item) => item.relation === "theory-reference")
      .map((item) => item.targetId ?? item.rawTarget)
  };
}

function pathSlug(url) {
  return url.replace(/^\/(?:a|s)\//, "").replace(/\/$/, "");
}

async function loadDerivedRelationships(projectRoot) {
  const relativePath = "research/frontier/frontier-graph.json";
  const absolutePath = path.join(projectRoot, relativePath);

  let graph;
  try {
    graph = JSON.parse(await fs.readFile(absolutePath, "utf8"));
  } catch (error) {
    if (error?.code === "ENOENT") return [];
    throw new Error(`Unable to read derived relationship source ${relativePath}: ${error.message}`);
  }

  if (!Array.isArray(graph.edges)) {
    throw new Error(`Derived relationship source ${relativePath} must contain an edges array.`);
  }

  return graph.edges
    .filter((edge) =>
      typeof edge?.from === "string"
      && typeof edge?.to === "string"
      && typeof edge?.type === "string"
    )
    .map((edge) => ({
      sourceRef: edge.from,
      targetRef: edge.to,
      relation: edge.type,
      evidenceSource: relativePath
    }));
}

export async function compileSemanticCorpus({ projectRoot, parsedDocuments }) {
  const derivedRelationships = await loadDerivedRelationships(projectRoot);
  const input = JSON.stringify({
    schemaVersion: "1.0",
    documents: parsedDocuments.map((document) => ({
      sourcePath: document.relativePath,
      frontmatter: document.frontmatter ?? {},
      excerpt: document.excerpt || null,
      headings: document.headings ?? []
    })),
    derivedRelationships
  });

  const result = runLifecycle(["compile-semantics", "--repo", projectRoot], {
    capture: true,
    input,
    cwd: projectRoot
  });

  if (result.status !== 0) {
    throw new Error(
      \`F# semantic compilation failed with exit code \${result.status}: \${result.error ?? result.stdout ?? "no diagnostic output"}\`
    );
  }

  const semantic = JSON.parse(result.stdout);
  const parsedByPath = new Map(parsedDocuments.map((document) => [document.relativePath, document]));

  const artifacts = semantic.artifacts.map((artifact) => {
    const parsed = parsedByPath.get(artifact.sourcePath);
    if (!parsed) {
      throw new Error(\`Semantic artifact \${artifact.sourcePath} has no parsed Markdown source.\`);
    }

    return {
      ...artifact,
      ...compatibilityRelationshipLists(artifact),
      schemaVersion: semantic.schemaVersion,
      slug: pathSlug(artifact.url),
      canonicalUrl: artifact.url,
      headings: parsed.headings ?? [],
      links: parsed.links ?? [],
      sourceMarkdown: parsed.body,
      contentHash: crypto.createHash("sha256").update(parsed.body).digest("hex"),
      compatibilityMode: Object.keys(parsed.frontmatter ?? {}).length === 0
    };
  });

  return {
    ...semantic,
    artifacts
  };
}
