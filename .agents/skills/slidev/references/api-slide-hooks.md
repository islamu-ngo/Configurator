---
name: slide-hooks
description: Lifecycle hooks for slide components
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Slide Hooks

Lifecycle hooks for slide components.

## Available Hooks

```ts
import { onSlideEnter, onSlideLeave, useIsSlideActive } from '@slidev/client'

const isActive = useIsSlideActive()

onSlideEnter((to, from) => {
  // Called when slide becomes active
})

onSlideLeave((to, from) => {
  // Called when slide becomes inactive
})
```

## Important

Do NOT use `onMounted` / `onUnmounted` in slides - component instance persists even when slide is inactive.

Use `onSlideEnter` and `onSlideLeave` instead.

## Use Cases

- Start/stop animations
- Play/pause media
- Initialize/cleanup resources
- Track analytics
