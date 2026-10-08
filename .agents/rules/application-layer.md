---
name: application-layer
description: Reference-only Event application-layer guidance; no matching Configurator layer.
paths:
  - "reference-only/application-layer/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# application-layer rules

Twin files at `.agents/rules/application-layer.md` and `.omo/rules/application-layer.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
