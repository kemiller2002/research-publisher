import fg from "fast-glob";
import path from "node:path";

export async function discoverFiles({ projectRoot, include, exclude }) {
  const entries = await fg(include, {
    cwd: projectRoot,
    ignore: exclude,
    dot: false,
    onlyFiles: true,
    unique: true
  });

  return entries
    .map((entry) => entry.split(path.sep).join("/"))
    .sort((left, right) => left.localeCompare(right));
}

