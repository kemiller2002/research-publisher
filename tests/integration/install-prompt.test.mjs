import fs from "node:fs/promises";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { runCommand } from "../../src/cli/run-command.mjs";

let tempRoot;

afterEach(async () => {
  if (tempRoot) {
    await fs.rm(tempRoot, { recursive: true, force: true });
  }
});

describe("install-prompt command", () => {
  it("installs the reusable marking prompt without overwriting local edits", async () => {
    tempRoot = await fs.mkdtemp(path.join(process.cwd(), ".research-publisher-prompt-test-"));
    const configPath = path.join(tempRoot, "research-publisher.config.mjs");
    await fs.writeFile(configPath, "export default {};\n");

    await runCommand(["node", "research-publisher", "install-prompt", "--config", configPath]);
    const promptPath = path.join(tempRoot, "prompts/research-publisher-mark-documents.md");
    const installed = await fs.readFile(promptPath, "utf8");
    expect(installed).toContain("Organize A Research Corpus By Reader Purpose");

    await fs.writeFile(promptPath, "local customization\n");
    await runCommand(["node", "research-publisher", "install-prompt", "--config", configPath]);
    await expect(fs.readFile(promptPath, "utf8")).resolves.toBe("local customization\n");
  });
});
