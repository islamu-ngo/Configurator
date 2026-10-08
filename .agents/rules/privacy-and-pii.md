---
name: privacy-and-pii
description: Configurator privacy-and-pii boundaries.
paths:
  - "src/Configurator.Core/**/*.cs"
  - "src/Configurator.Artifacts/**/*.cs"
  - "src/Configurator.Presentation/**/*.cs"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# privacy-and-pii rules

Twin files at `.agents/rules/privacy-and-pii.md` and `.omo/rules/privacy-and-pii.md` must remain byte-identical.

Follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Verify only the affected owner and report exact results.

Definitions cannot demote private answers. Native answer-derived artifacts remain restricted. Browser consumes public metadata only, never secret entry, private previews, persistence or target authentication. Diagnostics expose stable codes, not values or raw exception/path content.
