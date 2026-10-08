---
name: blazor-server
description: Reference-only Event blazor-server guidance; no matching Configurator layer.
paths:
  - "reference-only/blazor-server/**"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# blazor-server rules

Twin files at `.agents/rules/blazor-server.md` and `.omo/rules/blazor-server.md` must remain byte-identical.

This preserved router is inactive in Configurator. Its source-system layer does not exist here; do not introduce it to satisfy historical Event guidance. See [target authority](../../docs/internal/extraction/FOUNDATION.md).
