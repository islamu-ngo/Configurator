---
name: api-controllers
description: Reference-only Event api-controllers guidance; no matching Configurator layer.
paths:
  - "reference-only/api-controllers/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# api-controllers rules

Twin files at `.agents/rules/api-controllers.md` and `.omo/rules/api-controllers.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
