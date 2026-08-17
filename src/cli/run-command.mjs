import fs from "node:fs/promises";
import path from "node:path";
import { loadConfig } from "../build/config.mjs";
import { buildProject } from "../build/project.mjs";
import { inventoryProject } from "../content/inventory.mjs";
import { initializeProject } from "./init-project.mjs";
import { installMarkingPrompt } from "./install-prompt.mjs";

function parseArgs(argv) {
  const result = {
    command: argv[2] ?? "build",
    configPath: "./research-publisher.config.mjs"
  };

  for (let index = 3; index < argv.length; index += 1) {
    if (argv[index] === "--config") {
      result.configPath = argv[index + 1];
      index += 1;
    }
  }

  return result;
}

export async function runCommand(argv = process.argv) {
  const { command, configPath } = parseArgs(argv);
  if (command === "init") {
    const result = await initializeProject(process.cwd());
    process.stdout.write([
      result.configCreated ? `Created ${result.configPath}.` : `Kept existing ${result.configPath}.`,
      result.promptCreated ? `Created ${result.promptPath}.` : `Kept existing ${result.promptPath}.`,
      result.scriptsAdded.length > 0
        ? `Added package scripts: ${result.scriptsAdded.join(", ")}.`
        : "Required package scripts already exist.",
      "Next: review research-publisher.config.mjs, then run npm run research:inventory and npm run research:build.",
      ""
    ].join("\n"));
    return;
  }

  const { config, projectRoot, engineRoot } = await loadConfig(configPath);

  if (command === "inventory") {
    await inventoryProject({ projectRoot, config });
    return;
  }

  if (command === "install-prompt") {
    const result = await installMarkingPrompt(projectRoot);
    process.stdout.write(result.created
      ? `Installed document-marking prompt at ${result.path}.\n`
      : `Prompt already exists at ${result.path}; left it unchanged.\n`);
    return;
  }

  if (command === "clean") {
    await fs.rm(path.join(projectRoot, config.output.directory), {
      recursive: true,
      force: true
    });
    await fs.rm(path.join(projectRoot, ".research-publisher"), {
      recursive: true,
      force: true
    });
    return;
  }

  if (command === "validate") {
    const result = await buildProject({ engineRoot, projectRoot, config, mode: "validate" });
    if (result.diagnostics.some((diagnostic) => diagnostic.severity === "error")) {
      process.exitCode = 1;
    }
    return;
  }

  if (command === "check-links") {
    const result = await buildProject({ engineRoot, projectRoot, config, mode: "build" });
    if (result.diagnostics.some((diagnostic) => diagnostic.severity === "error")) {
      process.exitCode = 1;
    }
    return;
  }

  if (command === "preview") {
    process.stdout.write(`Preview output after building ${config.site.title}.\n`);
    return;
  }

  if (command === "migrate") {
    process.stdout.write("Dry-run migration is not yet automated; compatibility mode and inventory reports identify candidates.\n");
    return;
  }

  await buildProject({ engineRoot, projectRoot, config, mode: command === "dev" ? "dev" : "build" });
}
