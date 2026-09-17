// Compatibility wrapper.
//
// `init` is implemented in the F# lifecycle core. This module keeps the original
// JavaScript entry point working for anything that imported it directly, and
// translates the CLI's JSON result into the shape it used to return.

import path from "node:path";
import { delegateJson } from "./lifecycle.mjs";

const CONFIG_PATH = "research-publisher.config.mjs";
const PROMPT_PATH = "prompts/research-publisher-mark-documents.md";
const SCRIPT_PREFIX = "package.json#scripts.";

function createdPaths(report) {
  return (report.applied ?? [])
    .filter((change) => change.kind === "create-file" && change.outcome === "applied")
    .map((change) => change.target);
}

function addedScripts(report) {
  return (report.applied ?? [])
    .filter((change) => change.kind === "add-package-script" && change.outcome === "applied")
    .map((change) => change.target.slice(SCRIPT_PREFIX.length));
}

function preservedScripts(report) {
  return (report.skipped ?? [])
    .filter((entry) => entry.target.startsWith(SCRIPT_PREFIX))
    .map((entry) => entry.target.slice(SCRIPT_PREFIX.length));
}

export async function initializeProject(projectRoot = process.cwd()) {
  const report = delegateJson(["init", "--repo", projectRoot]);
  const created = createdPaths(report);

  return {
    projectRoot,
    packagePath: path.join(projectRoot, "package.json"),
    configPath: path.join(projectRoot, CONFIG_PATH),
    configCreated: created.includes(CONFIG_PATH),
    promptPath: path.join(projectRoot, PROMPT_PATH),
    promptCreated: created.includes(PROMPT_PATH),
    scriptsAdded: addedScripts(report),
    scriptsPreserved: preservedScripts(report)
  };
}
