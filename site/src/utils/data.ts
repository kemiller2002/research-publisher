import fs from "node:fs";
import path from "node:path";

const dataDir = process.env.RESEARCH_PUBLISHER_DATA_DIR;

function readJson(name: string) {
  if (!dataDir) {
    throw new Error("RESEARCH_PUBLISHER_DATA_DIR is required.");
  }

  const filePath = path.join(dataDir, name);
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

export const catalog = readJson("catalog.json");
export const graph = readJson("graph.json");
export const collections = readJson("collections.json");
export const siteData = readJson("site.json");

