import fs from "node:fs";
import path from "node:path";

const dataDir = process.env.RESEARCH_PUBLISHER_DATA_DIR;

function readJson(name: string) {
  if (!dataDir) throw new Error("RESEARCH_PUBLISHER_DATA_DIR is required.");
  return JSON.parse(fs.readFileSync(path.join(dataDir, name), "utf8"));
}

export const catalog = readJson("catalog.json");
export const graph = readJson("graph.json");
export const collections = readJson("collections.json");
export const guides = readJson("guides.json");
export const semantics = readJson("semantics.json");
export const siteData = readJson("site.json");
