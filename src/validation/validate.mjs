import { audienceVocabulary, documentPurposeVocabulary } from "../metadata/normalize.mjs";

const allowedPurposes = new Set(documentPurposeVocabulary);
const allowedAudiences = new Set(audienceVocabulary);

export function validateDocuments(documents) {
  const diagnostics = [];
  const idMap = new Map();
  const urlMap = new Map();

  for (const document of documents) {
    if (!document.title) {
      diagnostics.push({
        severity: "error",
        code: "missing-title",
        sourcePath: document.sourcePath,
        message: "Document is missing a title."
      });
    }

    if (!document.id) {
      diagnostics.push({
        severity: "warning",
        code: "missing-id",
        sourcePath: document.sourcePath,
        message: "Compatibility mode inferred metadata because no canonical identifier was present."
      });
    } else if (idMap.has(document.id)) {
      diagnostics.push({
        severity: "error",
        code: "duplicate-id",
        sourcePath: document.sourcePath,
        message: `Duplicate identifier ${document.id}.`
      });
    } else {
      idMap.set(document.id, document.sourcePath);
    }

    if (urlMap.has(document.url)) {
      diagnostics.push({
        severity: "error",
        code: "duplicate-url",
        sourcePath: document.sourcePath,
        message: `Duplicate output URL ${document.url}.`
      });
    } else {
      urlMap.set(document.url, document.sourcePath);
    }

    for (const link of document.relatedDocuments) {
      if (!link) {
        diagnostics.push({
          severity: "warning",
          code: "empty-relation",
          sourcePath: document.sourcePath,
          message: "Empty related document reference."
        });
      }
    }

    for (const purpose of document.purposes) {
      if (!allowedPurposes.has(purpose)) {
        diagnostics.push({
          severity: "warning",
          code: "unknown-purpose",
          sourcePath: document.sourcePath,
          message: `Unknown document purpose "${purpose}". Use a controlled purpose or update the publisher vocabulary deliberately.`
        });
      }
    }

    for (const audience of document.audiences) {
      if (!allowedAudiences.has(audience)) {
        diagnostics.push({
          severity: "warning",
          code: "unknown-audience",
          sourcePath: document.sourcePath,
          message: `Unknown audience "${audience}". Use a controlled audience or update the publisher vocabulary deliberately.`
        });
      }
    }

    if (document.entryPoint && !document.project) {
      diagnostics.push({
        severity: "warning",
        code: "entry-point-without-project",
        sourcePath: document.sourcePath,
        message: "Entry points should declare a project so they can be placed in a stable project guide."
      });
    }

    if (document.entryPoint && document.purposes.length === 0) {
      diagnostics.push({
        severity: "warning",
        code: "entry-point-without-purpose",
        sourcePath: document.sourcePath,
        message: "Entry points should declare at least one reader purpose."
      });
    }
  }

  return diagnostics;
}
