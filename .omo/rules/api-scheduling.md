---
name: api-scheduling
description: Reference-only Event api-scheduling guidance; no matching Configurator layer.
paths:
  - "reference-only/api-scheduling/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# api-scheduling rules

Twin files at `.agents/rules/api-scheduling.md` and `.omo/rules/api-scheduling.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
