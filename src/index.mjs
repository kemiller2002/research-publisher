// Public package surface.
//
// The publishing engine lives in JavaScript. The repository lifecycle lives in
// the F# core and is reached through the bridge below, so a consumer never has to
// parse CLI text to drive installation, verification or upgrade.

export { runCommand } from "./cli/run-command.mjs";
export { loadConfig } from "./build/config.mjs";
export { inventoryProject } from "./content/inventory.mjs";
export { normalizeDocument } from "./metadata/normalize.mjs";
export { buildProject } from "./build/project.mjs";

export { initializeProject } from "./cli/init-project.mjs";
export { installMarkingPrompt } from "./cli/install-prompt.mjs";
export { LIFECYCLE_COMMANDS, delegate, delegateJson, runLifecycle } from "./cli/lifecycle.mjs";
