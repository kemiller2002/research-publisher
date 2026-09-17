import { describe, expect, it } from "vitest";
import { SUPPORTED_RUNTIMES, UNSUPPORTED_PLATFORM, runtimeIdentifier, resolveExecutable } from "../../bin/lifecycle-runtime.js";

describe("lifecycle bootstrap", () => {
  it("maps every supported platform and architecture to a packaged runtime", () => {
    expect(runtimeIdentifier("win32", "x64")).toBe("win-x64");
    expect(runtimeIdentifier("linux", "x64")).toBe("linux-x64");
    expect(runtimeIdentifier("linux", "arm64")).toBe("linux-arm64");
    expect(runtimeIdentifier("darwin", "x64")).toBe("osx-x64");
    expect(runtimeIdentifier("darwin", "arm64")).toBe("osx-arm64");
  });

  it("declares exactly the runtimes it can map to", () => {
    const mapped = [
      runtimeIdentifier("win32", "x64"),
      runtimeIdentifier("linux", "x64"),
      runtimeIdentifier("linux", "arm64"),
      runtimeIdentifier("darwin", "x64"),
      runtimeIdentifier("darwin", "arm64")
    ];
    expect(new Set(mapped)).toEqual(new Set(SUPPORTED_RUNTIMES));
  });

  it("refuses an unsupported platform instead of guessing", () => {
    expect(runtimeIdentifier("aix", "ppc64")).toBeNull();
    expect(runtimeIdentifier("linux", "ia32")).toBeNull();
  });

  it("reserves a distinct exit code for an unsupported platform", () => {
    expect(UNSUPPORTED_PLATFORM).toBe(7);
  });

  it("reports a missing override path rather than falling back silently", () => {
    const previous = process.env.RESEARCH_PUBLISHER_LIFECYCLE_PATH;
    process.env.RESEARCH_PUBLISHER_LIFECYCLE_PATH = "/nonexistent/research-publisher-lifecycle";
    try {
      const resolved = resolveExecutable();
      expect(resolved.exitCode).toBe(UNSUPPORTED_PLATFORM);
      expect(resolved.error).toContain("/nonexistent/research-publisher-lifecycle");
    } finally {
      if (previous === undefined) {
        delete process.env.RESEARCH_PUBLISHER_LIFECYCLE_PATH;
      } else {
        process.env.RESEARCH_PUBLISHER_LIFECYCLE_PATH = previous;
      }
    }
  });
});
