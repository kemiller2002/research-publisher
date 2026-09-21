import fs from "node:fs/promises";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { spawn } from "node:child_process";
import { inventoryProject } from "../content/inventory.mjs";
import { discoverFiles } from "../content/discover.mjs";
import { parseDocument, renderDocumentHtml } from "../content/parse-document.mjs";
import { resolveDocumentLink } from "../content/resolve-link.mjs";
import { buildRelationshipGraph } from "../relationships/graph.mjs";
import { compileSemanticCorpus } from "../semantics/compile.mjs";
import { writeVersionedContracts } from "../output/contracts.mjs";
import { ensureDirectory, writeJson } from "./filesystem.mjs";

function normalizeBaseUrl(baseUrl) {
  if (!baseUrl || baseUrl === "/") return "/";
  const withLeadingSlash = baseUrl.startsWith("/") ? baseUrl : `/${baseUrl}`;
  return withLeadingSlash.endsWith("/") ? withLeadingSlash : `${withLeadingSlash}/`;
}

function withBasePath(baseUrl, targetPath) {
  if (!targetPath || targetPath.startsWith("#")) return targetPath;
  if (/^(?:[a-z]+:)?\/\//i.test(targetPath)) return targetPath;
  const normalizedBase = normalizeBaseUrl(baseUrl);
  const trimmedTarget = targetPath.replace(/^\/+/, "");
  return normalizedBase === "/" ? `/${trimmedTarget}` : `${normalizedBase}${trimmedTarget}`;
}

function increment(group, value) {
  if (value == null || value === "") return;
  group[value] = (group[value] ?? 0) + 1;
}

function summarizeDocuments(documents) {
  const byArtifactType = {};
  const byResearchArea = {};
  const byProject = {};
  const byPurpose = {};
  let unknownArtifactType = 0;
  let unknownResearchArea = 0;

  for (const document of documents) {
    if (document.artifactType) increment(byArtifactType, document.artifactType);
    else unknownArtifactType += 1;

    if (document.researchArea) increment(byResearchArea, document.researchArea);
    else unknownResearchArea += 1;

    increment(byProject, document.project);
    for (const purpose of document.purposes ?? []) increment(byPurpose, purpose);
  }

  return {
    totalDocuments: documents.length,
    classifiedArtifactTypes: Object.values(byArtifactType).reduce((sum, count) => sum + count, 0),
    classifiedResearchAreas: Object.values(byResearchArea).reduce((sum, count) => sum + count, 0),
    unknownArtifactType,
    unknownResearchArea,
    byArtifactType,
    byResearchArea,
    byProject,
    byPurpose
  };
}

function runProcess(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { stdio: "inherit", ...options });
    child.on("exit", (code) => code === 0 ? resolve() : reject(new Error(`${command} ${args.join(" ")} exited with code ${code}`)));
    child.on("error", reject);
  });
}

async function renderAstroSite({ engineRoot, projectRoot, config, dataDirectory, outputDirectory, mode }) {
  const astroRoot = path.join(engineRoot, "site");
  const env = {
    ...process.env,
    ASTRO_TELEMETRY_DISABLED: "1",
    RESEARCH_PUBLISHER_DATA_DIR: dataDirectory,
    RESEARCH_PUBLISHER_PROJECT_ROOT: projectRoot,
    RESEARCH_PUBLISHER_SITE_URL: config.site.siteUrl,
    RESEARCH_PUBLISHER_BASE_URL: config.site.baseUrl,
    RESEARCH_PUBLISHER_MODE: mode
  };

  if (mode === "dev") {
    await runProcess("npx", ["astro", "dev", "--root", astroRoot, "--host"], { cwd: astroRoot, env });
    return;
  }

  await runProcess("npx", ["astro", "build", "--root", astroRoot, "--outDir", outputDirectory], { cwd: astroRoot, env });
}

async function runPagefind({ engineRoot, outputDirectory }) {
  await runProcess("npx", ["pagefind", "--site", outputDirectory], { cwd: engineRoot, env: process.env });
}

async function verifyOutput({ outputDirectory, catalog }) {
  const issues = [];
  for (const document of catalog.records) {
    const outputPath = path.join(outputDirectory, document.url.replace(/^\//, ""), "index.html");
    try {
      await fs.access(outputPath);
    } catch {
      issues.push({
        severity: "error",
        code: "missing-html-output",
        sourcePath: document.sourcePath,
        message: `Expected rendered page at ${outputPath}.`
      });
    }
  }

  for (const document of catalog.records) {
    for (const legacyUrl of document.legacyUrls ?? []) {
      if (!legacyUrl.startsWith("/research/")) continue;
      const redirectPath = path.join(outputDirectory, legacyUrl.replace(/^\//, ""), "index.html");
      try {
        await fs.access(redirectPath);
      } catch {
        issues.push({
          severity: "error",
          code: "lost-published-url",
          sourcePath: document.sourcePath,
          message: `Legacy published URL ${legacyUrl} was not emitted or redirected.`
        });
      }
    }
  }

  for (const requiredPath of [
    "data/research-catalog.json",
    "data/research-graph.json",
    "data/research-guides.json",
    "data/v1/manifest.json",
    "data/v1/artifacts.json",
    "data/v1/edges.json",
    "data/v1/findings.json",
    "data/v1/redirects.json",
    "data/v1/provenance.json",
    "pagefind/pagefind.js"
  ]) {
    const absolute = path.join(outputDirectory, requiredPath);
    try {
      await fs.access(absolute);
    } catch {
      issues.push({
        severity: "error",
        code: "missing-output-artifact",
        sourcePath: requiredPath,
        message: `Missing output artifact ${absolute}.`
      });
    }
  }

  return issues;
}

function createCollections(documents) {
  const grouped = (property) =>
    documents.reduce((accumulator, document) => {
      const value = document[property];
      const values = Array.isArray(value) ? value : [value];
      for (const item of values.filter((candidate) => candidate != null && candidate !== "")) {
        const key = String(item);
        accumulator[key] ??= [];
        accumulator[key].push({
          key: document.key,
          id: document.id,
          title: document.title,
          url: document.url,
          artifactType: document.artifactType,
          summary: document.summary,
          updated: document.updated,
          project: document.project,
          purposes: document.purposes,
          audiences: document.audiences,
          entryPoint: document.entryPoint,
          entryPointOrder: document.entryPointOrder,
          entryPointLabel: document.entryPointLabel,
          status: document.status,
          researchArea: document.researchArea
        });
      }
      return accumulator;
    }, {});

  return {
    artifactTypes: grouped("artifactType"),
    researchAreas: grouped("researchArea"),
    disciplines: grouped("discipline"),
    tags: grouped("tags"),
    statuses: grouped("status"),
    projects: grouped("project"),
    purposes: grouped("purposes"),
    audiences: grouped("audiences")
  };
}

function createGuides(documents) {
  return documents
    .filter((document) => document.entryPoint)
    .sort((left, right) => {
      const projectComparison = (left.project ?? "").localeCompare(right.project ?? "");
      if (projectComparison !== 0) return projectComparison;
      const orderComparison = (left.entryPointOrder ?? 999) - (right.entryPointOrder ?? 999);
      return orderComparison !== 0 ? orderComparison : left.title.localeCompare(right.title);
    })
    .reduce((guides, document) => {
      const project = document.project ?? "unassigned";
      guides[project] ??= [];
      guides[project].push({
        key: document.key,
        id: document.id,
        title: document.title,
        summary: document.summary,
        url: document.url,
        artifactType: document.artifactType,
        project: document.project,
        researchArea: document.researchArea,
        status: document.status,
        updated: document.updated,
        purposes: document.purposes,
        audiences: document.audiences,
        entryPointLabel: document.entryPointLabel,
        order: document.entryPointOrder
      });
      return guides;
    }, {});
}

function publicDocument(document, config) {
  return {
    ...document,
    url: withBasePath(config.site.baseUrl, document.url),
    canonicalUrl: withBasePath(config.site.baseUrl, document.canonicalUrl),
    legacyUrls: (document.legacyUrls ?? []).map((url) => withBasePath(config.site.baseUrl, url))
  };
}

function createPublicCatalog(documents, config) {
  return {
    schemaVersion: "1.1",
    project: config.site.title,
    records: documents.map((document) => publicDocument(document, config))
  };
}

function createPublicGuides(guides, config) {
  return {
    schemaVersion: "1.0",
    projects: createPublicCollections({ guides }, config).guides
  };
}

function createPublicGraph(graph, config) {
  return {
    ...graph,
    nodes: graph.nodes.map((node) => ({ ...node, url: withBasePath(config.site.baseUrl, node.url) }))
  };
}

function createPublicCollections(collections, config) {
  return Object.fromEntries(
    Object.entries(collections).map(([collectionName, terms]) => [
      collectionName,
      Object.fromEntries(
        Object.entries(terms).map(([term, documents]) => [
          term,
          documents.map((document) => ({ ...document, url: withBasePath(config.site.baseUrl, document.url) }))
        ])
      )
    ])
  );
}

function createResearchIndex(documents, config) {
  return {
    schemaVersion: "1.0",
    records: documents.map((document) => ({
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
      legacyUrls: (document.legacyUrls ?? []).map((url) => withBasePath(config.site.baseUrl, url)),
      unknownFrontmatter: document.unknownFrontmatter
    }))
  };
}

function createRelationshipIndex(relationships, documents, config) {
  const byKey = new Map(documents.map((document) => [document.key, document]));
  return {
    schemaVersion: "1.0",
    relationships: relationships.map((relationship) => {
      const target = relationship.targetKey ? byKey.get(relationship.targetKey) : null;
      return {
        ...relationship,
        targetUrl: target ? withBasePath(config.site.baseUrl, target.url) : null
      };
    })
  };
}

function createValidationReport(findings) {
  return {
    schemaVersion: "1.0",
    findings
  };
}

function createPublicationManifest(semantic, documents, relationships, summary) {
  return {
    schemaVersion: "1.0",
    semanticSchemaVersion: semantic.schemaVersion,
    identityPolicy: "declared-id-or-source-path",
    canonicalSource: "ROS Markdown",
    counts: {
      artifacts: documents.length,
      relationships: relationships.length,
      blockingFindings: semantic.findings.filter((finding) => finding.severity === "blocking").length,
      warnings: semantic.findings.filter((finding) => finding.severity === "warning").length,
      unknownArtifactType: summary.unknownArtifactType,
      unknownResearchArea: summary.unknownResearchArea
    },
    capabilities: semantic.capabilities,
    outputs: [
      "research-index.json",
      "relationship-index.json",
      "validation-report.json",
      "publication-manifest.json"
    ]
  };
}

export async function buildProject({ engineRoot, projectRoot, config, mode = "build" }) {
  const startedAt = performance.now();
  const buildGeneratedAt = new Date().toISOString();

  await inventoryProject({ projectRoot, config });

  const discovered = await discoverFiles({
    projectRoot,
    include: config.content.include,
    exclude: config.content.exclude
  });

  const parseStarted = performance.now();
  const parsed = await Promise.all(discovered.map((relativePath) => parseDocument(projectRoot, relativePath)));
  const parseTimeMs = performance.now() - parseStarted;

  const semantic = await compileSemanticCorpus({ projectRoot, parsedDocuments: parsed });
  const parsedBySourcePath = new Map(parsed.map((document) => [document.relativePath, document]));
  const documentsBySourcePath = new Map(semantic.artifacts.map((document) => [document.sourcePath, document]));
  const unresolvedLinkDiagnostics = [];

  const normalized = await Promise.all(semantic.artifacts.map(async (document) => {
    const parsedDocument = parsedBySourcePath.get(document.sourcePath);
    const html = await renderDocumentHtml(parsedDocument.body, (href) => {
      const resolution = resolveDocumentLink({
        sourcePath: document.sourcePath,
        href,
        documentsBySourcePath,
        baseUrl: config.site.baseUrl
      });

      if (resolution.markdown && !resolution.resolved) {
        unresolvedLinkDiagnostics.push({
          severity: "warning",
          code: "unresolved-markdown-link",
          sourcePath: document.sourcePath,
          message: `Markdown link ${href} does not match a published source document.`
        });
      }

      return resolution.href;
    });

    return { ...document, html };
  }));

  normalized.sort((left, right) => left.url.localeCompare(right.url));

  const semanticDiagnostics = semantic.findings.map((finding) => ({
    ...finding,
    severity: finding.severity === "blocking" ? "error" : finding.severity
  }));

  const diagnostics = semanticDiagnostics.concat(unresolvedLinkDiagnostics);
  const graph = buildRelationshipGraph(normalized, semantic.relationships);
  const outputDirectory = path.join(projectRoot, config.output.directory);
  const internalDirectory = path.join(projectRoot, ".research-publisher", path.basename(config.output.directory) || "dist");
  const dataDirectory = path.join(internalDirectory, "data");

  await ensureDirectory(dataDirectory);

  const summary = summarizeDocuments(normalized);
  const internalCatalog = { schemaVersion: "2.0", project: config.site.title, records: normalized };
  const catalog = createPublicCatalog(normalized, config);
  const publicGraph = createPublicGraph(graph, config);
  const collections = createCollections(normalized);
  const guides = createGuides(normalized);
  const publicCollections = createPublicCollections(collections, config);
  const publicGuides = createPublicGuides(guides, config);

  await writeJson(path.join(dataDirectory, "catalog.json"), internalCatalog);
  await writeJson(path.join(dataDirectory, "graph.json"), graph);
  await writeJson(path.join(dataDirectory, "collections.json"), collections);
  await writeJson(path.join(dataDirectory, "guides.json"), guides);
  await writeJson(path.join(dataDirectory, "semantics.json"), semantic);
  await writeJson(path.join(dataDirectory, "site.json"), {
    site: { ...config.site, branding: config.branding },
    repository: config.repository,
    features: config.features,
    summary
  });

  if (mode === "validate") {
    await writeJson(path.join(projectRoot, "build-reports/validation-diagnostics.json"), diagnostics);
    return { diagnostics, semantic };
  }

  await fs.rm(outputDirectory, { recursive: true, force: true });

  const renderStarted = performance.now();
  await renderAstroSite({ engineRoot, projectRoot, config, dataDirectory, outputDirectory, mode });
  const renderTimeMs = performance.now() - renderStarted;

  await ensureDirectory(path.join(outputDirectory, "data"));
  await writeJson(path.join(outputDirectory, config.output.catalog), catalog);
  await writeJson(path.join(outputDirectory, "data/research-graph.json"), publicGraph);
  await writeJson(path.join(outputDirectory, "data/research-collections.json"), publicCollections);
  await writeJson(path.join(outputDirectory, "data/research-guides.json"), publicGuides);
  await writeVersionedContracts({
    outputDirectory,
    semantic,
    documents: normalized,
    config,
    summary
  });

  const pagefindStarted = performance.now();
  await runPagefind({ engineRoot, outputDirectory });
  const pagefindTimeMs = performance.now() - pagefindStarted;

  const verificationDiagnostics = await verifyOutput({ outputDirectory, catalog: internalCatalog });
  const finalDiagnostics = diagnostics.concat(verificationDiagnostics);
  const publicBuildDiagnostics = {
    schemaVersion: "2.0",
    summary,
    diagnostics: finalDiagnostics
  };

  const executionReport = {
    schemaVersion: "1.0",
    generatedOn: buildGeneratedAt,
    performance: {
      documentCount: normalized.length,
      parseTimeMs,
      renderTimeMs,
      searchIndexTimeMs: pagefindTimeMs,
      totalBuildTimeMs: performance.now() - startedAt
    },
    summary,
    diagnostics: finalDiagnostics
  };

  await writeJson(path.join(outputDirectory, config.output.diagnostics), publicBuildDiagnostics);
  await writeJson(path.join(projectRoot, "build-reports/build-diagnostics.json"), executionReport);

  if (finalDiagnostics.some((diagnostic) => diagnostic.severity === "error")) {
    throw new Error("Build completed with semantic or output validation errors.");
  }

  return { catalog, graph: publicGraph, diagnostics: finalDiagnostics, semantic };
}
