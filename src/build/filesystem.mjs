import fs from "node:fs/promises";
import path from "node:path";

export async function ensureDirectory(directoryPath) {
  await fs.mkdir(directoryPath, {
    recursive: true
  });
}

export async function writeJson(filePath, value) {
  await ensureDirectory(path.dirname(filePath));
  await fs.writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`);
}

