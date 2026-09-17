# Architecture Decision Records — Research Publisher vNext

| ADR | Decision | Reversibility |
| --- | --- | --- |
| [0001](0001-ros-markdown-canonical.md) | ROS Markdown remains canonical | Low cost to keep; high cost to reverse |
| [0002](0002-typed-runtime-model.md) | Typed F# runtime model, no generic graph | Moderate |
| [0003](0003-static-first.md) | Static-first publishing to GitHub Pages | Low — re-evaluate on measurement |
| [0004](0004-fsharp-core.md) | F# owns parsing, domain, validation, projection | High cost to reverse |
| [0005](0005-limen-browser.md) | Limen engine/kernel split; static HTML baseline | Moderate |
| [0006](0006-url-strategy.md) | ID-anchored URLs, path-key fallback, never title | Low — redirects absorb change |
| [0007](0007-machine-readable-contracts.md) | Sharded, versioned, HTML-free indexes | Low |
| [0008](0008-search.md) | Search built from the typed model, not rendered HTML | Moderate |
| [0009](0009-relationship-derivation.md) | Canonical and derived relations are distinct types | High — this is a correctness boundary |
| [0010](0010-no-fabricated-data.md) | Absent data stays absent | Low |
