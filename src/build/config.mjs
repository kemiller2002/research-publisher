import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const defaultConfig = {
  site: {
    title: "Research Publisher",
    description: "Searchable research site",
    baseUrl: "/",
    language: "en",
    siteUrl: "https://example.com"
  },
  repository: {
    name: "research-publisher",
    sourceUrl: ""
  },
  content: {
    include: [
      "research/**/*.md"
    ],
    exclude: [
      "dist/**",
      "node_modules/**"
    ],
    drafts: false
  },
  metadata: {
    mode: "compatible",
    strictInCI: true,
    required: [
      "title"
    ],
    stableIdPrefixes: [
      "RP",
      "JR",
      "EV",
      "HY",
      "TH",
      "EX",
      "DF",
      "CN",
      "GL"
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
    logo: null,
    cssVariables: {}
  }
};

function merge(target, source) {
  const result = {
    ...target
  };

  for (const [key, value] of Object.entries(source ?? {})) {
    if (Array.isArray(value)) {
      result[key] = value.slice();
      continue;
    }

    if (value && typeof value === "object") {
      result[key] = merge(target[key] ?? {}, value);
      continue;
    }

    result[key] = value;
  }

  return result;
}

export async function loadConfig(configPath) {
  const resolvedPath = path.resolve(configPath);
  await fs.access(resolvedPath);
  const imported = await import(pathToFileURL(resolvedPath).href);
  const config = merge(defaultConfig, imported.default ?? {});
  const projectRoot = path.dirname(resolvedPath);
  const engineRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
  return {
    config,
    configPath: resolvedPath,
    projectRoot,
    engineRoot
  };
}
