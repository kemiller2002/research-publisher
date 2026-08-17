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
      id: "RP-2026-002",
      entryPointLabel: "Findings abstract",
      purposes: ["orient", "decide", "integrate", "apply"]
    });
    expect(guides.projects["research-publisher"][1]).toMatchObject({
      id: "RP-2026-001",
      entryPointLabel: "Start here"
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
    expect(indexHtml).toMatch(/--color-bg:\s*#f2efe7/);
    expect(indexHtml).toMatch(/--color-accent:\s*#905831/);
    expect(indexHtml).toMatch(/--color-focus-ring-inverse:\s*#f2efe7/);
    expect(indexHtml).toContain('<span class="site-mark" aria-hidden="true">VE</span>');
    expect(indexHtml).not.toMatch(/<link[^>]+rel="stylesheet"/);
    expect(indexHtml).toContain("Research Publisher Findings Abstract");
    const schemaHtml = await fs.readFile(
      path.join(workspaceRoot, "dist/research/research-metadata-schema/index.html"),
      "utf8"
    );
    expect(schemaHtml).toContain('href="/research/document-purpose-and-project-guide-architecture/"');
    expect(schemaHtml).not.toMatch(/href="[^"]*\.md(?:[?#][^"]*)?"/);
    const abstractHtml = await fs.readFile(
      path.join(workspaceRoot, "dist/research/rp-2026-002-research-publisher-findings-abstract/index.html"),
      "utf8"
    );
    expect(abstractHtml).toContain('href="/research/ev-2026-001-static-site-sufficiency-evidence/"');
    expect(abstractHtml).not.toMatch(/href="[^"]*\.md(?:[?#][^"]*)?"/);
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
    expect(indexHtml).toContain("--color-bg:#eef6ff");
    expect(indexHtml).toContain("--color-accent:#0f766e");
    expect(indexHtml).toMatch(/@layer research-publisher-defaults\s*\{/);
    expect(indexHtml).toMatch(/--color-code-ink:\s*#3a403c/);
  }, 120000);
});
