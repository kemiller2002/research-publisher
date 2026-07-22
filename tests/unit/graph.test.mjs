import { describe, expect, it } from "vitest";
import { buildRelationshipGraph } from "../../src/relationships/graph.mjs";

describe("buildRelationshipGraph", () => {
  it("builds backlinks from relationships", () => {
    const graph = buildRelationshipGraph([
      {
        id: "RP-1",
        slug: "rp-1",
        title: "RP",
        artifactType: "research-package",
        url: "/research/rp-1/",
        researchArea: "Visual Engineering",
        sourcePath: "research/rp.md",
        relatedDocuments: ["EV-1"],
        evidenceIds: [],
        hypothesisIds: [],
        theoryIds: []
      },
      {
        id: "EV-1",
        slug: "ev-1",
        title: "EV",
        artifactType: "evidence",
        url: "/research/ev-1/",
        researchArea: "Visual Engineering",
        sourcePath: "research/ev.md",
        relatedDocuments: [],
        evidenceIds: [],
        hypothesisIds: [],
        theoryIds: []
      }
    ]);

    expect(graph.edges).toHaveLength(1);
    expect(graph.backlinks["EV-1"]).toEqual(["RP-1"]);
  });
});

