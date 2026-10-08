---
name: auth-trust-boundaries
description: Reference-only Event auth-trust-boundaries guidance; no matching Configurator layer.
paths:
  - "reference-only/auth-trust-boundaries/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# auth-trust-boundaries rules

Twin files at `.agents/rules/auth-trust-boundaries.md` and `.omo/rules/auth-trust-boundaries.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
