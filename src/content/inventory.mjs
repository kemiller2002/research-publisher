import fs from "node:fs/promises";
import path from "node:path";
import fg from "fast-glob";
import { discoverFiles } from "./discover.mjs";

export async function inventoryProject({ projectRoot, config }) {
  const publishable = await discoverFiles({
    projectRoot,
    include: config.content.include,
    exclude: config.content.exclude
  });

  const allMarkdown = (await fg(["**/*.md"], {
    cwd: projectRoot,
    dot: false,
    onlyFiles: true
  })).sort((left, right) => left.localeCompare(right));

  const publishableSet = new Set(publishable);
  const records = allMarkdown.map((file) => {
    let classification = "ambiguous";

    if (publishableSet.has(file)) {
      classification = "publishable source";
    } else if (file.startsWith("input-documents/")) {
      classification = "intake/unprocessed";
    } else if (file.startsWith("prompts/")) {
      classification = "ignored";
    } else if (file.includes("/archive/")) {
      classification = "archived";
    } else if (file.startsWith("dist/") || file.startsWith("fixtures/alt-research/dist/")) {
      classification = "generated output";
    } else if (file.startsWith("build-reports/")) {
      classification = "generated output";
    }

    return {
      path: file,
      classification
    };
  });

  const summary = records.reduce((accumulator, record) => {
    accumulator[record.classification] = (accumulator[record.classification] ?? 0) + 1;
    return accumulator;
  }, {});

  await fs.mkdir(path.join(projectRoot, "build-reports"), {
    recursive: true
  });
  await fs.writeFile(
    path.join(projectRoot, "build-reports/content-inventory.json"),
    `${JSON.stringify({ generatedAt: "2026-07-22", summary, records }, null, 2)}\n`
  );
  const markdown = [
    "# Content Inventory",
    "",
    "## Summary",
    "",
    ...Object.entries(summary).map(([key, value]) => `- ${key}: ${value}`),
    "",
    "## Records",
    "",
    ...records.map((record) => `- ${record.classification}: \`${record.path}\``),
    ""
  ].join("\n");
  await fs.writeFile(path.join(projectRoot, "build-reports/content-inventory.md"), markdown);

  return {
    summary,
    records,
    publishable
  };
}

