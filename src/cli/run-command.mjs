import fs from "node:fs/promises";
import path from "node:path";
import { loadConfig } from "../build/config.mjs";
import { buildProject } from "../build/project.mjs";
import { inventoryProject } from "../content/inventory.mjs";

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
  const { config, projectRoot, engineRoot } = await loadConfig(configPath);

  if (command === "inventory") {
    await inventoryProject({ projectRoot, config });
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
