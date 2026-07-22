---
id: RP-2026-001
title: Reusable Research Publishing System
artifactType: research-package
researchArea: Visual Engineering
discipline:
  - Information Architecture
  - Interface Systems
summary: Build a reusable publishing system that turns research Markdown into a searchable static site.
status: active
version: "1.0"
confidence: 0.86
completion: 0.9
priority: high
authorAgent: codex
created: 2026-07-22
updated: 2026-07-22
tags:
  - publishing
  - static-site
keywords:
  - astro
  - pagefind
relatedDocuments:
  - DF-2026-001
  - HY-2026-001
evidenceIds:
  - EV-2026-001
hypothesisIds:
  - HY-2026-001
theoryIds:
  - TH-2026-001
---

## Executive Summary

Visual Engineering needs a publishing workflow that preserves Markdown as the canonical source while generating a readable, searchable website.

## Repository Context

The repository began empty apart from a prompt. That makes reusability easier to enforce because there is no legacy rendering stack to preserve.

## Current Understanding

The system should separate content normalization, relationship modeling, and page rendering so that future repositories can reuse the same package through configuration.

## Key Discoveries

- Static generation is sufficient for the current repository shape.
- Search requires two artifacts: public catalog JSON and a full-text index.
- Relationship resolution is stronger when stable identifiers outrank filenames.

## Open Questions

- How strict should CI become once more repositories adopt the schema?
- Which additional artifact types need first-class layouts?

