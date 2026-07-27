import { describe, expect, it } from "vitest";
import { validateDocuments } from "../../src/validation/validate.mjs";

function document(overrides = {}) {
  return {
    id: "RP-2026-001",
    title: "Research package",
    url: "/research/research-package/",
    sourcePath: "research/RP-2026-001.md",
    relatedDocuments: [],
    purposes: [],
    audiences: [],
    entryPoint: false,
    project: null,
    ...overrides
  };
}

describe("validateDocuments purpose architecture", () => {
  it("accepts controlled purpose and audience values", () => {
    const diagnostics = validateDocuments([
      document({
        project: "research-publisher",
        purposes: ["orient", "integrate"],
        audiences: ["executive", "researcher"],
        entryPoint: true
      })
    ]);

    expect(diagnostics).toEqual([]);
  });

  it("warns about vocabulary drift and incomplete entry points", () => {
    const diagnostics = validateDocuments([
      document({
        purposes: ["promote"],
        audiences: ["machine"],
        entryPoint: true
      })
    ]);

    expect(diagnostics.map((diagnostic) => diagnostic.code)).toEqual([
      "unknown-purpose",
      "unknown-audience",
      "entry-point-without-project"
    ]);
  });
});
