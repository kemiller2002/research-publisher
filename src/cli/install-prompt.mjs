import fs from "node:fs/promises";
import { constants } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export async function installMarkingPrompt(projectRoot) {
  const sourcePath = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../../prompts/mark-research-documents.md"
  );
  const promptDirectory = path.join(projectRoot, "prompts");
  const destinationPath = path.join(promptDirectory, "research-publisher-mark-documents.md");
  await fs.mkdir(promptDirectory, { recursive: true });

  try {
    await fs.copyFile(sourcePath, destinationPath, constants.COPYFILE_EXCL);
    return { created: true, path: destinationPath };
  } catch (error) {
    if (error.code === "EEXIST") {
      return { created: false, path: destinationPath };
    }
    throw error;
  }
}
