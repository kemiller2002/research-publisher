// The README, the docs, the package metadata and the CLI's own help are one
// public contract. If they disagree, this fails.

import fs from "node:fs/promises";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { beforeAll, describe, expect, it } from "vitest";

const repositoryRoot = process.cwd();
const cli = path.join(repositoryRoot, "bin", "research-publisher.js");

function run(...args) {
  const result = spawnSync(process.execPath, [cli, ...args], { encoding: "utf8" });
  return { status: result.status, stdout: result.stdout ?? "", stderr: result.stderr ?? "" };
}

const LIFECYCLE_COMMANDS = ["init", "status", "verify", "upgrade", "doctor"];
const ENGINE_COMMANDS = [
  "build",
  "dev",
  "validate",
  "inventory",
  "check-links",
  "clean",
  "preview",
  "migrate"
];

let readme;
let cliDoc;
let packageJson;
let help;

beforeAll(async () => {
  readme = await fs.readFile(path.join(repositoryRoot, "README.md"), "utf8");
  cliDoc = await fs.readFile(path.join(repositoryRoot, "docs/cli.md"), "utf8");
  packageJson = JSON.parse(await fs.readFile(path.join(repositoryRoot, "package.json"), "utf8"));
  help = run("--help");
});

describe("documentation and CLI agree", () => {
  it("--version reports the published package version", () => {
    const version = run("--version");
    expect(version.status).toBe(0);
    expect(version.stdout.trim()).toBe(packageJson.version);
  });

  it("--help succeeds and names the package", () => {
    expect(help.status).toBe(0);
    expect(help.stdout).toContain(packageJson.name);
  });

  it("help lists every command the README documents", () => {
    for (const command of [...LIFECYCLE_COMMANDS, ...ENGINE_COMMANDS]) {
      expect(help.stdout, `help should document ${command}`).toContain(`  ${command}`);
      expect(readme, `README should document ${command}`).toContain(command);
    }
  });

  it("every command in help has command-specific help", () => {
    for (const command of [...LIFECYCLE_COMMANDS, ...ENGINE_COMMANDS]) {
      const result = run(command, "--help");
      expect(result.status, `${command} --help should succeed`).toBe(0);
      expect(result.stdout).toContain(command);
    }
    // Spawns the CLI once per command (~5 s in total), which exceeded the default
    // 5 s timeout under parallel suite load.
  }, 60000);

  it("the exit codes in help, the README and the command reference match", () => {
    const documented = [
      [0, "success"],
      [1, "internal failure"],
      [2, "invalid arguments"],
      [3, "verification failed"],
      [4, "incompatible installation"],
      [5, "migration blocked"],
      [6, "prerequisite"],
      [7, "unsupported platform"]
    ];

    for (const [code, phrase] of documented) {
      expect(help.stdout.toLowerCase()).toContain(`${code} ${phrase}`);
      expect(readme.toLowerCase()).toContain(`\`${code}\``);
      expect(cliDoc.toLowerCase()).toContain(`\`${code}\``);
    }
  });

  it("every npx invocation in the README names a real command or flag", () => {
    const invocations = [...readme.matchAll(/npx @echelon-foundry\/research-publisher ([^\n`]*)/g)]
      .map((match) => match[1].trim().split(/\s+/)[0])
      .filter(Boolean);

    expect(invocations.length).toBeGreaterThan(5);

    for (const token of invocations) {
      const known =
        LIFECYCLE_COMMANDS.includes(token) ||
        ENGINE_COMMANDS.includes(token) ||
        ["--help", "--version", "install-prompt"].includes(token);
      expect(known, `README references unknown command '${token}'`).toBe(true);
    }
  });

  it("the package declares the bin the documentation tells people to run", () => {
    expect(packageJson.bin).toEqual({ "research-publisher": "bin/research-publisher.js" });
  });

  it("the package publishes the launcher, the runtimes and the runtime template", () => {
    for (const entry of ["bin", "runtimes", "prompts/mark-research-documents.md", "README.md"]) {
      expect(packageJson.files).toContain(entry);
    }
  });

  it("no npm lifecycle script mutates a consuming repository", () => {
    for (const hook of ["preinstall", "install", "postinstall", "prepare"]) {
      expect(packageJson.scripts[hook]).toBeUndefined();
    }
  });

  it("the documented JSON schemas are the ones the CLI emits", () => {
    const status = run("status", "--json");
    expect(status.status).toBe(0);
    const report = JSON.parse(status.stdout);
    expect(report.schema).toBe("research-publisher.status/1");
    expect(cliDoc).toContain(report.schema);

    const verify = run("verify", "--json");
    const verifyReport = JSON.parse(verify.stdout);
    expect(verifyReport.schema).toBe("research-publisher.verify/1");
    expect(cliDoc).toContain(verifyReport.schema);
  });

  it("json output carries no decorative text on stdout", () => {
    const doctor = run("doctor", "--json");
    expect(doctor.stdout.trimStart().startsWith("{")).toBe(true);
    expect(() => JSON.parse(doctor.stdout)).not.toThrow();
  });
});
