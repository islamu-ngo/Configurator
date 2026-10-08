---
name: presenter-timer
description: Timer and progress bar in presenter mode
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Presenter Timer

Timer and progress bar in presenter mode.

## Configuration

```yaml
---
duration: 30min
timer: stopwatch
---
```

## Options

- `duration`: Presentation length (default: `30min`)
- `timer`: Mode - `stopwatch` or `countdown` (default: `stopwatch`)

## Features

- Start, pause, reset controls
- Progress bar showing time elapsed/remaining
- Visible only in presenter mode

## Duration Format

- `30min` - 30 minutes
- `1h` - 1 hour
- `45min` - 45 minutes
