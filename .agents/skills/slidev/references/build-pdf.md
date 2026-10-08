---
name: pdf
description: Include downloadable PDF in SPA build
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Generate PDF when Building

Generate a downloadable PDF alongside your built slides.

## Enable in Headmatter

```md
---
download: true
---
```

This generates a PDF and adds a download button to the built slides.

## Custom PDF URL

Skip generation and use an existing PDF:

```md
---
download: 'https://example.com/my-talk.pdf'
---
```

## CLI Option

```bash
slidev build --download
```

## Export Options

Configure PDF export settings via:
- CLI: `slidev build --download --with-clicks --timeout 60000`
- Headmatter: Set `exportFilename`, `withClicks`, etc.
