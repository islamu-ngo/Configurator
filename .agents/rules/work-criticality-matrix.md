---
name: work-criticality-matrix
description: Configurator work-criticality-matrix boundaries.
paths:
  - "src/**/*.cs"
  - "tests/**/*.cs"
related_docs: [AGENTS.md, docs/internal/extraction/FOUNDATION.md]
minimum_tests: []
related_intents: []
---

# work-criticality-matrix rules

Twin files at `.agents/rules/work-criticality-matrix.md` and `.omo/rules/work-criticality-matrix.md` must remain byte-identical.

Follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Verify only the affected owner and report exact results.

Definition admission, protected writes and privacy changes require adversarial invariant tests. UI/docs changes use scoped economic checks. No database matrix or Event backend reviewers are required for this offline engine. Ask only about material unresolved product decisions.
