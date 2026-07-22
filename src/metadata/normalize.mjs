import crypto from "node:crypto";
import path from "node:path";

const artifactPrefixMap = {
  RP: "research-package",
  JR: "journal-entry",
  EV: "evidence",
  HY: "hypothesis",
  TH: "theory",
  EX: "experiment",
  DF: "decision-framework",
  CN: "concept",
  GL: "glossary"
};

const legacyAliases = {
  identifier: "id",
  stableId: "id",
  research_area: "researchArea",
  artifact_type: "artifactType",
  updated_at: "updated",
  created_at: "created",
  author: "authorAgent"
};

function normalizeArray(value) {
  if (value == null || value === "") {
    return [];
  }

  if (Array.isArray(value)) {
    return value.map((item) => String(item).trim()).filter(Boolean);
  }

  return String(value)
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function inferTitle(relativePath, body) {
  const firstHeading = body.split("\n").find((line) => line.startsWith("# "));
  if (firstHeading) {
    return firstHeading.replace(/^#\s+/, "").trim();
  }

  return path.basename(relativePath, path.extname(relativePath)).replace(/[-_]/g, " ");
}

function inferIdFromPath(relativePath) {
  const name = path.basename(relativePath, path.extname(relativePath));
  const match = name.match(/^([A-Z]{2})-\d{4}-\d{3}/);
  return match ? match[0] : null;
}

function inferArtifactType(id, relativePath) {
  const prefix = id?.split("-")[0];
  if (prefix && artifactPrefixMap[prefix]) {
    return artifactPrefixMap[prefix];
  }

  if (relativePath.includes("/evidence/")) {
    return "evidence";
  }

  if (relativePath.includes("/hypotheses/")) {
    return "hypothesis";
  }

  if (relativePath.includes("/theories/")) {
    return "theory";
  }

  if (relativePath.includes("/journals/")) {
    return "journal-entry";
  }

  if (relativePath.includes("/glossary/")) {
    return "glossary";
  }

  if (relativePath.includes("/concept")) {
    return "concept";
  }

  return "research-document";
}

function normalizeDate(value) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return date.toISOString().slice(0, 10);
}

function toNumber(value) {
  if (value == null || value === "") {
    return null;
  }

  if (typeof value === "number") {
    return value;
  }

  const lowered = String(value).trim().toLowerCase();
  if (lowered === "high") {
    return 0.9;
  }
  if (lowered === "medium") {
    return 0.6;
  }
  if (lowered === "low") {
    return 0.3;
  }

  const parsed = Number(lowered);
  return Number.isFinite(parsed) ? parsed : null;
}

function slugify(value) {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

export function normalizeDocument(parsed) {
  const frontmatter = {};
  for (const [key, value] of Object.entries(parsed.frontmatter ?? {})) {
    frontmatter[legacyAliases[key] ?? key] = value;
  }

  const inferredId = inferIdFromPath(parsed.relativePath);
  const id = frontmatter.id ? String(frontmatter.id).trim() : inferredId;
  const title = frontmatter.title ? String(frontmatter.title).trim() : inferTitle(parsed.relativePath, parsed.body);
  const artifactType = frontmatter.artifactType ? String(frontmatter.artifactType).trim() : inferArtifactType(id, parsed.relativePath);
  const slug = frontmatter.slug ? String(frontmatter.slug).trim() : slugify(id ? `${id}-${title}` : title);
  const url = frontmatter.url ? String(frontmatter.url).trim() : `/research/${slug}/`;
  const contentHash = crypto.createHash("sha256").update(parsed.body).digest("hex");

  return {
    schemaVersion: "1.0",
    id,
    title,
    slug,
    url,
    artifactType,
    researchArea: frontmatter.researchArea ? String(frontmatter.researchArea) : "General Research",
    discipline: normalizeArray(frontmatter.discipline),
    summary: frontmatter.summary ? String(frontmatter.summary).trim() : parsed.excerpt || "",
    status: frontmatter.status ? String(frontmatter.status).trim() : "draft",
    version: frontmatter.version ? String(frontmatter.version) : "0.1",
    confidence: toNumber(frontmatter.confidence),
    completion: toNumber(frontmatter.completion),
    priority: frontmatter.priority ? String(frontmatter.priority).trim() : "medium",
    authorAgent: frontmatter.authorAgent ? String(frontmatter.authorAgent).trim() : "unknown",
    created: normalizeDate(frontmatter.created) ?? "2026-07-22",
    updated: normalizeDate(frontmatter.updated) ?? "2026-07-22",
    tags: normalizeArray(frontmatter.tags),
    keywords: normalizeArray(frontmatter.keywords),
    relatedProjects: normalizeArray(frontmatter.relatedProjects),
    relatedDocuments: normalizeArray(frontmatter.relatedDocuments),
    supersedes: normalizeArray(frontmatter.supersedes),
    supersededBy: normalizeArray(frontmatter.supersededBy),
    evidenceIds: normalizeArray(frontmatter.evidenceIds),
    hypothesisIds: normalizeArray(frontmatter.hypothesisIds),
    theoryIds: normalizeArray(frontmatter.theoryIds),
    headings: parsed.headings,
    sourcePath: parsed.relativePath,
    contentHash,
    html: parsed.html,
    links: parsed.links,
    compatibilityMode: Object.keys(parsed.frontmatter ?? {}).length === 0
  };
}

