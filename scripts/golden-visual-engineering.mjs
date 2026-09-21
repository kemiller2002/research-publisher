import path from "node:path";
import { loadConfig } from "../src/build/config.mjs";
import { buildProject } from "../src/build/project.mjs";

const targetRoot = path.resolve(process.argv[2] ?? "../visual-engineering");
const configPath = path.join(targetRoot, "research-publisher.config.mjs");
const { config, projectRoot, engineRoot } = await loadConfig(configPath);
const result = await buildProject({
  engineRoot,
  projectRoot,
  config,
  mode: "validate"
});

const artifacts = result.semantic.artifacts;
const relationships = result.semantic.relationships;
const resolved = relationships.filter((relationship) => relationship.resolution === "resolved");
const derived = relationships.filter((relationship) => relationship.authority === "derived");
const resolvedDerived = derived.filter((relationship) => relationship.resolution === "resolved");
const unresolvedDerived = derived.filter((relationship) => relationship.resolution !== "resolved");
const declared = relationships.filter((relationship) => relationship.authority !== "derived");

const byAuthority = Object.fromEntries(
  ["canonical", "declared-unclassified", "derived"].map((authority) => [
    authority,
    relationships.filter((relationship) => relationship.authority === authority).length
  ])
);

const byRelation = relationships.reduce((counts, relationship) => {
  counts[relationship.relation] = (counts[relationship.relation] ?? 0) + 1;
  return counts;
}, {});

const findingsByCode = result.semantic.findings.reduce((counts, finding) => {
  counts[finding.code] = (counts[finding.code] ?? 0) + 1;
  return counts;
}, {});

const report = {
  artifactCount: artifacts.length,
  relationshipCount: relationships.length,
  resolvedRelationshipCount: resolved.length,
  declaredRelationshipCount: declared.length,
  derivedRelationshipCount: derived.length,
  resolvedDerivedRelationshipCount: resolvedDerived.length,
  unresolvedDerivedRelationshipCount: unresolvedDerived.length,
  byAuthority,
  byRelation,
  findingsByCode
};

process.stdout.write(JSON.stringify(report, null, 2) + "\n");

const failures = [];

if (artifacts.length < 700) {
  failures.push(`Expected at least 700 published artifacts from the real Visual Engineering corpus; found ${artifacts.length}.`);
}

if (resolved.length < 850) {
  failures.push(`Expected at least 850 resolved declared/derived relationships; found ${resolved.length}.`);
}

if (resolvedDerived.length < 800) {
  failures.push(`Expected the declared frontier pipeline to contribute at least 800 resolved derived relationships; found ${resolvedDerived.length}.`);
}

if (artifacts.some((artifact) => artifact.researchArea === "General Research")) {
  failures.push("The corpus contains a fabricated General Research fallback.");
}

if (artifacts.some((artifact) => artifact.artifactType === "research-document")) {
  failures.push("The corpus contains a fabricated research-document fallback.");
}

if (failures.length > 0) {
  for (const failure of failures) process.stderr.write(`golden-corpus: ${failure}\n`);
  process.exit(1);
}
