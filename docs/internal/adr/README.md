# Architecture Decision Index

> **Audience:** Contributors | Maintainers | AI agents
> **Status:** Mixed; accepted designs and explicitly proposed records
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `docs/internal/adr/`, `docs/internal/OPERATIONS.md`, `docs/internal/IMPORT_COMPARISON.md`

An ADR records a design disposition, not an implementation or release receipt.
The status below follows each record's explicit wording. Use
[Status and evidence](../STATUS.md) for delivery, package, actual-host,
accessibility and target boundaries, and [Operations](../OPERATIONS.md#current-procedure-entry)
for procedures and dated observations. No record is promoted by this index.

## Accepted designs

| Decision record | Accepted scope and historical qualifications | Delivery / evidence owner |
| --- | --- | --- |
| [Definition runtime boundary](ADR-definition-runtime-boundary.md) | Bounded data, native protection, canonical identity and target independence; original public-only Browser paragraphs are explicitly superseded by the Browser trust amendment | [Core](../CORE_DEFINITION_CONTRACT.md), [launch](../LAUNCH_CONTRACT.md), [protected artifacts](../PROTECTED_ARTIFACTS.md) |
| [Browser session trust and export](ADR-browser-session-trust-and-export.md) | Accepted design for static WASM, bounded sources, transient consented custody and deliberate export | [Browser custody procedure/evidence](../OPERATIONS.md#browser-custody-and-offline-verification) |
| [Maintainer authoring and distribution](ADR-maintainer-authoring-and-canonical-distribution.md) | Accepted one-source compiler and canonical distribution; historical vocabulary does not create a supported v1 interpreter | [Maintainer contract](../MAINTAINER_AUTHORING.md), [v2 final delivery](../OPERATIONS.md#final-definition-delivery-and-responsibility) |
| [Optional public Store](ADR-optional-definition-store.md) | Accepted optional in-repository discovery; authoring amendment replaces original metadata input | [Store contract](../DEFINITION_STORE.md), [package status](../STATUS.md#package-admission) |
| [Terminal editing and publication lifetimes](ADR-terminal-editing-and-publication-lifetimes.md) | Accepted shared editing, operation ownership and publication-history boundaries; native qualification separate | [Terminal contract](../TERMINAL_CLIENT.md), [executed surfaces](../STATUS.md#executed-surface-and-host) |
| [CLI command runtime](ADR-cli-command-runtime.md) | Accepted runtime boundary; historical v1-to-v2 command wording is not today's machine version | [Current CLI contract](../CLI_CONTRACT.md), [implementation status](../STATUS.md#implemented-scope) |
| [Form computation](ADR-browser-form-computation.md) | Accepted revision-owned computation and bounded rendering; named reference-host gates recorded | [Frozen candidate evidence](../OPERATIONS.md#frozen-indexed-edit-candidate-2026-10-23), [later AOT candidate](../OPERATIONS.md#committed-aot-editor-candidate-2026-10-24) |
| [Workspace presentation](ADR-browser-workspace-presentation.md) | Accepted thin controls, shared disclosure and contextual help; runtime qualification separate | [Browser UI](../BROWSER_UI.md), [Browser evidence](../OPERATIONS.md#browser-custody-and-offline-verification) |
| [Cross-platform quality ownership](ADR-cross-platform-quality-ownership.md) | Accepted native lifetimes plus appended cross-platform ownership; retained native checkpoint is not the full program gate | [Native checkpoint](../OPERATIONS.md#native-ownership-qualification-2026-10-27), [program qualification](../OPERATIONS.md#cross-platform-quality-program-qualification-2026-10-27) |
| [Single-pass preparation](ADR-single-pass-preparation-ownership.md) | Accepted composition and staged adoption ownership; not broad preparation responsiveness qualification | [Program evidence and limits](../OPERATIONS.md#cross-platform-quality-program-qualification-2026-10-27) |
| [Bounded definition v2](ADR-definition-standard-v2.md) | Accepted D01-D12; Core v2 delivered, historical authoring defaults/vocabulary do not establish another interpreter | [Conformance profile](../DEFINITION_CONFORMANCE_PROFILE.md), [v2 qualification](../OPERATIONS.md#definition-v2-qualification) |
| [Evidence-aware import/comparison](ADR-artifact-import-comparison.md) | Accepted design; implementation and qualification in progress | [Import/comparison contract](../IMPORT_COMPARISON.md); client/package qualification pending, earlier package evidence not inherited |

## Proposed records

| Record | Explicit recorded disposition | Reading boundary |
| --- | --- | --- |
| [Browser answer intent and denial](ADR-browser-answer-intent-and-denial.md) | Proposed; design refined, not implemented or I-VSD-revalidated according to this record | Related later implementation narratives do not silently change this record's acceptance status |
| [Configuration experience and answer authority](ADR-configuration-experience-and-answer-authority.md) | Proposed planning decision, not implemented or production-qualified according to this record | Use [workspace ADR](ADR-browser-workspace-presentation.md) and current contracts for their narrower accepted scope; do not infer blanket acceptance |

## Using historical decision text

Read amendments and successor contracts before copying version numbers or
surface descriptions. Accepted rationale may survive while an earlier wire
version, original Browser restriction or owner name is historical. A proposed
record remains proposed until its disposition is explicitly resolved; a later
test total cannot resolve that decision. [Knowledge map](../KNOWLEDGE_MAP.md)
connects these records to strategy without inventing missing approvals.
