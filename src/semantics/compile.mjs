import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { runLifecycle } from "../../bin/lifecycle-runtime.js";
import { ensureDirectory, writeJson } from "../build/filesystem.mjs";

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

export async function compileSemanticCorpus({ projectRoot, parsedDocuments }) {
  const stateDirectory = path.join(projectRoot, ".research-publisher");
  await ensureDirectory(stateDirectory);

  const inputPath = path.join(stateDirectory, "semantic-input.json");
  await writeJson(inputPath, {
    schemaVersion: "1.0",
    documents: parsedDocuments.map((document) => ({
      sourcePath: document.relativePath,
      frontmatter: document.frontmatter ?? {},
      excerpt: document.excerpt || null,
      headings: document.headings ?? []
    }))
  });

  const result = runLifecycle(["compile-semantics", "--repo", projectRoot], {
    capture: true,
    cwd: projectRoot
  });

  if (result.status !== 0) {
    throw new Error(
      `F# semantic compilation failed with exit code ${result.status}: ${result.error ?? result.stdout ?? "no diagnostic output"}`
    );
  }

  const semanticPath = path.join(stateDirectory, "semantic-output.json");
  const semantic = JSON.parse(await fs.readFile(semanticPath, "utf8"));
  const parsedByPath = new Map(parsedDocuments.map((document) => [document.relativePath, document]));

  const artifacts = semantic.artifacts.map((artifact) => {
    const parsed = parsedByPath.get(artifact.sourcePath);
    if (!parsed) {
      throw new Error(`Semantic artifact ${artifact.sourcePath} has no parsed Markdown source.`);
    }

    return {
      ...artifact,
      ...compatibilityRelationshipLists(artifact),
      schemaVersion: semantic.schemaVersion,
      slug: pathSlug(artifact.url),
      canonicalUrl: artifact.url,
      headings: parsed.headings ?? [],
      links: parsed.links ?? [],
      contentHash: crypto.createHash("sha256").update(parsed.body).digest("hex"),
      compatibilityMode: Object.keys(parsed.frontmatter ?? {}).length === 0
    };
  });

  return {
    ...semantic,
    artifacts
  };
}
