export function normalizeBaseUrl(baseUrl?: string) {
  if (!baseUrl || baseUrl === "/") {
    return "/";
  }

  const withLeadingSlash = baseUrl.startsWith("/") ? baseUrl : `/${baseUrl}`;
  return withLeadingSlash.endsWith("/") ? withLeadingSlash : `${withLeadingSlash}/`;
}

export function withBasePath(baseUrl: string | undefined, targetPath: string) {
  if (!targetPath) {
    return targetPath;
  }

  if (targetPath.startsWith("#")) {
    return targetPath;
  }

  if (/^(?:[a-z]+:)?\/\//i.test(targetPath)) {
    return targetPath;
  }

  const normalizedBase = normalizeBaseUrl(baseUrl);
  const trimmedTarget = targetPath.replace(/^\/+/, "");

  if (normalizedBase === "/") {
    return `/${trimmedTarget}`;
  }

  return `${normalizedBase}${trimmedTarget}`;
}
