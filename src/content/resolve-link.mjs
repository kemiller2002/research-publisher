import path from "node:path";

function withBasePath(baseUrl, targetPath) {
  const base = !baseUrl || baseUrl === "/" ? "/" : `/${baseUrl.replace(/^\/+|\/+$/g, "")}/`;
  const target = targetPath.replace(/^\/+/, "");
  return base === "/" ? `/${target}` : `${base}${target}`;
}

export function resolveDocumentLink({ sourcePath, href, documentsBySourcePath, baseUrl = "/" }) {
  if (!href || href.startsWith("#") || /^(?:[a-z]+:)?\/\//i.test(href) || /^(?:mailto|tel):/i.test(href)) {
    return { href, markdown: false, resolved: true };
  }

  const match = href.match(/^([^?#]+)(\?[^#]*)?(#.*)?$/);
  if (!match || !/\.md$/i.test(match[1])) {
    return { href, markdown: false, resolved: true };
  }

  const [, targetPath, query = "", fragment = ""] = match;
  const resolvedSourcePath = targetPath.startsWith("/")
    ? path.posix.normalize(targetPath.replace(/^\/+/, ""))
    : path.posix.normalize(path.posix.join(path.posix.dirname(sourcePath), targetPath));
  const targetDocument = documentsBySourcePath.get(resolvedSourcePath);

  if (!targetDocument) {
    return { href, markdown: true, resolved: false, resolvedSourcePath };
  }

  return {
    href: `${withBasePath(baseUrl, targetDocument.url)}${query}${fragment}`,
    markdown: true,
    resolved: true,
    resolvedSourcePath
  };
}
