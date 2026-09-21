import path from "node:path";
import { ensureDirectory, writeJson } from "../build/filesystem.mjs";

function normalizeBaseUrl(baseUrl) {
  if (!baseUrl || baseUrl === "/") return "/";
  const withLeadingSlash = baseUrl.startsWith("/") ? baseUrl : `/${baseUrl}`;
  return withLeadingSlash.endsWith("/") ? withLeadingSlash : `${withLeadingSlash}/`;
}

function withBasePath(baseUrl, targetPath) {
  if (!targetPath || targetPath.startsWith("#")) return targetPath;
  if (/^(?:[a-z]+:)?\/\//i.test(targetPath)) return targetPath;
  const base = normalizeBaseUrl(baseUrl);
  const target = targetPath.replace(/^\/+/, "");
  return base === "/" ? `/${target}` : `${base}${target}`;
}

function shardKey(key) {
  if (/^[A-Za-z0-9._-]+$/.test(key)) return key;
  return `k-${Buffer.from(key, "utf8").toString("base64url")}`;
}

function stripPresentation(document) {
  const {
    html,
    sourceMarkdown,
    links,
    headings,
    compatibilityMode,
    ...semantic
  } = document;
  return semantic;
}

function artifactIndexRecord(document, config) {
  const fileKey = shardKey(document.key);
  return {
    key: document.key,
    keyKind: document.keyKind,
    id: document.id,
    title: document.title,
    artifactType: document.artifactType,
    typeSource: document.typeSource,
    project: document.project,
    purposes: document.purposes,
    audiences: document.audiences,
    researchArea: document.researchArea,
    status: document.status,
    created: document.created,
    updated: document.updated,
    sourcePath: document.sourcePath,
    url: withBasePath(config.site.baseUrl, document.url),
    detail: withBasePath(config.site.baseUrl, `/data/v1/artifact/${fileKey}.json`)
  };
}

function edgeRecord(relationship, byKey, config) {
  const source = byKey.get(relationship.sourceKey);
  const target = relationship.targetKey ? byKey.get(relationship.targetKey) : null;
  return {
    ...relationship,
    derived: relationship.authority === "derived",
    sourceUrl: source ? withBasePath(config.site.baseUrl, source.url) : null,
    targetUrl: target ? withBasePath(config.site.baseUrl, target.url) : null,
    evidence: {
      sourcePath: relationship.evidenceSource ?? relationship.sourcePath,
      field: relationship.field,
      rawTarget: relationship.rawTarget
    }
  };
}

export async function writeVersionedContracts({
  outputDirectory,
  semantic,
  documents,
  config,
  summary
}) {
  const root = path.join(outputDirectory, "data/v1");
  const artifactDirectory = path.join(root, "artifact");
  const edgeDirectory = path.join(root, "edges");
  await ensureDirectory(artifactDirectory);
  await ensureDirectory(edgeDirectory);

  const byKey = new Map(documents.map((document) => [document.key, document]));
  const edges = semantic.relationships.map((relationship) => edgeRecord(relationship, byKey, config));
  const artifacts = documents.map((document) => artifactIndexRecord(document, config));

  const redirects = Object.fromEntries(
    documents.flatMap((document) =>
      (document.legacyUrls ?? []).map((legacyUrl) => [
        withBasePath(config.site.baseUrl, legacyUrl),
        withBasePath(config.site.baseUrl, document.url)
      ])
    )
  );

  const provenance = Object.fromEntries(
    documents.map((document) => [
      document.key,
      {
        repository: config.repository.sourceUrl || config.repository.name || null,
        path: document.sourcePath,
        id: document.id,
        contentHash: document.contentHash
      }
    ])
  );

  for (const document of documents) {
    const fileKey = shardKey(document.key);
    const outgoing = edges.filter((edge) => edge.sourceKey === document.key);
    const incoming = edges.filter((edge) => edge.targetKey === document.key);

    await writeJson(path.join(artifactDirectory, `${fileKey}.json`), {
      schemaVersion: "1.0",
      artifact: {
        ...stripPresentation(document),
        sourceMarkdown: document.sourceMarkdown,
        provenance: provenance[document.key]
      },
      relationships: {
        outgoing,
        incoming
      }
    });

    await writeJson(path.join(edgeDirectory, `${fileKey}.json`), {
      schemaVersion: "1.0",
      key: document.key,
      outgoing,
      incoming
    });
  }

  const manifest = {
    schemaVersion: "1.0",
    semanticSchemaVersion: semantic.schemaVersion,
    contractVersion: "1",
    canonicalSource: "ROS Markdown",
    identityPolicy: "declared-id-or-source-path",
    counts: {
      artifacts: documents.length,
      relationships: edges.length,
      blockingFindings: semantic.findings.filter((finding) => finding.severity === "blocking").length,
      warnings: semantic.findings.filter((finding) => finding.severity === "warning").length,
      unknownArtifactType: summary.unknownArtifactType,
      unknownResearchArea: summary.unknownResearchArea
    },
    capabilities: semantic.capabilities,
    indexes: {
      artifacts: "artifacts.json",
      edges: "edges.json",
      findings: "findings.json",
      redirects: "redirects.json",
      provenance: "provenance.json"
    },
    shardTemplates: {
      artifact: "artifact/{artifact.detail-key}.json",
      edges: "edges/{artifact.detail-key}.json"
    }
  };

  await writeJson(path.join(root, "manifest.json"), manifest);
  await writeJson(path.join(root, "artifacts.json"), { schemaVersion: "1.0", artifacts });
  await writeJson(path.join(root, "edges.json"), { schemaVersion: "1.0", edges });
  await writeJson(path.join(root, "findings.json"), { schemaVersion: "1.0", findings: semantic.findings });
  await writeJson(path.join(root, "redirects.json"), { schemaVersion: "1.0", redirects });
  await writeJson(path.join(root, "provenance.json"), { schemaVersion: "1.0", artifacts: provenance });

  // Transitional aliases for the implementation branch and early adopters.
  await writeJson(path.join(root, "research-index.json"), { schemaVersion: "1.0", records: artifacts });
  await writeJson(path.join(root, "relationship-index.json"), { schemaVersion: "1.0", relationships: edges });
  await writeJson(path.join(root, "validation-report.json"), { schemaVersion: "1.0", findings: semantic.findings });
  await writeJson(path.join(root, "publication-manifest.json"), manifest);

  return manifest;
}
