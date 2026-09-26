// Reading front matter received as JSON text (REQ-RP-PROV R14.12; Praxis contract
// revision 1.2, rule 1). JSON.parse silently keeps the last of two repeated member
// names, so the provenance member is taken from the text itself, as raw text, and
// classified with the vendored `classifyText`. Every function here is pure and never
// throws: text that is not a JSON object yields `undefined`.

const tryParse = (text) => {
  try {
    return { ok: true, value: JSON.parse(text) };
  } catch {
    return { ok: false };
  }
};

const isObject = (value) => value !== null && typeof value === "object" && !Array.isArray(value);

/** Index of the first character at or after `index` that is not JSON whitespace. */
const skipSpace = (text, index) => {
  const offset = text.slice(index).search(/[^ \t\n\r]/);
  return offset < 0 ? text.length : index + offset;
};

/** Index just past the JSON string starting at `index` (text is already known to be valid JSON). */
const stringEnd = (text, index) => {
  const match = /^"(?:[^"\\]|\\.)*"/s.exec(text.slice(index));
  return match ? index + match[0].length : -1;
};

/** Index just past the JSON value starting at `index`: strings are skipped whole, brackets are balanced. */
const valueEnd = (text, index) => {
  const first = text[index];
  if (first === '"') return stringEnd(text, index);
  if (first !== "{" && first !== "[") {
    const scalar = /^(?:-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null)/.exec(text.slice(index));
    return scalar ? index + scalar[0].length : -1;
  }
  const walk = (position, depth) => {
    if (position >= text.length) return -1;
    const char = text[position];
    if (char === '"') {
      const end = stringEnd(text, position);
      return end < 0 ? -1 : walk(end, depth);
    }
    if (char === "{" || char === "[") return walk(position + 1, depth + 1);
    if (char === "}" || char === "]") return depth === 1 ? position + 1 : walk(position + 1, depth - 1);
    const next = text.slice(position).search(/["{}[\]]/);
    return next < 0 ? -1 : walk(position + next, depth);
  };
  return walk(index, 0);
};

/** `[name, rawValueText]` pairs of the top-level object, in order, from `index` (just inside `{`). */
const membersFrom = (text, index, found) => {
  const start = skipSpace(text, index);
  if (text[start] === "}") return found;
  const nameEnd = stringEnd(text, start);
  if (nameEnd < 0) return undefined;
  const name = JSON.parse(text.slice(start, nameEnd));
  const colon = skipSpace(text, nameEnd);
  if (text[colon] !== ":") return undefined;
  const valueStart = skipSpace(text, colon + 1);
  const end = valueEnd(text, valueStart);
  if (end < 0) return undefined;
  const members = [...found, [name, text.slice(valueStart, end)]];
  const separator = skipSpace(text, end);
  if (text[separator] === ",") return membersFrom(text, separator + 1, members);
  return text[separator] === "}" ? members : undefined;
};

/**
 * The top-level members of a JSON object text as `[name, rawValueText]` pairs, keeping
 * repeated names (JSON.parse would silently keep only the last). `undefined` when the
 * text is not valid JSON or not an object.
 */
export const topLevelMembers = (text) => {
  if (typeof text !== "string") return undefined;
  const parsed = tryParse(text);
  if (!parsed.ok || !isObject(parsed.value)) return undefined;
  return membersFrom(text, skipSpace(text, 0) + 1, []);
};

/** The raw value texts of every top-level member called `name` (several when the name is repeated). */
export const memberTexts = (members, name) => members.filter(([member]) => member === name).map(([, value]) => value);
