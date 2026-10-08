---
name: efcore-persistence
description: Reference-only Event efcore-persistence guidance; no matching Configurator layer.
paths:
  - "reference-only/efcore-persistence/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# efcore-persistence rules

Twin files at `.agents/rules/efcore-persistence.md` and `.omo/rules/efcore-persistence.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
