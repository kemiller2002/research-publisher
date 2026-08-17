import fs from "node:fs/promises";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { initializeProject } from "../../src/cli/init-project.mjs";

let tempRoot;

afterEach(async () => {
  if (tempRoot) {
    await fs.rm(tempRoot, { recursive: true, force: true });
  }
});

describe("initializeProject", () => {
  it("creates a safe reusable setup and preserves existing package scripts", async () => {
    tempRoot = await fs.mkdtemp(path.join(process.cwd(), ".research-publisher-init-test-"));
    await fs.writeFile(path.join(tempRoot, "package.json"), `${JSON.stringify({
      name: "example-research",
      private: true,
      scripts: {
        test: "vitest",
        "research:build": "custom-build-command"
      },
      repository: "https://github.com/example/example-research.git"
    }, null, 2)}\n`);

    const first = await initializeProject(tempRoot);
    expect(first.configCreated).toBe(true);
    expect(first.promptCreated).toBe(true);
    expect(first.scriptsAdded).toEqual([
      "research:inventory",
      "research:validate",
      "research:clean"
    ]);
    expect(first.scriptsPreserved).toEqual(["research:build"]);

    const packageJson = JSON.parse(await fs.readFile(path.join(tempRoot, "package.json"), "utf8"));
    expect(packageJson.scripts.test).toBe("vitest");
    expect(packageJson.scripts["research:build"]).toBe("custom-build-command");
    expect(packageJson.scripts["research:validate"]).toContain("research-publisher validate");

    const config = await fs.readFile(path.join(tempRoot, "research-publisher.config.mjs"), "utf8");
    expect(config).toContain('title: "Example Research"');
    expect(config).toContain('sourceUrl: "https://github.com/example/example-research"');
    expect(config).toContain('include: ["**/*.md"]');
    expect(config).toContain("branding: {");
    expect(config).toContain('"--color-accent": "#2457a6"');

    await fs.writeFile(path.join(tempRoot, "research-publisher.config.mjs"), "local config\n");
    await fs.writeFile(
      path.join(tempRoot, "prompts/research-publisher-mark-documents.md"),
      "local prompt\n"
    );
    const second = await initializeProject(tempRoot);
    expect(second.configCreated).toBe(false);
    expect(second.promptCreated).toBe(false);
    await expect(fs.readFile(path.join(tempRoot, "research-publisher.config.mjs"), "utf8"))
      .resolves.toBe("local config\n");
    await expect(fs.readFile(path.join(tempRoot, "prompts/research-publisher-mark-documents.md"), "utf8"))
      .resolves.toBe("local prompt\n");
  });

  it("requires an npm project", async () => {
    tempRoot = await fs.mkdtemp(path.join(process.cwd(), ".research-publisher-init-test-"));
    await expect(initializeProject(tempRoot)).rejects.toThrow("npm init -y");
  });
});
