# Theming Research Publisher

Research Publisher keeps layout, typography scale, spacing, components, and semantic color roles in the package. Host repositories can change the palette without copying package CSS, so sites can have distinct identities while retaining a shared visual system.

## Override colors in the host repository

Add `branding.cssVariables` to the host repository's `research-publisher.config.mjs`:

```js
export default {
  // Existing site, content, and output settings...
  branding: {
    cssVariables: {
      "--color-bg": "#f4f7fb",
      "--color-surface": "#ffffff",
      "--color-ink": "#172033",
      "--color-accent": "#2457a6",
      "--color-accent-strong": "#173b73",
      "--color-accent-soft": "#dce8fa",
      "--color-on-accent": "#ffffff",
      "--color-border": "#ccd5e3",
      "--color-border-strong": "#8b98aa",
      "--color-muted": "#536174",
      "--color-dark": "#172033",
      "--color-code-ink": "#303936",
      "--color-on-dark": "#f7f5ee",
      "--color-focus-ring": "#5b8bd9",
      "--color-focus-ring-inverse": "#ffffff",
      "--color-print-bg": "#ffffff"
    }
  }
};
```

Every entry is optional. The package default remains active for any omitted variable. Prefer overriding the smallest coherent set needed for the site's identity.

Package defaults live in a low-priority CSS cascade layer. The host variables are unlayered, so they override package defaults even when the build tool changes stylesheet emission order.

## Stable semantic roles

| Variable | Purpose |
| --- | --- |
| `--color-bg` | Page background |
| `--color-surface` | Raised or grouped content surfaces |
| `--color-ink` | Primary text and strong rules |
| `--color-accent` | Links, focus, and primary emphasis |
| `--color-accent-strong` | Strong accent state |
| `--color-accent-soft` | Subtle accent background |
| `--color-on-accent` | Text shown on accent surfaces |
| `--color-border` | Standard separators and outlines |
| `--color-border-strong` | Emphasized separators |
| `--color-muted` | Secondary text |
| `--color-dark` | Dark surfaces |
| `--color-code-ink` | Inline code text |
| `--color-on-dark` | Text shown on dark surfaces |
| `--color-focus-ring` | Keyboard focus outline |
| `--color-focus-ring-inverse` | Keyboard focus outline on dark surfaces |
| `--color-print-bg` | Printed page background |

These semantic names are the public theming contract. Package components consume the roles instead of host-specific palette names.

## Accessibility requirements

Color combinations remain the host repository's responsibility after overrides. Verify at minimum:

- primary and muted text contrast against their backgrounds;
- link and focus visibility against page and surface backgrounds;
- text using `--color-on-accent` against `--color-accent`;
- text on `--color-dark` using `--color-on-dark`;
- meaning remains understandable without color;
- forced-colors/high-contrast mode and keyboard focus remain usable.

Run a production build after changing the palette and inspect representative index, collection, document, search, code, and focus states.
