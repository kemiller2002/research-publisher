import { describe, expect, it } from "vitest";
import { buildRelationshipGraph } from "../../src/relationships/graph.mjs";

describe("buildRelationshipGraph", () => {
  it("builds backlinks only from resolved semantic relationships and retains unresolved declarations", () => {
    const documents = [
      {
        key: "RP-1",
        id: "RP-1",
        title: "RP",
        artifactType: "research-package",
        url: "/a/RP-1/",
        researchArea: "Visual Engineering",
        sourcePath: "research/rp.md"
      },
      {
        key: "EV-1",
        id: "EV-1",
        title: "EV",
        artifactType: "evidence",
        url: "/a/EV-1/",
        researchArea: "Visual Engineering",
        sourcePath: "research/ev.md"
      }
    ];

    const graph = buildRelationshipGraph(documents, [
      {
        sourceKey: "RP-1",
        targetKey: "EV-1",
        relation: "related-document",
        authority: "canonical",
        field: "related_documents",
        resolution: "resolved"
      },
      {
        sourceKey: "RP-1",
        targetKey: null,
        relation: "related-document",
        authority: "canonical",
        field: "related_documents",
        rawTarget: "DF-MISSING",
        resolution: "dangling"
      }
    ]);

    expect(graph.edges).toHaveLength(1);
    expect(graph.backlinks["EV-1"]).toEqual(["RP-1"]);
    expect(graph.unresolved).toHaveLength(1);
    expect(graph.unresolved[0].rawTarget).toBe("DF-MISSING");
  });
});
