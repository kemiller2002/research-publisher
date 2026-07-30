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
    const guides = JSON.parse(await fs.readFile(path.join(workspaceRoot, "dist/data/research-guides.json"), "utf8"));
    expect(guides.schemaVersion).toBe("1.0");
    expect(guides.projects["research-publisher"][0]).toMatchObject({
      id: "RP-2026-001",
      entryPointLabel: "Start here",
      purposes: ["orient", "integrate", "decide"]
    });
    const projectHtml = await fs.readFile(
      path.join(workspaceRoot, "dist/collections/project/research-publisher/index.html"),
      "utf8"
    );
    expect(projectHtml).toContain("guided starting point");
    expect(projectHtml).toContain("Start here");
    expect(result.catalog.records[0].url.startsWith(config.site.baseUrl)).toBe(true);
    const indexHtml = await fs.readFile(path.join(workspaceRoot, "dist/index.html"), "utf8");
    expect(indexHtml).toContain("Build:");
    expect(indexHtml).toMatch(/Build:\s*<time datetime="[^"]+">/);
    expect(indexHtml).toContain("<style");
    expect(indexHtml).toContain("--color-bg");
    expect(indexHtml).toContain('<span class="site-mark" aria-hidden="true">VE</span>');
    expect(indexHtml).not.toMatch(/<link[^>]+rel="stylesheet"/);
    await expect(
      fs.access(path.join(workspaceRoot, "dist/research/adding-a-research-repository/index.html"))
    ).resolves.toBeUndefined();
  }, 120000);

  it("builds the second fixture project with the same package", async () => {
    const { config, projectRoot, engineRoot } = await loadConfig(path.join(workspaceRoot, "fixtures/alt-research/research-publisher.config.mjs"));
    const result = await buildProject({ engineRoot, projectRoot, config });
    expect(result.catalog.records.length).toBe(3);
    await expect(fs.access(path.join(workspaceRoot, "fixtures/alt-research/dist/data/research-catalog.json"))).resolves.toBeUndefined();
    const guides = JSON.parse(
      await fs.readFile(path.join(workspaceRoot, "fixtures/alt-research/dist/data/research-guides.json"), "utf8")
    );
    expect(guides.projects["civic-dashboard"]).toHaveLength(1);
    const indexHtml = await fs.readFile(path.join(workspaceRoot, "fixtures/alt-research/dist/index.html"), "utf8");
    expect(indexHtml).toContain('<span class="site-mark" aria-hidden="true">CSN</span>');
  }, 120000);
});
