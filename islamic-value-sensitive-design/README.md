# Provider-responsibility assessment catalog

> **Audience:** Contributors | Maintainers | Reviewers | AI agents
> **Kind:** Guide
> **Status:** Current index; linked assessments retain their recorded lifecycle
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `.agents/skills/i-vsd/`, `islamic-value-sensitive-design/consultations/`, `islamic-value-sensitive-design/workstreams/`, `eng/knowledge/Knowledge.cs`

Use this catalog to find Islamic Value-Sensitive Design (I-VSD) reasoning about
provider responsibility: what maintainers control, who is affected, and what
mitigations or evidence a decision needs. This is navigation, not certification,
implementation approval, a legal opinion or a scholarly ruling.

The verification date means report paths and their initial review metadata were
inspected. It does not renew any report's evidence, disposition, findings,
mitigations or support claims. Entries below copy the **recorded** report status,
not a new freshness assessment. A report marked `current / plan-aligned` can
still describe an old or unimplemented design.

## Governance and review procedure

- [I-VSD workflow](../.agents/skills/i-vsd/SKILL.md) selects the appropriate mode.
- [Scope boundaries](../.agents/skills/i-vsd/resources/scope-boundaries.md) separate
  provider reasoning from religious-legal and other specialist determinations.
- [Report contract](../.agents/skills/i-vsd/resources/report-contract.md) defines
  claim boundaries, lifecycle, findings and evidence.
- [Plan integration](../.agents/skills/i-vsd/resources/integration-contract.md)
  binds reviewed input and mitigation mappings; changed authority, custody,
  support claims or responsibilities require revalidation.
- [Evidence levels](../.agents/skills/i-vsd/resources/evidence-and-validation-levels.md)
  distinguish source inspection, execution and outcome evidence.

Governance resources are reusable instructions. Consultations are bounded advice
or audits. Workstream assessments bind a particular design or reviewed input.
None of those document kinds silently grants target, publication or release
authority. Use [current status](../docs/internal/STATUS.md) for qualification and
the [knowledge map](../docs/internal/KNOWLEDGE_MAP.md) for authority relationships.

## Consultations

| Assessment | Recorded lifecycle / disposition | Recorded evidence cutoff |
| --- | --- | --- |
| [Browser production readiness](consultations/i-vsd-browser-production-readiness-detailed-audit.md) | current / changes-required | 2026-10-19 |
| [Browser feature completeness](consultations/i-vsd-browser-feature-completeness-detailed-audit.md) | current / advisory | Repository/execution 2026-10-20; external research 2026-10-18 |

Consultation findings belong to their named inspected state. A later workstream
or passing check does not automatically close them; follow the report's actual
disposition and replacement evidence.

## Workstream assessments

| Assessment | Recorded lifecycle / disposition | Recorded evidence cutoff |
| --- | --- | --- |
| [Configurator extraction](workstreams/i-vsd-configurator-extraction.md) | current / plan-aligned | 2026-10-08 |
| [Universal MVP](workstreams/i-vsd-universal-mvp.md) | current / plan-aligned | 2026-10-09 |
| [Desktop cross-platform packaging](workstreams/i-vsd-desktop-cross-platform-packaging.md) | current / plan-aligned | 2026-10-09 |
| [Complete browser configuration](workstreams/i-vsd-browser-complete-configuration.md) | current / plan-aligned | 2026-10-09 |
| [Browser interface and optional store](workstreams/i-vsd-browser-ui-and-definition-store.md) | current / plan-aligned | 2026-10-10 |
| [Maintainer authoring and distribution](workstreams/i-vsd-configurator-maintainer-authoring.md) | current / plan-aligned | 2026-10-10 |
| [Avalonia MVVM refactor](workstreams/i-vsd-avalonia-desktop-mvvm-refactor.md) | current / plan-aligned | 2026-10-11 |
| [Terminal hardening](workstreams/i-vsd-terminal-hardening.md) | current / plan-aligned | 2026-10-14 |
| [Native CLI adoption](workstreams/i-vsd-cli-native-adoption.md) | current / plan-aligned | 2026-10-15 |
| [Cross-client feature completeness](workstreams/i-vsd-cross-platform-feature-completeness.md) | current / plan-aligned | 2026-10-18 |
| [Truthful native publication revalidation](workstreams/i-vsd-cross-platform-feature-completeness-moral-diff-review.md) | current / plan-aligned | 2026-10-18 |
| [Browser production v1](workstreams/i-vsd-browser-production-v1.md) | current / plan-aligned | 2026-10-19 |
| [Guided configuration experiences](workstreams/i-vsd-guided-configuration-experiences.md) | current / plan-aligned | 2026-10-20 |
| [Browser interaction performance](workstreams/i-vsd-browser-interaction-performance.md) | current / plan-aligned | 2026-10-22 |
| [Browser UI and UX redesign](workstreams/i-vsd-browser-ui-ux-redesign.md) | current / plan-aligned | 2026-10-24 |
| [Cross-platform quality](workstreams/i-vsd-cross-platform-quality.md) | current / plan-aligned | 2026-10-27 |
| [Definition standard redesign](workstreams/i-vsd-definition-standard-redesign.md) | current / plan-aligned | 2026-10-28, supplied packet cutoff, not independently refreshed |
| [Artifact import and comparison](workstreams/i-vsd-artifact-import-comparison.md) | current / advisory | 2026-10-30 |
| [Knowledge and contributor experience](workstreams/i-vsd-knowledge-contributor-experience.md) | current / plan-aligned | 2026-10-30 |

For each assessment, read its reviewed input, claim boundary, validation gaps,
finding and mitigation IDs, and lifecycle before reusing a conclusion. A
planning-alignment statement covers the recorded plan, not every later execution
ledger edit or delivered product. Preserve original hashes and history.

## Retrieval and maintenance

The native knowledge tool treats **only this root README** as a current index.
Assessment bodies, including nested README files, remain historical/on-demand
in the audit profile even if their own header records `current`. This is a
retrieval distinction, not a change to the report author's recorded status.
See [classification and profiles](../eng/knowledge/README.md#classification-policy).

Add a route when a report is introduced. Copy its recorded metadata accurately;
do not change the body, refresh its date, declare an old finding closed or renew
approval merely to maintain this index. Revalidation belongs to the report
workflow and the actual changed responsibility.
