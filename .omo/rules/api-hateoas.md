---
name: api-hateoas
description: Reference-only Event api-hateoas guidance; no matching Configurator layer.
paths:
  - "reference-only/api-hateoas/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# api-hateoas rules

Twin files at `.agents/rules/api-hateoas.md` and `.omo/rules/api-hateoas.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
