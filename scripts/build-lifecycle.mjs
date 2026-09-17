// Publishes the F# lifecycle CLI into runtimes/<rid>/ so npm can ship it.
//
// Usage:
//   node scripts/build-lifecycle.mjs               # the current platform
//   node scripts/build-lifecycle.mjs --all         # every supported platform
//   node scripts/build-lifecycle.mjs --rid win-x64 # one named platform
//   node scripts/build-lifecycle.mjs --if-missing  # skip when already staged

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { SUPPORTED_RUNTIMES, runtimeIdentifier } from "../bin/lifecycle-runtime.js";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const project = path.join(repositoryRoot, "src", "ResearchPublisher.Lifecycle.Cli", "ResearchPublisher.Lifecycle.Cli.fsproj");

const argv = process.argv.slice(2);
const ifMissing = argv.includes("--if-missing");
const buildAll = argv.includes("--all");
const ridIndex = argv.indexOf("--rid");
const explicitRid = ridIndex >= 0 ? argv[ridIndex + 1] : null;

function targets() {
  if (buildAll) return SUPPORTED_RUNTIMES;
  if (explicitRid) return [explicitRid];

  const rid = runtimeIdentifier();
  if (!rid) {
    console.error(`No packaged runtime for ${process.platform}-${process.arch}.`);
    process.exit(1);
  }
  return [rid];
}

function executableFor(rid) {
  return rid.startsWith("win-") ? "research-publisher-lifecycle.exe" : "research-publisher-lifecycle";
}

function publish(rid) {
  const destination = path.join(repositoryRoot, "runtimes", rid);
  const executable = path.join(destination, executableFor(rid));

  if (ifMissing && fs.existsSync(executable)) {
    console.log(`runtimes/${rid}: already staged`);
    return;
  }

  console.log(`runtimes/${rid}: publishing`);

  const result = spawnSync(
    "dotnet",
    [
      "publish",
      project,
      "--configuration",
      "Release",
      "--runtime",
      rid,
      "--self-contained",
      "true",
      "--output",
      destination,
      "-p:PublishSingleFile=true",
      "-p:PublishTrimmed=true",
      "-p:DebugType=none",
      "-p:GenerateDependencyFile=false"
    ],
    { stdio: "inherit", cwd: repositoryRoot }
  );

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }

  if (!fs.existsSync(executable)) {
    console.error(`Expected ${executable} to exist after publishing.`);
    process.exit(1);
  }

  // A single-file publish leaves the launcher and nothing else worth shipping.
  for (const entry of fs.readdirSync(destination)) {
    if (entry !== executableFor(rid)) {
      fs.rmSync(path.join(destination, entry), { recursive: true, force: true });
    }
  }

  if (!rid.startsWith("win-")) {
    fs.chmodSync(executable, 0o755);
  }

  const bytes = fs.statSync(executable).size;
  console.log(`runtimes/${rid}: ${(bytes / (1024 * 1024)).toFixed(1)} MiB`);
}

for (const rid of targets()) {
  if (!SUPPORTED_RUNTIMES.includes(rid)) {
    console.error(`Unknown runtime identifier '${rid}'. Supported: ${SUPPORTED_RUNTIMES.join(", ")}.`);
    process.exit(1);
  }
  publish(rid);
}
