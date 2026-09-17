// Exercises the published npm artifact, not the source tree.
//
// Packs the package, installs the tarball into a clean temporary repository and
// runs every command the README documents, asserting exit codes, idempotency and
// JSON validity. `dotnet test` cannot prove any of this.

import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";

const repositoryRoot = process.cwd();

// npm ships as npm.cmd on Windows, and spawnSync does not resolve it without a
// shell. Naming the executable directly keeps the tarball path out of a shell.
const npm = process.platform === "win32" ? "npm.cmd" : "npm";

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    encoding: "utf8",
    ...options,
    env: { ...process.env, ...options.env }
  });

  if (result.error) {
    throw result.error;
  }

  return { status: result.status, stdout: result.stdout ?? "", stderr: result.stderr ?? "" };
}

function expectExit(label, result, expected) {
  if (result.status !== expected) {
    throw new Error(
      `${label}: expected exit ${expected}, got ${result.status}\n--- stdout ---\n${result.stdout}\n--- stderr ---\n${result.stderr}`
    );
  }
  console.log(`  ok  ${label} (exit ${result.status})`);
}

function parseJson(label, result) {
  try {
    return JSON.parse(result.stdout);
  } catch {
    throw new Error(`${label}: stdout was not valid JSON.\n${result.stdout}`);
  }
}

async function snapshot(root) {
  const entries = [];

  async function walk(directory) {
    for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
      if (entry.name === "node_modules" || entry.name === ".git") continue;
      const full = path.join(directory, entry.name);
      if (entry.isDirectory()) {
        await walk(full);
      } else {
        const contents = await fs.readFile(full);
        const digest = crypto.createHash("sha256").update(contents).digest("hex");
        entries.push(`${path.relative(root, full)}=${digest}`);
      }
    }
  }

  await walk(root);
  return entries.sort().join("\n");
}

async function main() {
  const temporaryRoot = await fs.mkdtemp(path.join(os.tmpdir(), "research-publisher-package-"));
  const packDirectory = path.join(temporaryRoot, "pack");
  const consumer = path.join(temporaryRoot, "consumer");
  const npmCache = path.join(temporaryRoot, "npm-cache");

  await fs.mkdir(packDirectory, { recursive: true });
  await fs.mkdir(consumer, { recursive: true });
  await fs.mkdir(npmCache, { recursive: true });

  const npmEnv = { npm_config_cache: npmCache };

  console.log("Packing the package...");
  const packed = run(npm, ["pack", "--pack-destination", packDirectory], {
    cwd: repositoryRoot,
    env: npmEnv
  });
  expectExit("npm pack", packed, 0);

  const tarballs = (await fs.readdir(packDirectory)).filter((name) => name.endsWith(".tgz"));
  assert.equal(tarballs.length, 1, "expected exactly one packed tarball");
  const tarball = path.join(packDirectory, tarballs[0]);

  // The archive must carry the launcher and at least the host platform binary.
  const listing = run("tar", ["-tzf", tarball]);
  expectExit("tar -tzf", listing, 0);
  const contents = listing.stdout.split("\n").filter(Boolean).map((entry) => entry.replace(/^package\//, ""));

  for (const required of [
    "package.json",
    "README.md",
    "bin/research-publisher.js",
    "bin/lifecycle-runtime.js",
    "prompts/mark-research-documents.md"
  ]) {
    assert.ok(contents.includes(required), `the package must contain ${required}`);
  }

  assert.ok(
    contents.some((entry) => entry.startsWith("runtimes/") && entry.includes("research-publisher-lifecycle")),
    "the package must contain at least one platform executable"
  );

  for (const forbidden of [
    ".env",
    "coverage/",
    "build-reports/",
    ".research-publisher/",
    "TestResults/",
    "tests/",
    "fixtures/",
    "src/ResearchPublisher"
  ]) {
    assert.ok(
      !contents.some((entry) => entry.startsWith(forbidden)),
      `the package must not contain ${forbidden}`
    );
  }

  // F# sources and .NET build output are not runtime assets.
  for (const pattern of [/\.fs$/, /\.fsproj$/, /(^|\/)obj\//, /(^|\/)bin\/(Debug|Release)\//]) {
    const leaked = contents.filter((entry) => pattern.test(entry));
    assert.equal(leaked.length, 0, `the package must not contain ${pattern}: ${leaked.join(", ")}`);
  }
  console.log(`  ok  package contents (${contents.length} entries)`);

  await fs.writeFile(
    path.join(consumer, "package.json"),
    `${JSON.stringify({ name: "packaged-consumer", private: true, type: "module" }, null, 2)}\n`
  );

  console.log("Installing the tarball into a clean repository...");
  expectExit(
    "npm install <tarball>",
    run(npm, ["install", "--prefer-offline", "--no-package-lock", tarball], { cwd: consumer, env: npmEnv }),
    0
  );

  // The npm shim must exist, but the commands run through the package's own entry
  // point so this test behaves identically on Windows, Linux and macOS.
  const shim = path.join(
    consumer,
    "node_modules",
    ".bin",
    process.platform === "win32" ? "research-publisher.cmd" : "research-publisher"
  );
  assert.ok(await fs.stat(shim).catch(() => null), `npm did not link the executable at ${shim}`);

  const cli = path.join(
    consumer,
    "node_modules",
    "@echelon-foundry",
    "research-publisher",
    "bin",
    "research-publisher.js"
  );
  const at = (...args) => run(process.execPath, [cli, ...args], { cwd: consumer, env: npmEnv });

  const packageVersion = JSON.parse(await fs.readFile(path.join(repositoryRoot, "package.json"), "utf8")).version;

  console.log("Exercising the documented commands...");
  const version = at("--version");
  expectExit("--version", version, 0);
  assert.equal(version.stdout.trim(), packageVersion, "--version must match the package version");

  const help = at("--help");
  expectExit("--help", help, 0);
  for (const command of ["init", "status", "verify", "upgrade", "doctor"]) {
    assert.ok(help.stdout.includes(`  ${command}`), `--help must document ${command}`);
  }

  for (const command of ["init", "status", "verify", "upgrade", "doctor"]) {
    expectExit(`${command} --help`, at(command, "--help"), 0);
  }

  // Nothing is installed yet.
  expectExit("verify (not installed)", at("verify"), 3);
  expectExit("upgrade (not installed)", at("upgrade"), 4);

  const dryRun = at("init", "--dry-run", "--json");
  expectExit("init --dry-run --json", dryRun, 0);
  const plan = parseJson("init --dry-run --json", dryRun);
  assert.equal(plan.schema, "research-publisher.plan/1");
  assert.ok(plan.changeCount > 0, "a fresh repository should have planned changes");
  assert.ok(
    !(await fs.stat(path.join(consumer, "research-publisher.config.mjs")).catch(() => null)),
    "--dry-run must not create files"
  );

  expectExit("init", at("init"), 0);

  const afterFirstInit = await snapshot(consumer);
  expectExit("init (second run)", at("init"), 0);
  assert.equal(await snapshot(consumer), afterFirstInit, "init must be idempotent");
  console.log("  ok  init is idempotent");

  expectExit("init --check", at("init", "--check"), 0);
  expectExit("upgrade --check", at("upgrade", "--check"), 0);
  expectExit("verify", at("verify"), 0);
  expectExit("doctor", at("doctor"), 0);

  const status = at("status", "--json");
  expectExit("status --json", status, 0);
  const statusReport = parseJson("status --json", status);
  assert.equal(statusReport.schema, "research-publisher.status/1");
  assert.equal(statusReport.state, "installed");
  assert.equal(statusReport.installedVersion, packageVersion);
  assert.equal(statusReport.configurationVersion, statusReport.currentConfigurationVersion);

  const verifyJson = at("verify", "--json");
  expectExit("verify --json", verifyJson, 0);
  assert.equal(parseJson("verify --json", verifyJson).passed, true);

  const doctorJson = at("doctor", "--json");
  expectExit("doctor --json", doctorJson, 0);
  assert.equal(parseJson("doctor --json", doctorJson).counts.error, 0);

  const upgradeDryRun = at("upgrade", "--dry-run", "--json");
  expectExit("upgrade --dry-run --json", upgradeDryRun, 0);
  assert.equal(parseJson("upgrade --dry-run --json", upgradeDryRun).changeCount, 0);

  // A damaged installation must be detected and repaired.
  await fs.rm(path.join(consumer, "prompts/research-publisher-mark-documents.md"));
  expectExit("verify (damaged)", at("verify"), 3);
  expectExit("doctor (damaged)", at("doctor"), 6);
  expectExit("init (repair)", at("init"), 0);
  expectExit("verify (repaired)", at("verify"), 0);

  // A user-owned file is never replaced.
  const customConfig = "export default { site: { title: 'Mine' } };\n";
  await fs.writeFile(path.join(consumer, "research-publisher.config.mjs"), customConfig);
  expectExit("init (user-owned config present)", at("init"), 0);
  assert.equal(
    await fs.readFile(path.join(consumer, "research-publisher.config.mjs"), "utf8"),
    customConfig,
    "init must not overwrite a user-owned file"
  );
  console.log("  ok  user-owned configuration survives init");

  // Usage errors are stable.
  expectExit("unknown command", at("nonsense-command", "--help"), 2);

  // A declared dependency satisfies strict verification; removing it does not.
  expectExit("verify --strict (dependency declared)", at("verify", "--strict"), 0);

  const consumerPackage = JSON.parse(await fs.readFile(path.join(consumer, "package.json"), "utf8"));
  const declared = { ...consumerPackage.dependencies };
  delete consumerPackage.dependencies["@echelon-foundry/research-publisher"];
  await fs.writeFile(path.join(consumer, "package.json"), `${JSON.stringify(consumerPackage, null, 2)}\n`);
  expectExit("verify (dependency undeclared)", at("verify"), 0);
  expectExit("verify --strict (dependency undeclared)", at("verify", "--strict"), 3);
  consumerPackage.dependencies = declared;
  await fs.writeFile(path.join(consumer, "package.json"), `${JSON.stringify(consumerPackage, null, 2)}\n`);

  // The publishing engine still works through the same executable.
  await fs.mkdir(path.join(consumer, "research"), { recursive: true });
  await fs.writeFile(
    path.join(consumer, "research/RP-2026-901-packaged.md"),
    "---\nid: RP-2026-901\ntitle: Packaged Artifact Check\nsummary: Confirms the packaged engine still builds.\n---\n\n## Body\n\nContent.\n"
  );
  await fs.writeFile(path.join(consumer, "research-publisher.config.mjs"), customConfig);
  expectExit("inventory (engine)", at("inventory", "--config", "./research-publisher.config.mjs"), 0);

  console.log(`\nPackaged artifact verified in ${consumer}`);
}

main().catch((error) => {
  process.stderr.write(`${error.stack ?? error.message}\n`);
  process.exit(1);
});
