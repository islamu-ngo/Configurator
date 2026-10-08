---
name: tests
description: Configurator tests boundaries.
paths:
  - "tests/**/*.cs"
  - "eng/**/tests/**/*.cs"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# tests rules

Twin files at `.agents/rules/tests.md` and `.omo/rules/tests.md` must remain byte-identical.

Follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Verify only the affected owner and report exact results.

Use TUnit through Microsoft.Testing.Platform and bUnit for browser semantics. Subscribe to async signals before triggering work; no timing sleeps, source scraping, mock mirroring, warning suppressions or test weakening. Assert definition admission, sensitivity floors, generation fencing and create-only output through observable public seams. Tooling changes run engineering tests, not product suites.
