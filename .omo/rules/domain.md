---
name: domain
description: Reference-only Event domain guidance; no matching Configurator layer.
paths:
  - "reference-only/domain/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# domain rules

Twin files at `.agents/rules/domain.md` and `.omo/rules/domain.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
