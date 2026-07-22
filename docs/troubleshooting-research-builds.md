# Troubleshooting Research Builds

## Common Problems

### Duplicate identifiers

The validation stage fails when two documents normalize to the same `id`. Rename or correct the front matter so the identifiers are unique.

### Duplicate output URLs

Two documents generated the same slug or explicit URL. Add an explicit `slug` or `url` field to disambiguate them.

### Missing search assets

If `dist/pagefind/pagefind.js` is missing, the Pagefind post-build step did not complete. Re-run `npm run research:build` and inspect the terminal output.

### Legacy Markdown missing metadata

Compatibility mode publishes legacy content, but CI can still warn about missing IDs or incomplete metadata. Use the inventory report and validation diagnostics to prioritize cleanup.

