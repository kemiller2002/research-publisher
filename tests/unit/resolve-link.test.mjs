import { describe, expect, it } from "vitest";
import { resolveDocumentLink } from "../../src/content/resolve-link.mjs";

const documentsBySourcePath = new Map([
  ["docs/target.md", { url: "/research/target/" }]
]);

describe("resolveDocumentLink", () => {
  it("maps relative Markdown links to base-aware published URLs", () => {
    const result = resolveDocumentLink({
      sourcePath: "docs/nested/source.md",
      href: "../target.md#finding",
      documentsBySourcePath,
      baseUrl: "/research-publisher/"
    });

    expect(result).toMatchObject({
      href: "/research-publisher/research/target/#finding",
      markdown: true,
      resolved: true,
      resolvedSourcePath: "docs/target.md"
    });
  });

  it("leaves external and same-page links unchanged", () => {
    expect(resolveDocumentLink({
      sourcePath: "docs/source.md",
      href: "https://example.com/source.md",
      documentsBySourcePath
    }).href).toBe("https://example.com/source.md");

    expect(resolveDocumentLink({
      sourcePath: "docs/source.md",
      href: "#finding",
      documentsBySourcePath
    }).href).toBe("#finding");
  });

  it("reports unmatched Markdown targets without inventing a route", () => {
    const result = resolveDocumentLink({
      sourcePath: "docs/source.md",
      href: "missing.md",
      documentsBySourcePath
    });

    expect(result).toMatchObject({
      href: "missing.md",
      markdown: true,
      resolved: false,
      resolvedSourcePath: "docs/missing.md"
    });
  });
});
