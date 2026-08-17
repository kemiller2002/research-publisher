export default {
  site: {
    title: "Visual Engineering Research",
    description: "Searchable Visual Engineering research repository",
    baseUrl: "/",
    language: "en",
    siteUrl: "https://research-publisher.echelonfoundry.com/"
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
    cssVariables: {}
  }
};
