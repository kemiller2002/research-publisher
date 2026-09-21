import fs from "node:fs/promises";
import path from "node:path";
import { beforeAll, describe, expect, it } from "vitest";
import { loadConfig } from "../../src/build/config.mjs";
import { buildProject } from "../../src/build/project.mjs";

const workspaceRoot = process.cwd();

function outputPath(root, url) {
  return path.join(root, url.replace(/^\//, ""), "index.html");
}

describe("integration build", () => {
  beforeAll(async () => {
    await fs.rm(path.join(workspaceRoot, "dist"), { recursive: true, force: true });
    await fs.rm(path.join(workspaceRoot, "fixtures/alt-research/dist"), { recursive: true, force: true });
  });

  it("builds the main research site through the F# semantic compiler", async () => {
    const { config, projectRoot, engineRoot } = await loadConfig(path.join(workspaceRoot, "research-publisher.config.mjs"));
    const result = await buildProject({ engineRoot, projectRoot, config });

    expect(result.catalog.records.length).toBeGreaterThan(3);

    for (const machineOutput of [
      "dist/data/research-catalog.json",
      "dist/data/v1/research-index.json",
      "dist/data/v1/relationship-index.json",
      "dist/data/v1/validation-report.json",
      "dist/data/v1/publication-manifest.json"
    ]) {
      await expect(fs.access(path.join(workspaceRoot, machineOutput))).resolves.toBeUndefined();
    }

    const manifest = JSON.parse(await fs.readFile(path.join(workspaceRoot, "dist/data/v1/publication-manifest.json"), "utf8"));
    expect(manifest.identityPolicy).toBe("declared-id-or-source-path");
    expect(manifest.canonicalSource).toBe("ROS Markdown");
    expect(manifest).not.toHaveProperty("generatedOn");

    const guides = JSON.parse(await fs.readFile(path.join(workspaceRoot, "dist/data/research-guides.json"), "utf8"));
    expect(guides.schemaVersion).toBe("2.0");
    expect(guides.projects["research-publisher"][0]).toMatchObject({
      id: "RP-VE-2026-0002",
      entryPointLabel: "Findings abstract",
      purposes: ["orient", "decide", "integrate", "apply"]
    });

    const abstractRecord = result.catalog.records.find((record) => record.id === "RP-VE-2026-0002");
    expect(abstractRecord.url).toBe("/a/RP-VE-2026-0002/");
    const abstractHtml = await fs.readFile(outputPath(path.join(workspaceRoot, "dist"), abstractRecord.url), "utf8");
    expect(abstractHtml).not.toMatch(/href="[^"]*\.md(?:[?#][^"]*)?"/);

    const legacyUrl = abstractRecord.legacyUrls.find((url) => url.startsWith("/research/"));
    expect(legacyUrl).toBeTruthy();
    const legacyHtml = await fs.readFile(outputPath(path.join(workspaceRoot, "dist"), legacyUrl), "utf8");
    expect(legacyHtml).toContain("stable address");
    expect(legacyHtml).toContain("/a/RP-VE-2026-0002/");

    const legacyDocument = result.catalog.records.find((record) => record.sourcePath.endsWith("legacy-observations.md"));
    expect(legacyDocument.id).toBeNull();
    expect(legacyDocument.url).toMatch(/^\/s\/[0-9a-f]{16}\/$/);
    await expect(fs.access(outputPath(path.join(workspaceRoot, "dist"), legacyDocument.url))).resolves.toBeUndefined();

    for (const page of ["questions", "relationships", "integrity"]) {
      await expect(fs.access(path.join(workspaceRoot, `dist/${page}/index.html`))).resolves.toBeUndefined();
    }

    const indexHtml = await fs.readFile(path.join(workspaceRoot, "dist/index.html"), "utf8");
    expect(indexHtml).toContain("Unknown types preserved");
    expect(indexHtml).not.toContain("General Research");
    expect(indexHtml).toContain("<style");
  }, 120000);

  it("builds the second fixture project with the same semantic pipeline", async () => {
    const { config, projectRoot, engineRoot } = await loadConfig(path.join(workspaceRoot, "fixtures/alt-research/research-publisher.config.mjs"));
    const result = await buildProject({ engineRoot, projectRoot, config });

    expect(result.catalog.records.length).toBe(3);
    await expect(fs.access(path.join(workspaceRoot, "fixtures/alt-research/dist/data/v1/publication-manifest.json"))).resolves.toBeUndefined();

    const indexHtml = await fs.readFile(path.join(workspaceRoot, "fixtures/alt-research/dist/index.html"), "utf8");
    expect(indexHtml).toContain('<span class="site-mark" aria-hidden="true">CSN</span>');
    expect(indexHtml).toContain("--color-bg:#eef6ff");
    expect(indexHtml).toContain("--color-accent:#0f766e");
  }, 120000);
});
