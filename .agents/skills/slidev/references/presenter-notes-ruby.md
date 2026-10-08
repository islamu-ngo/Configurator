---
name: notes-auto-ruby
description: Automatically add ruby text (pronunciation guides) to presenter notes
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Notes Auto Ruby

Automatically add ruby text (pronunciation guides) to presenter notes.

## Configuration

```md
---
notesAutoRuby:
  日本語: ni hon go
  勉強: べんきょう
---
```

## Example

Notes:
```md
<!--
私は日本語を勉強しています。
-->
```

Renders with ruby annotations above the matched words.

## Use Case

- Language learning presentations
- Pronunciation guides for technical terms
- Reading assistance for non-native text
