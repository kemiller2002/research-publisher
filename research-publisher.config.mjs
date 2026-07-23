export default {
  site: {
    title: "Visual Engineering Research",
    description: "Searchable Visual Engineering research repository",
    baseUrl: "/research-publisher/",
    language: "en",
    siteUrl: "https://kemiller2002.github.io/research-publisher/"
  },
  repository: {
    name: "research-publisher",
    sourceUrl: "https://github.com/kemiller2002/research-publisher"
  },
  content: {
    include: [
      "research/**/*.md",
      "docs/**/*.md"
    ],
    exclude: [
      "input-documents/**",
      "dist/**",
      "node_modules/**",
      "build-reports/**",
      "fixtures/**",
      "**/archive/**"
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
      "--color-bg": "#f6f3ea",
      "--color-surface": "#fffdf8",
      "--color-ink": "#1b2129",
      "--color-accent": "#9d3c12",
      "--color-accent-soft": "#f6d6c7",
      "--color-border": "#dccfc0",
      "--font-display": "\"Fraunces\", Georgia, serif",
      "--font-body": "\"Source Sans 3\", system-ui, sans-serif"
    }
  }
};
