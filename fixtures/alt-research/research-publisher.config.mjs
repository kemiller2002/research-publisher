export default {
  site: {
    title: "Civic Systems Notes",
    description: "Reusable fixture project for research-publisher",
    baseUrl: "/",
    language: "en",
    siteUrl: "https://example.com/civic-systems"
  },
  repository: {
    name: "civic-systems-fixture",
    sourceUrl: "https://github.com/example/civic-systems-fixture"
  },
  content: {
    include: [
      "content/**/*.md"
    ],
    exclude: [
      "content/**/archive/**"
    ],
    drafts: false
  },
  metadata: {
    mode: "compatible",
    strictInCI: true,
    required: [
      "title"
    ],
    stableIdPrefixes: [
      "RP",
      "JR",
      "EV",
      "HY",
      "TH",
      "EX",
      "DF",
      "CN",
      "GL"
    ]
  },
  output: {
    directory: "dist",
    catalog: "data/research-catalog.json",
    diagnostics: "data/build-diagnostics.json"
  },
  features: {
    search: true,
    filters: true,
    relatedDocuments: true,
    backlinks: true,
    tableOfContents: true,
    readingProgress: false,
    graphData: true
  },
  branding: {
    logo: null,
    cssVariables: {
      "--color-bg": "#eef6ff",
      "--color-surface": "#ffffff",
      "--color-ink": "#10233f",
      "--color-accent": "#0f766e",
      "--color-accent-soft": "#c8f4ef",
      "--color-border": "#c8d8ec",
      "--font-display": "\"Bricolage Grotesque\", system-ui, sans-serif",
      "--font-body": "\"IBM Plex Sans\", system-ui, sans-serif"
    }
  }
};
