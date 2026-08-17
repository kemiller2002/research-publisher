import fs from "node:fs/promises";
import path from "node:path";
import { installMarkingPrompt } from "./install-prompt.mjs";

const requiredScripts = {
  "research:inventory": "research-publisher inventory --config ./research-publisher.config.mjs",
  "research:validate": "research-publisher validate --config ./research-publisher.config.mjs",
  "research:build": "research-publisher build --config ./research-publisher.config.mjs",
  "research:clean": "research-publisher clean --config ./research-publisher.config.mjs"
};

function titleFromName(name) {
  return String(name || "Research Repository")
    .replace(/^@[^/]+\//, "")
    .replace(/[-_]+/g, " ")
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function repositoryUrl(packageJson) {
  if (typeof packageJson.repository === "string") {
    return packageJson.repository.replace(/^git\+/, "").replace(/\.git$/, "");
  }
  if (packageJson.repository?.url) {
    return packageJson.repository.url.replace(/^git\+/, "").replace(/\.git$/, "");
  }
  return "";
}

function configSource(packageJson, projectRoot) {
  const name = packageJson.name || path.basename(projectRoot);
  const title = titleFromName(name);
  const sourceUrl = repositoryUrl(packageJson);
  return `export default {
  site: {
    title: ${JSON.stringify(title)},
    description: "Searchable research repository",
    // Use "/repository-name/" for a GitHub Pages project site.
    baseUrl: "/",
    language: "en",
    siteUrl: "https://example.com/"
  },
  repository: {
    name: ${JSON.stringify(name)},
    sourceUrl: ${JSON.stringify(sourceUrl)}
  },
  content: {
    // Broad discovery keeps future research folders visible without config churn.
    include: ["**/*.md"],
    exclude: [
      "README.md",
      "CHANGELOG.md",
      "CONTRIBUTING.md",
      "node_modules/**",
      "dist/**",
      ".git/**",
      ".github/**",
      ".research-publisher/**",
      "build-reports/**",
      "prompts/**",
      "coverage/**",
      "tmp/**",
      "temp/**",
      "**/archive/**",
      "**/archives/**"
    ],
    drafts: false
  },
  metadata: {
    mode: "compatible",
    strictInCI: true,
    required: ["title"],
    stableIdPrefixes: ["RP", "JR", "EV", "HY", "TH", "EX", "DF", "CN", "GL"]
  },
  output: {
    directory: "dist",
    catalog: "data/research-catalog.json",
    diagnostics: "data/build-diagnostics.json"
  },
  branding: {
    // Package defaults provide a unified design. Override only the semantic
    // color roles this repository needs; see the Research Publisher README.
    cssVariables: {
      // "--color-accent": "#2457a6",
      // "--color-accent-strong": "#173b73",
      // "--color-accent-soft": "#dce8fa"
    }
  }
};
`;
}

async function writeExclusive(filePath, contents) {
  try {
    await fs.writeFile(filePath, contents, { flag: "wx" });
    return true;
  } catch (error) {
    if (error.code === "EEXIST") {
      return false;
    }
    throw error;
  }
}

export async function initializeProject(projectRoot = process.cwd()) {
  const packagePath = path.join(projectRoot, "package.json");
  let packageJson;
  try {
    packageJson = JSON.parse(await fs.readFile(packagePath, "utf8"));
  } catch (error) {
    if (error.code === "ENOENT") {
      throw new Error("package.json is required. Run npm init -y before research-publisher init.");
    }
    throw error;
  }

  const scriptsAdded = [];
  const scriptsPreserved = [];
  packageJson.scripts ??= {};
  for (const [name, command] of Object.entries(requiredScripts)) {
    if (Object.hasOwn(packageJson.scripts, name)) {
      scriptsPreserved.push(name);
    } else {
      packageJson.scripts[name] = command;
      scriptsAdded.push(name);
    }
  }
  if (scriptsAdded.length > 0) {
    await fs.writeFile(packagePath, `${JSON.stringify(packageJson, null, 2)}\n`);
  }

  const configPath = path.join(projectRoot, "research-publisher.config.mjs");
  const configCreated = await writeExclusive(configPath, configSource(packageJson, projectRoot));
  const prompt = await installMarkingPrompt(projectRoot);

  return {
    projectRoot,
    packagePath,
    configPath,
    configCreated,
    promptPath: prompt.path,
    promptCreated: prompt.created,
    scriptsAdded,
    scriptsPreserved
  };
}
