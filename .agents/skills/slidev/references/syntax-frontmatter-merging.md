---
name: frontmatter-merging
description: Priority rules when importing slides with conflicting frontmatter
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Frontmatter Merging

When importing slides, frontmatter from main entry takes priority.

## Example

Main file (`slides.md`):
```md
---
src: ./cover.md
background: https://sli.dev/bar.png
class: text-center
---
```

Imported file (`cover.md`):
```md
---
layout: cover
background: https://sli.dev/foo.png
---

# Cover

Cover Page
```

## Result

```md
---
layout: cover
background: https://sli.dev/bar.png  # main entry wins
class: text-center
---

# Cover

Cover Page
```

## Priority Rule

Main entry > Imported file for duplicate keys.
