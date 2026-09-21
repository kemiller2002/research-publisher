import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { beforeAll, describe, expect, it } from "vitest";
import { loadConfig } from "../../src/build/config.mjs";
import { buildProject } from "../../src/build/project.mjs";

const workspaceRoot = process.cwd();

function outputPath(root, url) {
  return path.join(root, url.replace(/^\//, ""), "index.html");
}

async function snapshotTree(root) {
  const snapshot = {};

  async function visit(directory) {
    const entries = await fs.readdir(directory, { withFileTypes: true });
    entries.sort((left, right) => left.name.localeCompare(right.name));

    for (const entry of entries) {
      const absolute = path.join(directory, entry.name);
      if (entry.isDirectory()) {
        await visit(absolute);
        continue;
      }

      const relative = path.relative(root, absolute).split(path.sep).join("/");
      const bytes = await fs.readFile(absolute);
      snapshot[relative] = crypto.createHash("sha256").update(bytes).digest("hex");
    }
  }

  await visit(root);
  return snapshot;
}

describe("integration build", () => {
  beforeAll(async () => {
    await fs.rm(path.join(workspaceRoot, "dist"), { recursive: true, force: true });
    await fs.rm(path.join(workspaceRoot, "fixtures/alt-research/dist"), { recursive: true, force: true });
  });

  it("builds deterministic semantic projections through the F# compiler", async () => {
    const { config, projectRoot, engineRoot } = await loadConfig(
      path.join(workspaceRoot, "research-publisher.config.mjs")
    );
    const result = await buildProject({ engineRoot, projectRoot, config });

    expect(result.catalog.records.length).toBeGreaterThan(3);

    for (const machineOutput of [
      "dist/data/research-catalog.json",
      "dist/data/v1/manifest.json",
      "dist/data/v1/artifacts.json",
      "dist/data/v1/edges.json",
      "dist/data/v1/findings.json",
      "dist/data/v1/redirects.json",
      "dist/data/v1/provenance.json"
    ]) {
      await expect(fs.access(path.join(workspaceRoot, machineOutput))).resolves.toBeUndefined();
    }

    const manifest = JSON.parse(
      await fs.readFile(path.join(workspaceRoot, "dist/data/v1/manifest.json"), "utf8")
    );
    expect(manifest.identityPolicy).toBe("declared-id-or-source-path");
    expect(manifest.canonicalSource).toBe("ROS Markdown");
    expect(manifest).not.toHaveProperty("generatedOn");
    expect(manifest.indexes).toMatchObject({
      artifacts: "artifacts.json",
      edges: "edges.json",
      findings: "findings.json",
      redirects: "redirects.json",
      provenance: "provenance.json"
    });

    const artifactIndex = JSON.parse(
      await fs.readFile(path.join(workspaceRoot, "dist/data/v1/artifacts.json"), "utf8")
    );
    expect(artifactIndex.artifacts.length).toBe(result.catalog.records.length);
    expect(JSON.stringify(artifactIndex)).not.toContain('"html"');
    expect(JSON.stringify(artifactIndex)).not.toContain('"sourceMarkdown"');

    const abstractRecord = result.catalog.records.find((record) => record.id === "RP-VE-2026-0002");
    expect(abstractRecord.url).toBe("/a/RP-VE-2026-0002/");

    const abstractIndex = artifactIndex.artifacts.find((record) => record.id === "RP-VE-2026-0002");
    expect(abstractIndex.detail).toMatch(/\/data\/v1\/artifact\/RP-VE-2026-0002\.json$/);

    const abstractDetail = JSON.parse(
      await fs.readFile(path.join(workspaceRoot, "dist", abstractIndex.detail.replace(/^\//, "")), "utf8")
    );
    expect(abstractDetail.artifact.sourceMarkdown).toContain("Research Publisher Findings Abstract");
    expect(abstractDetail.artifact.provenance.path).toBe(abstractRecord.sourcePath);

    const guides = JSON.parse(
      await fs.readFile(path.join(workspaceRoot, "dist/data/research-guides.json"), "utf8")
    );
    expect(guides.schemaVersion).toBe("1.0");
    expect(guides.projects["research-publisher"][0]).toMatchObject({
      id: "RP-VE-2026-0002",
      entryPointLabel: "Findings abstract",
      purposes: ["orient", "decide", "integrate", "apply"]
    });

    const abstractHtml = await fs.readFile(
      outputPath(path.join(workspaceRoot, "dist"), abstractRecord.url),
      "utf8"
    );
    expect(abstractHtml).not.toMatch(/href="[^"]*\.md(?:[?#][^"]*)?"/);

    const legacyUrl = abstractRecord.legacyUrls.find((url) => url.startsWith("/research/"));
    expect(legacyUrl).toBeTruthy();
    const legacyHtml = await fs.readFile(
      outputPath(path.join(workspaceRoot, "dist"), legacyUrl),
      "utf8"
    );
    expect(legacyHtml).toContain("stable address");
    expect(legacyHtml).toContain("/a/RP-VE-2026-0002/");

    const legacyDocument = result.catalog.records.find((record) =>
      record.sourcePath.endsWith("legacy-observations.md")
    );
    expect(legacyDocument.id).toBeNull();
    expect(legacyDocument.url).toMatch(/^\/s\/[0-9a-f]{16}\/$/);
    await expect(
      fs.access(outputPath(path.join(workspaceRoot, "dist"), legacyDocument.url))
    ).resolves.toBeUndefined();

    for (const page of ["questions", "relationships", "integrity"]) {
      await expect(
        fs.access(path.join(workspaceRoot, `dist/${page}/index.html`))
      ).resolves.toBeUndefined();
    }

    const publicDiagnostics = JSON.parse(
      await fs.readFile(path.join(workspaceRoot, "dist/data/build-diagnostics.json"), "utf8")
    );
    expect(publicDiagnostics).not.toHaveProperty("generatedOn");
    expect(publicDiagnostics).not.toHaveProperty("performance");

    const indexHtml = await fs.readFile(path.join(workspaceRoot, "dist/index.html"), "utf8");
    expect(indexHtml).toContain("Unknown types preserved");
    expect(indexHtml).not.toContain("General Research");
    expect(indexHtml).not.toContain("site-build-stamp");
    expect(indexHtml).not.toMatch(/Build:\s*<time/);
    expect(indexHtml).toContain("<style");

    const firstSnapshot = await snapshotTree(path.join(workspaceRoot, "dist"));
    await buildProject({ engineRoot, projectRoot, config });
    const secondSnapshot = await snapshotTree(path.join(workspaceRoot, "dist"));
    expect(secondSnapshot).toEqual(firstSnapshot);
  }, 240000);

  it("builds the second fixture project with the same semantic pipeline", async () => {
    const { config, projectRoot, engineRoot } = await loadConfig(
      path.join(workspaceRoot, "fixtures/alt-research/research-publisher.config.mjs")
    );
    const result = await buildProject({ engineRoot, projectRoot, config });

    expect(result.catalog.records.length).toBe(3);
    await expect(
      fs.access(path.join(workspaceRoot, "fixtures/alt-research/dist/data/v1/manifest.json"))
    ).resolves.toBeUndefined();

    const indexHtml = await fs.readFile(
      path.join(workspaceRoot, "fixtures/alt-research/dist/index.html"),
      "utf8"
    );
    expect(indexHtml).toContain('<span class="site-mark" aria-hidden="true">CSN</span>');
    expect(indexHtml).toContain("--color-bg:#eef6ff");
    expect(indexHtml).toContain("--color-accent:#0f766e");
  }, 120000);
});
