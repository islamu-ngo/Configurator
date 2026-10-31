---
name: ip-clean-room
description: Apply when source, documentation, active planning, or dependency changes are externally informed.
paths:
  - "src/**/*"
  - "tests/**/*"
  - "eng/**/*"
  - "docs/**/*"
  - "dev/active/**/*"
  - ".agents/**/*"
  - "**/*.csproj"
  - "**/packages.lock.json"
  - "Directory.*.props"
  - "NuGet.Config"
  - "global.json"
  - "THIRD-PARTY-NOTICES.md"
related_skills: [ip-clean-room, agentic-research]
related_docs: [AGENTS.md, docs/internal/legal/IP_GOVERNANCE.md, docs/internal/QUICK_REFERENCE.md, THIRD-PARTY-NOTICES.md]
minimum_tests: []
related_intents: [repository-tooling, definition-engine, protected-artifacts, client-interface, agent-context, documentation, provenance-docs]
---

# IP Clean-Room And Outbound-License Protection

Twin files at `.agents/rules/ip-clean-room.md` and `.omo/rules/ip-clean-room.md` must remain byte-identical.

> **Applies to:** implementation source, tests, engineering, documentation, active design workstreams and dependency metadata.
> **Authority:** [IP Governance](../../docs/internal/legal/IP_GOVERNANCE.md), [AGENTS.md](../../AGENTS.md) and [Quick Reference](../../docs/internal/QUICK_REFERENCE.md).

## Rules (Correct / Wrong)

| # | Rule | Correct | Wrong |
|---|---|---|---|
| 1 | External input | Source-free functional observations and constraints | Restricted external code, snippets, ASTs, SQL, tests, comments, prose or assets |
| 2 | Context boundary | Fresh third-party-informed implementation receives sanitized handoff only | Continue in a source-bearing context |
| 3 | Literal copying | Independently designed third-party-informed work; faithful copying of Steward-authorized original ISLAMU-owned text | Treat first-party MIT transfer as permission to relicense third-party expression |
| 4 | SSO | Record Abstraction-Filtration-Comparison (AFC) and independently designed structure, sequence and organization (SSO) for externally informed work | Assume identifier/framework changes prove independence |
| 5 | Dependencies | Evaluate actual resolved/published closure, terms, approvals and notices for MIT distribution | Treat a scanner, contributor signature or central version declaration as relicensing authority |
| 6 | Evidence | Source register + clean handoff + SSO decision + dependency record + task/review evidence links | Anonymous observations or retained restricted expression |

The Steward permits copying original ISLAMU-owned scaffold text into this MIT repository. That exception does not include the 154 unlicensed Evolve example-site corpus entries or Contributor Covenant expression. Do not ingest or reconstruct either exclusion.

## Must-Reads for This Path

Only externally informed or provenance/dependency decisions activate this
resource chain. Internal copy-editing and navigation corrections reuse target
context; do not force FOUNDATION or a source register for ordinary prose.
- [IP Governance](../../docs/internal/legal/IP_GOVERNANCE.md)
- [Quick Reference](../../docs/internal/QUICK_REFERENCE.md)
- [IP Clean-Room Skill](../../.agents/skills/ip-clean-room/SKILL.md), with the first-party transfer exception above.
- [Third-Party Notices](../../THIRD-PARTY-NOTICES.md) for component changes.

## Anti-Patterns (Forbidden on These Paths)
- Ingesting restricted third-party source or source-derived representations into implementation prompts/tooling.
- Copying third-party expressive organization, workflows, schemas, UI composition, tests, comments or documentation merely with renamed identifiers.
- Removing attribution/provenance instead of removing restricted expression.
- Continuing after contamination: discard only owned unmerged contaminated output, preserve others' work and restart from a sanitized handoff.
- Transferring Event credentials, signers, trust activation or publication history.
- Substituting reviewed dependency versions or bypassing required bundled-asset notices.

## Verification
- Complete the retained [SSO checklist](../../.agents/skills/ip-clean-room/resources/sso-and-provenance-review.md) for externally informed work.
- Review exact source identities/access dates, permitted first-party provenance and exclusions without retaining restricted expression.
- For dependency changes use native owner-specific engineering commands in [PROJECTS.md](../../PROJECTS.md) and [Release Runbook](../../docs/internal/RELEASE_RUNBOOK.md); preserve component notices/approval and fail-closed publication trust. No invented Event license-check command.
- Context-only changes use metadata, link, twin and whitespace checks, not product tests.

## Related
- [Governance](../../docs/internal/GOVERNANCE.md)
- [Documentation Architecture](../../docs/internal/DOCUMENTATION_ARCHITECTURE.md)
- [Documentation Index](../../docs/README.md)
