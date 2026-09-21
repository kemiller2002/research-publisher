// Locates and launches the packaged F# lifecycle executable.
//
// This file is a bootstrap, not an application. It may detect the platform, find
// the executable, report environment facts, forward arguments and streams, and
// return the child's exit code. Every judgement about the repository belongs to
// the F# core: what to install, what the state means, whether it is valid, what
// has to change between versions and what configuration should exist.

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const packageRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

/** Exit code the CLI contract reserves for a platform with no packaged binary. */
export const UNSUPPORTED_PLATFORM = 7;

/** Runtime identifiers this package ships binaries for. */
export const SUPPORTED_RUNTIMES = [
  "win-x64",
  "linux-x64",
  "linux-arm64",
  "osx-x64",
  "osx-arm64"
];

export function runtimeIdentifier(platform = process.platform, arch = process.arch) {
  if (platform === "win32" && arch === "x64") return "win-x64";
  if (platform === "linux" && arch === "x64") return "linux-x64";
  if (platform === "linux" && arch === "arm64") return "linux-arm64";
  if (platform === "darwin" && arch === "x64") return "osx-x64";
  if (platform === "darwin" && arch === "arm64") return "osx-arm64";
  return null;
}

function executableName(platform = process.platform) {
  return platform === "win32" ? "research-publisher-lifecycle.exe" : "research-publisher-lifecycle";
}

/**
 * @returns {{ path: string } | { error: string, exitCode: number }}
 */
export function resolveExecutable() {
  const override = process.env.RESEARCH_PUBLISHER_LIFECYCLE_PATH;
  if (override) {
    if (fs.existsSync(override)) {
      return { path: override };
    }
    return {
      error: `RESEARCH_PUBLISHER_LIFECYCLE_PATH points at ${override}, which does not exist.`,
      exitCode: UNSUPPORTED_PLATFORM
    };
  }

  const rid = runtimeIdentifier();
  if (!rid) {
    return {
      error:
        `Unsupported platform ${process.platform}-${process.arch}. ` +
        `Packaged runtimes: ${SUPPORTED_RUNTIMES.join(", ")}.`,
      exitCode: UNSUPPORTED_PLATFORM
    };
  }

  const candidate = path.join(packageRoot, "runtimes", rid, executableName());
  if (!fs.existsSync(candidate)) {
    return {
      error:
        `The ${rid} executable is missing from this installation (${candidate}). ` +
        `Reinstall the package, or set RESEARCH_PUBLISHER_LIFECYCLE_PATH to a local build.`,
      exitCode: UNSUPPORTED_PLATFORM
    };
  }

  return { path: candidate };
}

/** Environment facts the F# CLI reports and judges. The bootstrap only measures. */
function childEnvironment() {
  return {
    ...process.env,
    RESEARCH_PUBLISHER_PACKAGE_ROOT: packageRoot,
    RESEARCH_PUBLISHER_NODE_VERSION: process.versions.node,
    RESEARCH_PUBLISHER_PLATFORM: process.platform,
    RESEARCH_PUBLISHER_ARCH: process.arch
  };
}

/** Run the lifecycle CLI with the caller's streams attached. */
export function runLifecycle(args, options = {}) {
  const resolved = resolveExecutable();
  if (resolved.error) {
    return { status: resolved.exitCode, error: resolved.error };
  }

  const hasInput = options.input !== undefined;
  const result = spawnSync(resolved.path, args, {
    stdio: options.capture
      ? [hasInput ? "pipe" : "inherit", "pipe", "inherit"]
      : "inherit",
    input: hasInput ? options.input : undefined,
    maxBuffer: options.maxBuffer ?? 64 * 1024 * 1024,
    env: childEnvironment(),
    encoding: "utf8",
    cwd: options.cwd ?? process.cwd()
  });

  if (result.error) {
    return { status: 1, error: result.error.message };
  }

  return { status: result.status ?? 1, stdout: result.stdout };
}
