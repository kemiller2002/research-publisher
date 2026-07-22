import fs from "node:fs/promises";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { spawn } from "node:child_process";
import { inventoryProject } from "../content/inventory.mjs";
import { discoverFiles } from "../content/discover.mjs";
import { parseDocument } from "../content/parse-document.mjs";
import { normalizeDocument } from "../metadata/normalize.mjs";
import { validateDocuments } from "../validation/validate.mjs";
import { buildRelationshipGraph } from "../relationships/graph.mjs";
import { ensureDirectory, writeJson } from "./filesystem.mjs";

function summarizeDocuments(documents) {
  const byArtifactType = {};
  const byResearchArea = {};
  for (const document of documents) {
    byArtifactType[document.artifactType] = (byArtifactType[document.artifactType] ?? 0) + 1;
    byResearchArea[document.researchArea] = (byResearchArea[document.researchArea] ?? 0) + 1;
  }
  return {
    totalDocuments: documents.length,
    byArtifactType,
    byResearchArea
  };
}

function runProcess(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      stdio: "inherit",
      ...options
    });

    child.on("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`${command} ${args.join(" ")} exited with code ${code}`));
      }
    });
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
    await runProcess("npx", ["astro", "dev", "--root", astroRoot, "--host"], {
      cwd: astroRoot,
      env
    });
    return;
  }

  await runProcess("npx", ["astro", "build", "--root", astroRoot, "--outDir", outputDirectory], {
    cwd: astroRoot,
    env
  });
}

async function runPagefind({ engineRoot, outputDirectory }) {
  await runProcess("npx", ["pagefind", "--site", outputDirectory], {
    cwd: engineRoot,
    env: process.env
  });
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

  for (const requiredPath of [
    path.join(outputDirectory, "data/research-catalog.json"),
    path.join(outputDirectory, "data/research-graph.json"),
    path.join(outputDirectory, "pagefind/pagefind.js")
  ]) {
    try {
      await fs.access(requiredPath);
    } catch {
      issues.push({
        severity: "error",
        code: "missing-output-artifact",
        sourcePath: requiredPath,
        message: `Missing output artifact ${requiredPath}.`
      });
    }
  }

  return issues;
}

function createCollections(documents) {
  const grouped = (property) =>
    documents.reduce((accumulator, document) => {
      const values = Array.isArray(document[property]) ? document[property] : [document[property]];
      for (const value of values.filter(Boolean)) {
        const key = String(value);
        accumulator[key] ??= [];
        accumulator[key].push({
          id: document.id,
          title: document.title,
          url: document.url,
          artifactType: document.artifactType,
          summary: document.summary,
          updated: document.updated
        });
      }
      return accumulator;
    }, {});

  return {
    artifactTypes: grouped("artifactType"),
    researchAreas: grouped("researchArea"),
    disciplines: grouped("discipline"),
    tags: grouped("tags"),
    statuses: grouped("status")
  };
}

export async function buildProject({ engineRoot, projectRoot, config, mode = "build" }) {
  const startedAt = performance.now();
  await inventoryProject({ projectRoot, config });
  const discovered = await discoverFiles({
    projectRoot,
    include: config.content.include,
    exclude: config.content.exclude
  });

  const parseStarted = performance.now();
  const parsed = await Promise.all(discovered.map((relativePath) => parseDocument(projectRoot, relativePath)));
  const parseTimeMs = performance.now() - parseStarted;

  const normalized = parsed.map(normalizeDocument).sort((left, right) => left.url.localeCompare(right.url));
  const diagnostics = validateDocuments(normalized);
  const graph = buildRelationshipGraph(normalized);
  const outputDirectory = path.join(projectRoot, config.output.directory);
  const internalDirectory = path.join(projectRoot, ".research-publisher", path.basename(config.output.directory) || "dist");
  const dataDirectory = path.join(internalDirectory, "data");
  await ensureDirectory(dataDirectory);
  const catalog = {
    schemaVersion: "1.0",
    generatedOn: "2026-07-22",
    project: config.site.title,
    records: normalized
  };
  const collections = createCollections(normalized);

  await writeJson(path.join(dataDirectory, "catalog.json"), catalog);
  await writeJson(path.join(dataDirectory, "graph.json"), graph);
  await writeJson(path.join(dataDirectory, "collections.json"), collections);
  await writeJson(path.join(dataDirectory, "site.json"), {
    site: {
      ...config.site,
      branding: config.branding
    },
    repository: config.repository,
    features: config.features,
    summary: summarizeDocuments(normalized)
  });

  if (mode === "validate") {
    await writeJson(path.join(projectRoot, "build-reports/validation-diagnostics.json"), diagnostics);
    return {
      diagnostics
    };
  }

  await fs.rm(outputDirectory, {
    recursive: true,
    force: true
  });

  const renderStarted = performance.now();
  await renderAstroSite({ engineRoot, projectRoot, config, dataDirectory, outputDirectory, mode });
  const renderTimeMs = performance.now() - renderStarted;

  await ensureDirectory(path.join(outputDirectory, "data"));
  await writeJson(path.join(outputDirectory, config.output.catalog), catalog);
  await writeJson(path.join(outputDirectory, "data/research-graph.json"), graph);
  await writeJson(path.join(outputDirectory, "data/build-diagnostics.json"), {
    schemaVersion: "1.0",
    generatedOn: "2026-07-22",
    performance: {
      parseTimeMs,
      renderTimeMs,
      totalBuildTimeMs: performance.now() - startedAt
    },
    summary: summarizeDocuments(normalized),
    diagnostics
  });
  await writeJson(path.join(outputDirectory, "data/research-collections.json"), collections);

  const pagefindStarted = performance.now();
  await runPagefind({ engineRoot, outputDirectory });
  const pagefindTimeMs = performance.now() - pagefindStarted;
  const verificationDiagnostics = await verifyOutput({ outputDirectory, catalog });
  const finalDiagnostics = diagnostics.concat(verificationDiagnostics);
  const buildDiagnostics = {
    schemaVersion: "1.0",
    generatedOn: "2026-07-22",
    performance: {
      documentCount: normalized.length,
      parseTimeMs,
      renderTimeMs,
      searchIndexTimeMs: pagefindTimeMs,
      totalBuildTimeMs: performance.now() - startedAt
    },
    summary: summarizeDocuments(normalized),
    diagnostics: finalDiagnostics
  };
  await writeJson(path.join(outputDirectory, config.output.diagnostics), buildDiagnostics);
  await writeJson(path.join(projectRoot, "build-reports/build-diagnostics.json"), buildDiagnostics);

  if (finalDiagnostics.some((diagnostic) => diagnostic.severity === "error")) {
    throw new Error("Build completed with validation errors.");
  }

  return {
    catalog,
    graph,
    diagnostics: finalDiagnostics
  };
}
