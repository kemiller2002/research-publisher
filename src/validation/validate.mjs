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
  }

  return diagnostics;
}

