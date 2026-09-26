// Provenance transport (REQ-RP-PROV, R14; Praxis RQ-ROS-2026-A008/A009/A015/A019,
// DF-ROS-2026-A037). The publisher must carry Praxis provenance without stripping,
// rewriting, or inventing it.
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import YAML from "yaml";
import { describe, expect, it } from "vitest";
import {
  addLineage,
  appendContribution,
  IDENTITY_ENVIRONMENT_VARIABLES,
  classify,
  emptyBlock,
  originator,
  preservationViolations,
  withRole
} from "../../src/vendor/praxis/provenance-interchange.mjs";
import {
  SELF_DECLARED_UNVERIFIED,
  describeProvenance,
  provenanceDiagnostics,
  toInterchangeBlock
} from "../../src/metadata/provenance.mjs";
import { normalizeDocument } from "../../src/metadata/normalize.mjs";
import { parseDocument } from "../../src/content/parse-document.mjs";
import { buildRelationshipGraph } from "../../src/relationships/graph.mjs";
import { validateDocuments } from "../../src/validation/validate.mjs";
import { createPublicCatalog, buildProject } from "../../src/build/project.mjs";
import { loadConfig } from "../../src/build/config.mjs";

const workspaceRoot = process.cwd();
const fixtureDirectory = path.join(workspaceRoot, "tests/fixtures/praxis-provenance");
const vendorDirectory = path.join(workspaceRoot, "src/vendor/praxis");
const readJson = (file) => JSON.parse(fs.readFileSync(file, "utf8"));
const cases = readJson(path.join(fixtureDirectory, "cases.json")).cases;
const chain = readJson(path.join(fixtureDirectory, "echelon-chain.json"));

const A = { kind: "agent", id: "openai/codex", provider: "openai", model: "gpt-5-codex", runtime: "codex" };
const B = { kind: "agent", id: "anthropic/claude-code", provider: "anthropic", model: "unknown", runtime: "claude-code" };
const H = { kind: "human", id: "kevin" };
const CI = { kind: "automation", id: "github-actions/ci", provider: "github", model: "unknown", runtime: "github-actions" };
const U = { kind: "unknown", id: "unknown", provider: "unknown", model: "unknown", runtime: "unknown" };

/** A parsed-document stand-in; `frontmatterText` is what gray-matter hands the parser. */
function parsedFrom(frontmatter, relativePath = "research/evidence/EV-2026-001-x.md") {
  return {
    relativePath,
    frontmatter,
    frontmatterText: YAML.stringify(frontmatter),
    excerpt: "",
    body: "Body",
    headings: [],
    links: [],
    html: "<p>Body</p>"
  };
}

const normalized = (frontmatter, relativePath) => normalizeDocument(parsedFrom(frontmatter, relativePath));

describe("vendored Praxis contract", () => {
  const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

  it("fixtures are byte-identical to the pinned Praxis commit", () => {
    const source = readJson(path.join(fixtureDirectory, "SOURCE.json"));
    expect(source.commit).toBe("c2657efb4d54f11d0fd0617cc1bcd5b8418601d5");
    for (const [file, hash] of Object.entries(source.files)) {
      expect(sha256(path.join(fixtureDirectory, file)), file).toBe(hash);
    }
  });

  it("the reference library is byte-identical to the pinned Praxis commit", () => {
    const source = readJson(path.join(vendorDirectory, "SOURCE.json"));
    expect(source.commit).toBe("c2657efb4d54f11d0fd0617cc1bcd5b8418601d5");
    for (const [file, hash] of Object.entries(source.files)) {
      expect(sha256(path.join(vendorDirectory, file)), file).toBe(hash);
    }
  });

  it("has all 56 conformance cases (contract revision 1.1)", () => {
    expect(cases).toHaveLength(56);
  });

  it("the vendored identity-environment list matches the vendored library", () => {
    // The publisher never launches a process for another actor (it only spawns its
    // own Astro and Pagefind steps), so revision 1.1 rule 7 does not apply here;
    // this keeps the two vendored files consistent for any future launcher.
    const environment = readJson(path.join(fixtureDirectory, "identity-environment.json"));
    expect([...IDENTITY_ENVIRONMENT_VARIABLES]).toEqual(environment.variables);
  });

  for (const item of cases) {
    it(`conformance: ${item.name} is ${item.expect}`, () => {
      const result = classify(item.block);
      expect(result.verdict, JSON.stringify(result.problems)).toBe(item.expect);
      expect(result.warnings).toHaveLength(item.warnings);
    });

    it(`publisher boundary: ${item.name} is ${item.expect === "malformed" ? "rejected visibly" : "carried verbatim"}`, () => {
      const before = JSON.stringify(item.block);
      const described = describeProvenance(item.block);
      expect(JSON.stringify(item.block)).toBe(before);
      expect(described.provenanceStatus.verdict).toBe(item.expect);
      if (item.expect === "malformed") {
        expect("provenance" in described).toBe(false);
        expect(described.provenanceWithheld).toBe(true);
        expect(described.provenanceStatus.problems.length).toBeGreaterThan(0);
      } else {
        expect(described.provenance).toEqual(item.block);
        expect(described.provenanceStatus.warnings).toHaveLength(item.warnings);
      }
    });
  }
});

describe("normalization preserves provenance", () => {
  it("keeps a Praxis front-matter block exactly, including unknown fields and exact timestamps", async () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), "rp-prov-"));
    const file = "research/evidence/EV-2026-101-carried.md";
    fs.mkdirSync(path.join(directory, "research/evidence"), { recursive: true });
    const frontmatterText = [
      "id: EV-2026-101",
      "title: Carried provenance",
      "author_agent: codex",
      "derived_from: [EV-2026-100, \"praxis:RQ-ROS-2026-A009\"]",
      "provenance:",
      "  x-origin-note: kept by every reader",
      "  contributions:",
      "    EXE-20260925T193942361Z-84dc9b22:",
      "      operations: [created, x-drafted]",
      "      at: 2026-09-25T20:16:21Z",
      "      actor:",
      "        kind: agent",
      "        id: anthropic/claude-code",
      "        provider: anthropic",
      "        model: unknown",
      "        runtime: claude-code",
      "        x-session: s-1",
      "      reason: \"Captured (FEAT-1)\"",
      "      evidence: [EV-2026-100]",
      "    CTB-20260926T090000000Z-5f2e19aa:",
      "      operations: [approved]",
      "      at: 2026-09-26T09:00:00.000Z",
      "      actor:",
      "        kind: human",
      "        id: kevin"
    ].join("\n");
    fs.writeFileSync(path.join(directory, file), `---\n${frontmatterText}\n---\n\n# Carried\n`);

    const record = normalizeDocument(await parseDocument(directory, file));
    const expected = YAML.parse(frontmatterText).provenance;

    expect(record.provenance).toEqual(expected);
    expect(JSON.stringify(record.provenance)).toBe(JSON.stringify(expected));
    expect(record.provenance.contributions["EXE-20260925T193942361Z-84dc9b22"].at).toBe("2026-09-25T20:16:21Z");
    expect(record.provenanceStatus).toEqual({ verdict: "supported", schema: "praxis.provenance/1", warnings: [], problems: [] });
    expect(record.derivedFrom).toEqual(["EV-2026-100", "praxis:RQ-ROS-2026-A009"]);
    expect(preservationViolations(expected, JSON.parse(JSON.stringify(record.provenance)))).toEqual([]);
    // Legacy authorship remains a separate, self-declared claim.
    expect(record.authorAgent).toBe("codex");
    expect(record.selfDeclaredAuthors).toEqual([{ field: "author_agent", value: "codex", status: SELF_DECLARED_UNVERIFIED }]);
  });

  it("round-trips to a Praxis-compatible interchange block", () => {
    const block = appendContribution(emptyBlock(), "EXE-1", { operations: ["created"], at: "2026-09-26T08:00:00.000Z", actor: A }).block;
    const { schema: _schema, ...frontMatterBlock } = block;
    const record = normalized({ id: "EV-2026-001", provenance: frontMatterBlock, derived_from: ["EV-2026-000"] });
    const published = JSON.parse(JSON.stringify(createPublicCatalog([record], { site: { title: "t", baseUrl: "/" } }).records[0]));
    const interchange = toInterchangeBlock(published);

    expect(interchange).toEqual({ ...block, derivedFrom: ["EV-2026-000"] });
    expect(classify(interchange).verdict).toBe("supported");
    expect(preservationViolations(block, interchange)).toEqual([]);
  });

  it("keeps multiple contributors: agents, two executions of one agent, a human, automation, and an unknown actor", () => {
    const steps = [
      ["EXE-1", { operations: ["created"], at: "2026-09-26T08:00:00.000Z", actor: A }],
      ["EXE-2", { operations: ["modified"], at: "2026-09-26T08:05:00.000Z", actor: B }],
      ["EXE-3", { operations: ["remediated"], at: "2026-09-26T08:10:00.000Z", actor: A }],
      ["CTB-1", { operations: ["approved"], at: "2026-09-26T08:15:00.000Z", actor: H }],
      ["EXT-github-actions.run-1", { operations: ["validated"], at: "2026-09-26T08:20:00.000Z", actor: CI }],
      ["CTB-2", { operations: ["reviewed"], at: "2026-09-26T08:25:00.000Z", actor: U }],
      ["EXE-4", { operations: ["transformed"], at: "2026-09-26T08:30:00.000Z", actor: B }]
    ];
    const block = steps.reduce((current, [key, contribution]) => {
      const next = appendContribution(current, key, contribution);
      expect(next.ok, next.error).toBe(true);
      return next.block;
    }, emptyBlock());

    const record = normalized({ id: "EV-2026-001", provenance: block });

    expect(record.provenance).toEqual(block);
    expect(originator(record.provenance).actor).toEqual(A);
    expect(Object.values(record.provenance.contributions).filter((entry) => entry.actor.id === A.id)).toHaveLength(2);
    expect(withRole(record.provenance, "transformed").map((entry) => entry.actor.id)).toEqual([B.id]);
    // Nothing structured is projected onto the legacy author field.
    expect(record.authorAgent).toBe("unknown");
    expect(record.selfDeclaredAuthors).toEqual([]);
  });

  it("carries another major version verbatim, flags it, and does not read lineage from it", () => {
    const block = { schema: "praxis.provenance/2", contributors: [{ who: "x" }], derivedFrom: ["EV-2026-900"] };
    const record = normalized({ id: "EV-2026-001", provenance: block });

    expect(record.provenance).toEqual(block);
    expect(record.provenanceStatus.verdict).toBe("unsupported");
    expect(record.provenanceStatus.schema).toBe("praxis.provenance/2");
    expect(record.derivedFrom).toEqual([]);
    expect(toInterchangeBlock(record)).toEqual(block);
    expect(provenanceDiagnostics(record)).toMatchObject([{ severity: "warning", code: "unsupported-provenance-version" }]);
  });

  it("rejects malformed provenance with a diagnostic, never silently, and never publishes it", () => {
    const record = normalized({
      id: "EV-2026-001",
      provenance: { contributions: { "EXE-1": { operations: ["created"], at: "yesterday", actor: A } } }
    });

    // Withheld, never presented as "unattributed" (provenance: null).
    expect("provenance" in record).toBe(false);
    expect(record.provenanceWithheld).toBe(true);
    expect(record.provenanceStatus.verdict).toBe("malformed");
    const diagnostics = validateDocuments([record]).filter((item) => item.code === "malformed-provenance");
    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0].severity).toBe("warning");
    expect(diagnostics[0].message).toContain("contributions.EXE-1.at");
  });

  it("never publishes a credential-like value", () => {
    const secret = "ghp_abcdefghijklmnopqrstuvwxyz0123456789";
    const record = normalized({
      id: "EV-2026-001",
      provenance: { contributions: { "EXE-1": { operations: ["created"], at: "2026-09-26T08:00:00.000Z", actor: A, reason: `token ${secret}` } } }
    });
    const published = JSON.stringify(createPublicCatalog([record], { site: { title: "t", baseUrl: "/" } }));

    expect(record.provenanceStatus.verdict).toBe("malformed");
    expect(published).not.toContain(secret);
    expect(JSON.stringify(provenanceDiagnostics(record))).not.toContain(secret);
  });

  it("reports unknown operation codes but keeps them", () => {
    const block = { contributions: { "EXE-1": { operations: ["created", "sketched"], at: "2026-09-26T08:00:00.000Z", actor: A } } };
    const record = normalized({ id: "EV-2026-001", provenance: block });

    expect(record.provenance).toEqual(block);
    expect(provenanceDiagnostics(record)).toMatchObject([{ severity: "warning", code: "provenance-unknown-operation" }]);
  });
});

describe("legacy records", () => {
  it("publishes free-text author fields as self-declared and unverified, without inventing provenance", () => {
    const record = normalized({
      id: "EV-2026-001",
      author_agent: "codex",
      created_by_agent: "claude",
      source_author: "Jane Doe"
    });

    expect(record.authorAgent).toBe("codex");
    expect(record.selfDeclaredAuthors).toEqual([
      { field: "author_agent", value: "codex", status: SELF_DECLARED_UNVERIFIED },
      { field: "created_by_agent", value: "claude", status: SELF_DECLARED_UNVERIFIED },
      { field: "source_author", value: "Jane Doe", status: SELF_DECLARED_UNVERIFIED }
    ]);
    expect(record.provenance).toBeNull();
    expect(record.provenanceStatus).toBeNull();
    expect(toInterchangeBlock(record)).toBeNull();
  });

  it("leaves an artifact without provenance unattributed and does not fabricate dates", () => {
    const record = normalized({ id: "EV-2026-001", title: "Plain" });

    expect(record.provenance).toBeNull();
    expect(record.provenanceStatus).toBeNull();
    expect(record.derivedFrom).toEqual([]);
    expect(record.created).toBeNull();
    expect(record.updated).toBeNull();
    expect(provenanceDiagnostics(record)).toEqual([]);
    expect(validateDocuments([record]).filter((item) => item.code.includes("provenance"))).toEqual([]);
  });
});

describe("lineage", () => {
  const document = (id, frontmatter = {}) => normalized({ id, title: id, ...frontmatter }, `research/${id}.md`);

  it("turns derived_from into derivedFrom and typed derived-from graph edges, separate from authorship", () => {
    const source = document("EV-2026-001", {
      provenance: { contributions: { "EXE-1": { operations: ["created"], at: "2026-09-26T08:00:00.000Z", actor: A } } }
    });
    const derived = document("EV-2026-002", {
      derived_from: ["EV-2026-001", "aegis:fault/F-1"],
      provenance: { contributions: { "EXE-2": { operations: ["created"], at: "2026-09-26T09:00:00.000Z", actor: B } } }
    });
    const graph = buildRelationshipGraph([source, derived]);

    expect(derived.derivedFrom).toEqual(["EV-2026-001", "aegis:fault/F-1"]);
    expect(graph.edges).toEqual([{ source: "EV-2026-002", target: "EV-2026-001", type: "derived-from" }]);
    expect(graph.backlinks["EV-2026-001"]).toEqual(["EV-2026-002"]);
    // The derived artifact's author is its own; the source's author is not inherited.
    expect(originator(derived.provenance).actor).toEqual(B);
    expect(originator(source.provenance).actor).toEqual(A);
  });

  it("replays the Echelon chain: every record keeps its own originator and lineage links them", () => {
    const blocks = chain.steps.reduce((records, step) => {
      const current = records[step.record] ?? emptyBlock();
      if (step.append) {
        const next = appendContribution(current, step.append.key, step.append.contribution);
        expect(next.ok, next.error).toBe(true);
        return { ...records, [step.record]: next.block };
      }
      return { ...records, [step.record]: addLineage(current, step.lineage) };
    }, {});
    const documents = Object.entries(blocks).map(([id, block], index) =>
      normalized({ id, title: id, provenance: block }, `research/chain-${index}.md`)
    );
    const graph = buildRelationshipGraph(documents);
    const byId = new Map(documents.map((item) => [item.id, item]));

    for (const [id, expected] of Object.entries(chain.expect.originators)) {
      const found = originator(byId.get(id).provenance);
      expect(found.key).toBe(expected.key);
      expect(found.actor.id).toBe(expected.actorId);
      expect(preservationViolations(blocks[id], byId.get(id).provenance)).toEqual([]);
    }
    for (const [id, roles] of Object.entries(chain.expect.roles)) {
      for (const [role, keys] of Object.entries(roles)) {
        expect(withRole(byId.get(id).provenance, role).map((entry) => entry.key)).toEqual(keys);
      }
    }
    const walk = (start, seen = new Set()) =>
      graph.edges
        .filter((edge) => edge.type === "derived-from" && edge.source === start && !seen.has(edge.target))
        .reduce((acc, edge) => walk(edge.target, new Set([...acc, edge.target])), seen);
    const reached = walk(chain.expect.lineageFrom);
    for (const reference of chain.expect.lineageReaches) {
      expect(reached.has(reference), reference).toBe(true);
    }
    const originators = documents.map((item) => originator(item.provenance).actor.id);
    expect(originators).toHaveLength(chain.expect.chainOriginatorCount);
    expect(new Set(originators).size).toBeGreaterThan(1);
    expect(documents.every((item) => item.authorAgent === "unknown")).toBe(true);
  });
});

describe("contract revision 1.1 and review findings", () => {
  const block = { contributions: { "EXE-1": { operations: ["created"], at: "2026-09-26T08:00:00.000Z", actor: A } } };

  it("a declared `provenance: null` is malformed and withheld, not unattributed", () => {
    const parsed = { ...parsedFrom({ id: "EV-2026-001" }), frontmatterText: "id: EV-2026-001\nprovenance:\n" };
    const record = normalizeDocument(parsed);

    expect("provenance" in record).toBe(false);
    expect(record.provenanceWithheld).toBe(true);
    expect(record.provenanceStatus.verdict).toBe("malformed");
    expect(provenanceDiagnostics(record)).toMatchObject([{ code: "malformed-provenance", severity: "warning" }]);
  });

  it("null inside a block is malformed (revision 1.1: null is not absence)", () => {
    for (const field of ["last", "reason", "evidence"]) {
      const entry = { ...block.contributions["EXE-1"], [field]: null };
      const record = normalized({ id: "EV-2026-001", provenance: { contributions: { "EXE-1": entry } } });
      expect(record.provenanceWithheld, field).toBe(true);
    }
    expect(normalized({ id: "EV-2026-001", provenance: { ...block, schema: null } }).provenanceWithheld).toBe(true);
    expect(normalized({ id: "EV-2026-001", provenance: { ...block, derivedFrom: null } }).provenanceWithheld).toBe(true);
  });

  it("exact matching and calendar-valid timestamps are enforced through the vendored classifier", () => {
    const withAt = (at) => ({ contributions: { "EXE-1": { operations: ["created"], at, actor: A } } });
    expect(normalized({ id: "E", provenance: withAt("2026-02-30T08:00:00.000Z") }).provenanceWithheld).toBe(true);
    expect(normalized({ id: "E", provenance: withAt("2026-09-26T24:00:00.000Z") }).provenanceWithheld).toBe(true);
    expect(normalized({ id: "E", provenance: withAt("9999-12-31T23:59:59.999999999Z") }).provenanceStatus.verdict).toBe("supported");
    expect(normalized({ id: "E", provenance: { contributions: { "EXE-1\n": block.contributions["EXE-1"] } } }).provenanceWithheld).toBe(true);
    expect(normalized({ id: "E", provenance: { ...block, schema: "praxis.provenance/1\n" } }).provenanceWithheld).toBe(true);
  });

  it("every record states provenanceWithheld explicitly", () => {
    expect(normalized({ id: "E" }).provenanceWithheld).toBe(false);
    expect(normalized({ id: "E", provenance: block }).provenanceWithheld).toBe(false);
    expect(normalized({ id: "E", provenance: { schema: "praxis.provenance/2" } }).provenanceWithheld).toBe(false);
  });

  it("a withheld block is distinguishable from an unattributed record in the public catalog", () => {
    const withheld = normalized({ id: "EV-2026-001", provenance: { contributions: "bad" } }, "research/a.md");
    const unattributed = normalized({ id: "EV-2026-002" }, "research/b.md");
    const [first, second] = JSON.parse(JSON.stringify(createPublicCatalog([withheld, unattributed], { site: { title: "t", baseUrl: "/" } }))).records;

    expect(first).not.toHaveProperty("provenance");
    expect(first.provenanceWithheld).toBe(true);
    expect(first.provenanceStatus.verdict).toBe("malformed");
    expect(second.provenance).toBeNull();
    expect(second.provenanceWithheld).toBe(false);
    expect(second.provenanceStatus).toBeNull();
  });

  it("a scalar author field is one self-declared author, never split on commas", () => {
    const record = normalized({ id: "E", author: "Doe, Jane", source_author: "Smith, J., and Lee, K." });

    expect(record.selfDeclaredAuthors).toEqual([
      { field: "author", value: "Doe, Jane", status: SELF_DECLARED_UNVERIFIED },
      { field: "source_author", value: "Smith, J., and Lee, K.", status: SELF_DECLARED_UNVERIFIED }
    ]);
    expect(record.authorAgent).toBe("Doe, Jane");
  });

  it("a real YAML list of authors is several self-declared authors", async () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), "rp-prov-authors-"));
    fs.writeFileSync(path.join(directory, "a.md"), "---\nid: E\nauthor_agent: [codex, \"Doe, Jane\"]\n---\n\n# A\n");
    const record = normalizeDocument(await parseDocument(directory, "a.md"));

    expect(record.selfDeclaredAuthors.map((item) => item.value)).toEqual(["codex", "Doe, Jane"]);
    expect(record.provenance).toBeNull();
  });

  it("a scalar derived_from is one lineage reference; only a list holds several", () => {
    expect(normalized({ id: "E", derived_from: "EV-1, EV-2" }).derivedFrom).toEqual(["EV-1, EV-2"]);
    expect(normalized({ id: "E", derived_from: "EV-1" }).derivedFrom).toEqual(["EV-1"]);
    expect(normalized({ id: "E", derived_from: ["EV-1", "EV-2"] }).derivedFrom).toEqual(["EV-1", "EV-2"]);
  });

  it("the catalog and record schema versions are 1.2 and the graph is 1.1", () => {
    const record = normalized({ id: "E" });
    expect(record.schemaVersion).toBe("1.2");
    expect(createPublicCatalog([record], { site: { title: "t", baseUrl: "/" } }).schemaVersion).toBe("1.2");
    expect(buildRelationshipGraph([record]).schemaVersion).toBe("1.1");
  });
});

describe("build pipeline", () => {
  function temporaryProject(files) {
    // Under the workspace (git-ignored .tmp/) so the test runner may import its config.
    fs.mkdirSync(path.join(workspaceRoot, ".tmp"), { recursive: true });
    const directory = fs.mkdtempSync(path.join(workspaceRoot, ".tmp", "rp-prov-build-"));
    fs.writeFileSync(
      path.join(directory, "research-publisher.config.mjs"),
      'export default { site: { title: "Provenance fixture", baseUrl: "/" }, content: { include: ["research/**/*.md"], exclude: [] } };\n'
    );
    for (const [file, contents] of Object.entries(files)) {
      fs.mkdirSync(path.dirname(path.join(directory, file)), { recursive: true });
      fs.writeFileSync(path.join(directory, file), contents);
    }
    return directory;
  }

  async function validateProject(directory) {
    const { config, projectRoot, engineRoot } = await loadConfig(path.join(directory, "research-publisher.config.mjs"));
    const result = await buildProject({ engineRoot, projectRoot, config, mode: "validate" });
    const dataDirectory = path.join(directory, ".research-publisher/dist/data");
    const output = {
      diagnostics: result.diagnostics,
      catalog: readJson(path.join(dataDirectory, "catalog.json")),
      graph: readJson(path.join(dataDirectory, "graph.json"))
    };
    fs.rmSync(directory, { recursive: true, force: true });
    return output;
  }

  it("carries provenance and lineage into the catalog and graph, and reports malformed blocks as warnings", async () => {
    const directory = temporaryProject({
      "research/EV-2026-001-source.md": [
        "---",
        "id: EV-2026-001",
        "title: Source",
        "provenance:",
        "  contributions:",
        "    EXE-20260926T080000000Z-aaaa0001:",
        "      operations: [created]",
        "      at: 2026-09-26T08:00:00.000Z",
        "      actor: {kind: agent, id: openai/codex, provider: openai, model: gpt-5-codex, runtime: codex}",
        "---",
        "",
        "# Source"
      ].join("\n"),
      "research/EV-2026-002-derived.md": [
        "---",
        "id: EV-2026-002",
        "title: Derived",
        "derived_from: [EV-2026-001]",
        "provenance:",
        "  contributions:",
        "    EXE-1:",
        "      operations: [created]",
        "      at: not-a-time",
        "      actor: {kind: agent, id: anthropic/claude-code, provider: anthropic, model: unknown, runtime: claude-code}",
        "---",
        "",
        "# Derived"
      ].join("\n")
    });

    const { diagnostics, catalog, graph } = await validateProject(directory);
    const byId = new Map(catalog.records.map((record) => [record.id, record]));

    expect(byId.get("EV-2026-001").provenance.contributions["EXE-20260926T080000000Z-aaaa0001"].at).toBe("2026-09-26T08:00:00.000Z");
    expect("provenance" in byId.get("EV-2026-002")).toBe(false);
    expect(byId.get("EV-2026-002").provenanceWithheld).toBe(true);
    expect(byId.get("EV-2026-002").provenanceStatus.verdict).toBe("malformed");
    expect(byId.get("EV-2026-002").derivedFrom).toEqual(["EV-2026-001"]);
    expect(graph.edges).toContainEqual({ source: "EV-2026-002", target: "EV-2026-001", type: "derived-from" });
    expect(diagnostics).toContainEqual(expect.objectContaining({ code: "malformed-provenance", severity: "warning", sourcePath: "research/EV-2026-002-derived.md" }));
    expect(diagnostics.some((item) => item.severity === "error")).toBe(false);
  });

  it("is unaffected for a repository with no provenance", async () => {
    const directory = temporaryProject({
      "research/EV-2026-001-plain.md": "---\nid: EV-2026-001\ntitle: Plain\nauthor_agent: codex\n---\n\n# Plain\n",
      "research/notes.md": "# Notes without front matter\n"
    });

    const { diagnostics, catalog } = await validateProject(directory);

    expect(catalog.records).toHaveLength(2);
    expect(catalog.records.every((record) => record.provenance === null && record.provenanceStatus === null && record.provenanceWithheld === false)).toBe(true);
    expect(catalog.schemaVersion).toBe("1.2");
    expect(catalog.records.every((record) => record.schemaVersion === "1.2")).toBe(true);
    expect(catalog.records.every((record) => record.created === null && record.updated === null)).toBe(true);
    expect(diagnostics.filter((item) => item.code.includes("provenance"))).toEqual([]);
  });

  it("includes provenance in the public catalog", () => {
    const block = { contributions: { "EXE-1": { operations: ["created"], at: "2026-09-26T08:00:00.000Z", actor: A, "x-extra": { kept: true } } } };
    const record = normalized({ id: "EV-2026-001", provenance: block, derived_from: "EV-2026-000" });
    const catalog = createPublicCatalog([record], { site: { title: "t", baseUrl: "/base/" } });

    expect(catalog.records[0].provenance).toEqual(block);
    expect(catalog.records[0].derivedFrom).toEqual(["EV-2026-000"]);
    expect(catalog.records[0].url.startsWith("/base/")).toBe(true);
  });
});
