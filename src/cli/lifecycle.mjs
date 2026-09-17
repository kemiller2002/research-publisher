// Bridge from the JavaScript publishing engine to the F# lifecycle CLI.
//
// The engine keeps its own commands; every lifecycle decision is delegated here so
// there is exactly one implementation of what "installed" means.

import { runLifecycle } from "../../bin/lifecycle-runtime.js";

export { runLifecycle };

/** Lifecycle commands handled by the F# CLI rather than by the engine. */
export const LIFECYCLE_COMMANDS = new Set([
  "init",
  "status",
  "verify",
  "upgrade",
  "doctor",
  "install-prompt"
]);

/** Run a lifecycle command with the caller's streams attached. */
export function delegate(args) {
  const result = runLifecycle(args);
  if (result.error) {
    throw new Error(result.error);
  }
  return result.status;
}

/**
 * Run a lifecycle command and return its JSON document.
 * Throws with the blocking problem when the command refused to run.
 */
export function delegateJson(args) {
  const result = runLifecycle([...args, "--json"], { capture: true });

  if (result.error) {
    throw new Error(result.error);
  }

  let report;
  try {
    report = JSON.parse(result.stdout);
  } catch {
    throw new Error(`The lifecycle CLI did not return JSON (exit ${result.status}).`);
  }

  if (report.schema === "research-publisher.error/1") {
    throw new Error(report.message);
  }

  const blockers = report.blockers ?? [];
  if (blockers.length > 0) {
    const [blocker] = blockers;
    throw new Error([blocker.title, blocker.detail, blocker.remediation].filter(Boolean).join(" "));
  }

  if (result.status !== 0) {
    throw new Error(`The lifecycle CLI exited with code ${result.status}.`);
  }

  return report;
}
