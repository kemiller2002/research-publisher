import fs from "node:fs/promises";
import path from "node:path";
import { beforeAll, describe, expect, it } from "vitest";
import { loadConfig } from "../../src/build/config.mjs";
import { buildProject } from "../../src/build/project.mjs";

const workspaceRoot = process.cwd();

describe("integration build", () => {
  beforeAll(async () => {
    await fs.rm(path.join(workspaceRoot, "dist"), { recursive: true, force: true });
    await fs.rm(path.join(workspaceRoot, "fixtures/alt-research/dist"), { recursive: true, force: true });
  });

  it("builds the main research site", async () => {
    const { config, projectRoot, engineRoot } = await loadConfig(path.join(workspaceRoot, "research-publisher.config.mjs"));
    const result = await buildProject({ engineRoot, projectRoot, config });
    expect(result.catalog.records.length).toBeGreaterThan(3);
    await expect(fs.access(path.join(workspaceRoot, "dist/data/research-catalog.json"))).resolves.toBeUndefined();
  }, 120000);

  it("builds the second fixture project with the same package", async () => {
    const { config, projectRoot, engineRoot } = await loadConfig(path.join(workspaceRoot, "fixtures/alt-research/research-publisher.config.mjs"));
    const result = await buildProject({ engineRoot, projectRoot, config });
    expect(result.catalog.records.length).toBe(3);
    await expect(fs.access(path.join(workspaceRoot, "fixtures/alt-research/dist/data/research-catalog.json"))).resolves.toBeUndefined();
  }, 120000);
});
