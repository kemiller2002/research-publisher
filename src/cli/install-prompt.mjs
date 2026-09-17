// Compatibility wrapper.
//
// Installing the shared document-marking prompt is part of the installed state,
// so the decision lives in the F# lifecycle core. This module preserves the
// original JavaScript entry point and return shape.

import path from "node:path";
import { delegateJson } from "./lifecycle.mjs";

const PROMPT_PATH = "prompts/research-publisher-mark-documents.md";

export async function installMarkingPrompt(projectRoot) {
  const report = delegateJson(["install-prompt", "--repo", projectRoot]);

  const created = (report.applied ?? []).some(
    (change) => change.target === PROMPT_PATH && change.outcome === "applied"
  );

  return {
    created,
    path: path.join(projectRoot, PROMPT_PATH)
  };
}
