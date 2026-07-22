import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";

function run(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      stdio: "inherit",
      ...options
    });

    child.on("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`${command} ${args.join(" ")} exited with code ${code}`));
      }
    });
    child.on("error", reject);
  });
}

async function main() {
  const repoRoot = process.cwd();
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "research-publisher-smoke-"));
  const packDir = path.join(tempRoot, "pack");
  const consumerDir = path.join(tempRoot, "consumer");
  const researchDir = path.join(consumerDir, "research");
  const npmCacheDir = path.join(tempRoot, "npm-cache");
  const npmEnv = {
    ...process.env,
    npm_config_cache: npmCacheDir
  };

  await fs.mkdir(packDir, { recursive: true });
  await fs.mkdir(researchDir, { recursive: true });
  await fs.mkdir(npmCacheDir, { recursive: true });

  await run("npm", ["pack", "--pack-destination", packDir], { cwd: repoRoot, env: npmEnv });

  const packedFiles = (await fs.readdir(packDir)).filter((name) => name.endsWith(".tgz"));
  if (packedFiles.length !== 1) {
    throw new Error(`Expected exactly one packed tarball, found ${packedFiles.length}.`);
  }

  await fs.writeFile(
    path.join(consumerDir, "package.json"),
    `${JSON.stringify({
      name: "research-publisher-smoke-consumer",
      private: true,
      type: "module",
      scripts: {
        build: "research-publisher build --config ./research-publisher.config.mjs"
      }
    }, null, 2)}\n`
  );

  await fs.writeFile(
    path.join(consumerDir, "research-publisher.config.mjs"),
    `export default {
  site: {
    title: "Smoke Test Research",
    description: "Temporary consumer build",
    baseUrl: "/",
    language: "en",
    siteUrl: "https://example.com/smoke"
  },
  repository: {
    name: "research-publisher-smoke-consumer",
    sourceUrl: "https://example.com/smoke"
  },
  content: {
    include: ["research/**/*.md"],
    exclude: ["dist/**", "node_modules/**"],
    drafts: false
  },
  output: {
    directory: "dist",
    catalog: "data/research-catalog.json",
    diagnostics: "data/build-diagnostics.json"
  }
};\n`
  );

  await fs.writeFile(
    path.join(researchDir, "RP-2026-900-smoke.md"),
    `---
id: RP-2026-900
title: Smoke Consumer Document
artifactType: research-package
researchArea: Smoke Testing
discipline:
  - Engineering Practice
summary: Verifies that the packaged CLI can build a consumer repository.
status: active
version: "1.0"
confidence: 1
completion: 1
priority: medium
authorAgent: codex
created: 2026-07-22
updated: 2026-07-22
tags:
  - smoke
---

## Verification

This file is built from a temporary consumer project that installs the package tarball.
`
  );

  const tarballPath = path.join(packDir, packedFiles[0]);
  await run("npm", ["install", "--prefer-offline", "--no-package-lock", tarballPath], {
    cwd: consumerDir,
    env: npmEnv
  });
  await run("npm", ["run", "build"], { cwd: consumerDir, env: npmEnv });

  await fs.access(path.join(consumerDir, "dist/data/research-catalog.json"));
  await fs.access(path.join(consumerDir, "dist/pagefind/pagefind.js"));

  process.stdout.write(`Smoke consumer build passed in ${consumerDir}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error.stack ?? error.message}\n`);
  process.exit(1);
});
