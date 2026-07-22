import { defineConfig } from "astro/config";

const site = process.env.RESEARCH_PUBLISHER_SITE_URL || "https://example.com";
const base = process.env.RESEARCH_PUBLISHER_BASE_URL || "/";

export default defineConfig({
  site,
  base,
  output: "static"
});

