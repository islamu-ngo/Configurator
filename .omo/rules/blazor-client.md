---
name: blazor-client
description: Configurator blazor-client boundaries.
paths:
  - "src/Configurator.Browser/**/*.razor"
  - "src/Configurator.Browser/**/*.cs"
  - "src/Configurator.Browser/**/*.css"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# blazor-client rules

Twin files at `.agents/rules/blazor-client.md` and `.omo/rules/blazor-client.md` must remain byte-identical.

Follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Verify only the affected owner and report exact results.

The client is static public-only Blazor WebAssembly. No BFF, HAL role gates, target API client, service worker, cookies or private storage. Launch resolves registered bounded public data and pins revision/digest. Preserve accessible keyboard, focus, status and RTL behavior.
