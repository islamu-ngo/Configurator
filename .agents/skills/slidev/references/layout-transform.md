---
name: transform-component
description: Scale elements without affecting slide layout using the Transform component
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Transform Component

Scale elements without affecting slide layout.

## Usage

```md
<Transform :scale="0.5" origin="top center">
  <YourElements />
</Transform>
```

## Props

- `scale`: Scale factor (0.5 = 50%, 2 = 200%)
- `origin`: Transform origin (CSS transform-origin value)

## Use Cases

- Shrink large diagrams
- Scale code blocks
- Fit oversized content
- Create emphasis effects

## Related Features

- Scale all slides: Use `canvasWidth` / `aspectRatio` in headmatter
- Scale individual slides: Use `zoom` frontmatter option
