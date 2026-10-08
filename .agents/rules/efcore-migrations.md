---
name: efcore-migrations
description: Reference-only Event efcore-migrations guidance; no matching Configurator layer.
paths:
  - "reference-only/efcore-migrations/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# efcore-migrations rules

Twin files at `.agents/rules/efcore-migrations.md` and `.omo/rules/efcore-migrations.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
