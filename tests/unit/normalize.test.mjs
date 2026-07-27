import { describe, expect, it } from "vitest";
import { normalizeDocument } from "../../src/metadata/normalize.mjs";

describe("normalizeDocument", () => {
  it("normalizes canonical metadata", () => {
    const document = normalizeDocument({
      relativePath: "research/evidence/EV-2026-001-static-site-sufficiency.md",
      frontmatter: {
        id: "EV-2026-001",
        title: "Static Site Sufficiency Evidence",
        artifactType: "evidence",
        researchArea: "Visual Engineering",
        discipline: ["Web Architecture"],
        project: "research-publisher",
        purposes: ["verify", "reproduce"],
        audiences: "researcher, contributor",
        entryPoint: "yes",
        entryPointOrder: "20",
        entryPointLabel: "Evidence guide",
        tags: "evidence, architecture",
        confidence: "0.82",
        completion: "1"
      },
      excerpt: "",
      body: "## Evidence",
      headings: [{ depth: 2, text: "Evidence" }],
      links: [],
      html: "<h2>Evidence</h2>"
    });

    expect(document.id).toBe("EV-2026-001");
    expect(document.tags).toEqual(["evidence", "architecture"]);
    expect(document.project).toBe("research-publisher");
    expect(document.purposes).toEqual(["verify", "reproduce"]);
    expect(document.audiences).toEqual(["researcher", "contributor"]);
    expect(document.entryPoint).toBe(true);
    expect(document.entryPointOrder).toBe(20);
    expect(document.confidence).toBe(0.82);
    expect(document.slug).toContain("ev-2026-001");
  });

  it("supports compatibility mode for legacy markdown", () => {
    const document = normalizeDocument({
      relativePath: "research/visual-engineering/legacy-observations.md",
      frontmatter: {},
      excerpt: "",
      body: "Legacy observations\n\n## Findings",
      headings: [{ depth: 2, text: "Findings" }],
      links: [],
      html: "<p>Legacy observations</p>"
    });

    expect(document.compatibilityMode).toBe(true);
    expect(document.id).toBeNull();
    expect(document.title).toBe("legacy observations");
    expect(document.purposes).toEqual([]);
    expect(document.audiences).toEqual([]);
    expect(document.entryPoint).toBe(false);
  });
});
