import { defineConfig } from "astro/config";

const site = process.env.RESEARCH_PUBLISHER_SITE_URL || "https://example.com";
const base = process.env.RESEARCH_PUBLISHER_BASE_URL || "/";

export default defineConfig({
  site,
  base,
  output: "static",
  build: {
    // Keep research pages portable across GitHub Pages project paths,
    // artifact previews, and local static servers.
    inlineStylesheets: "always"
  }
});
