---
name: canvas-size
description: Configure slide canvas dimensions and aspect ratio
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Slide Canvas Size

Set the canvas dimensions for all slides.

## Configuration

```md
---
aspectRatio: 16/9
canvasWidth: 980
---
```

- `aspectRatio`: Ratio of width to height (default: `16/9`)
- `canvasWidth`: Canvas width in pixels (default: `980`)

## Related Features

- Scale individual slides: use `zoom` frontmatter option
- Scale elements: use `<Transform>` component
