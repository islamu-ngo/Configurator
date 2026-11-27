# Architecture Decision Index

> **Audience:** Contributors | Maintainers | AI agents
> **Status:** Mixed; accepted designs and explicitly proposed records
> **Owner:** Contributor Experience
> **Last Verified:** 2026-11-23
> **Source Anchors:** `docs/internal/adr/`, `docs/internal/OPERATIONS.md`, `docs/internal/IMPORT_COMPARISON.md`
> **Review basis:** Decision-index source review on 2026-11-23 (added Desktop SukiUI UI-library decision and Static Browser Coolify container image decision); historical execution and qualification dates are unchanged.

An ADR records a design disposition, not an implementation or release receipt.
The status below follows each record's explicit wording. Use
[Status and evidence](../STATUS.md) for delivery, package, actual-host,
accessibility and target boundaries, and [Operations](../OPERATIONS.md#current-procedure-entry)
for procedures and dated observations. No record is promoted by this index.

## Accepted designs

| Decision record | Accepted scope and historical qualifications | Delivery / evidence owner |
| --- | --- | --- |
| [Captured export evidence and reviewed value transfer](ADR-output-guidance-and-value-transfer.md) | Accepted captured export identity, conservative freshness and one accepted-state transfer; client/package qualification remains separate | [Presentation contract](../PRESENTATION_STATE.md) |
| [Encrypted local profiles, upgrade modes, and target delta apply](ADR-encrypted-local-profiles-and-target-apply.md) | Accepted and implemented local profiles using bounded library-owned JSON JWE; original Argon2id/custom-envelope proposal is historical. Remote target apply remains proposed and unimplemented | [Current decision](ADR-encrypted-local-profiles-and-target-apply.md#current-decision), [qualification status](../STATUS.md#implemented-scope) |
| [Desktop workspace composition](ADR-desktop-workspace-composition.md) | Accepted three-stage projection, bounded visible scopes and existing semantic/publication owners; appearance/high-contrast coupling amended for planned SukiUI direction | [Desktop contract](../DESKTOP_CLIENT.md), [SukiUI UI-library ADR](ADR-desktop-ui-library-and-sukiui.md) |
| [Desktop UI library selection (SukiUI)](ADR-desktop-ui-library-and-sukiui.md) | Accepted 2026-11-23 product-owner direction: adopt 100% MIT `SukiUI` (Avalonia 12 `7.x`), delegate Desktop UI/theming/controls to SukiUI, keep safety/custody/protected output non-negotiable and treat Desktop RTL/WCAG clamping/Browser-parity density as optional bonuses. **Implementation and dependency admission planned; Desktop currently runs `Avalonia.Themes.Fluent 12.1.6`** | [Current decision](ADR-desktop-ui-library-and-sukiui.md#decision), [Desktop contract](../DESKTOP_CLIENT.md), [Design alternatives](../DESIGN_ALTERNATIVES.md#8-desktop-ui-component-library-and-delegation-strategy) |
| [Domain vocabulary and workspace ownership](ADR-domain-vocabulary-and-workspace-ownership.md) | Explicit wire maps, pure Core operations and one interactive mutation authority; implementation qualification in progress | [Core](../CORE_DEFINITION_CONTRACT.md), [Presentation](../PRESENTATION_STATE.md), [Operations](../OPERATIONS.md) |
| [Definition runtime boundary](ADR-definition-runtime-boundary.md) | Bounded data, native protection, canonical identity and target independence; original public-only Browser paragraphs are explicitly superseded by the Browser trust amendment | [Core](../CORE_DEFINITION_CONTRACT.md), [launch](../LAUNCH_CONTRACT.md), [protected artifacts](../PROTECTED_ARTIFACTS.md) |
| [Browser session trust and export](ADR-browser-session-trust-and-export.md) | Accepted design for static WASM, bounded sources, transient consented custody and deliberate export | [Browser custody procedure/evidence](../OPERATIONS.md#browser-custody-and-offline-verification) |
| [Static Browser Coolify container image and permanent SecretKit boundary](ADR-browser-coolify-static-image.md) | Accepted and implemented (`2026-11-23`): single unified embedded-Store container, allowlist-preserving HTTPS Brotli/Gzip (`EnableForHttps = true`), curl-free loopback `--healthcheck`, `Dockerfile.dockerignore` & zero-SecretKit architecture guards, exact-digest rollback; permanently excludes SecretKit and connected target APIs | [Browser v1 qualification](../releases/browser-v1-qualification.md), [Coolify rollback](../OPERATIONS.md#coolify-static-deployment-and-rollback), [Packaging](../PACKAGING.md#browser-container) |
| [Self-hosted connected Browser and public static boundary](ADR-self-hosted-connected-browser-and-public-static-boundary.md) | Accepted 2026-11-16 deployment direction (2026-11-27 anti-conditioning rationale addendum): permanent ISLAMU public/static target exclusion; separate future operator-owned connected host after native pilots. **No connected host, connector API, provider support or target apply implemented** | [Current decision](ADR-self-hosted-connected-browser-and-public-static-boundary.md#decision), [trust & anti-conditioning rationale](../TRUST_BOUNDARY_AND_ANTI_CONDITIONING.md), [launch contract](../LAUNCH_CONTRACT.md), [packaging](../PACKAGING.md#browser-container) |
| [Maintainer authoring and distribution](ADR-maintainer-authoring-and-canonical-distribution.md) | Accepted one-source compiler and canonical distribution; historical vocabulary does not create a supported v1 interpreter | [Maintainer contract](../MAINTAINER_AUTHORING.md), [v2 final delivery](../OPERATIONS.md#final-definition-delivery-and-responsibility) |
| [Optional public Store](ADR-optional-definition-store.md) | Accepted optional in-repository discovery; authoring amendment replaces original metadata input | [Store contract](../DEFINITION_STORE.md), [package status](../STATUS.md#package-admission) |
| [Terminal editing and publication lifetimes](ADR-terminal-editing-and-publication-lifetimes.md) | Accepted shared editing, operation ownership and publication-history boundaries; native qualification separate | [Terminal contract](../TERMINAL_CLIENT.md), [executed surfaces](../STATUS.md#executed-surface-and-host) |
| [Terminal framework and workbench](ADR-terminal-framework-and-workbench.md) | Official Terminal.Gui retained after comparison with SharpConsoleUI; sustained workbench direction, layout/editor/provider delivery and qualification kept separate | [Terminal contract](../TERMINAL_CLIENT.md), [dependency review/history](../TERMINAL_GUI_DEPENDENCY.md), [product direction](../VISION_AND_APPROACH.md) |
| [CLI command runtime](ADR-cli-command-runtime.md) | Accepted runtime boundary; historical v1-to-v2 command wording is not today's machine version | [Current CLI contract](../CLI_CONTRACT.md), [implementation status](../STATUS.md#implemented-scope) |
| [Form computation](ADR-browser-form-computation.md) | Accepted revision-owned computation and bounded rendering; named reference-host gates recorded | [Frozen candidate evidence](../OPERATIONS.md#frozen-indexed-edit-candidate-2026-10-23), [later AOT candidate](../OPERATIONS.md#committed-aot-editor-candidate-2026-10-24) |
| [Workspace presentation](ADR-browser-workspace-presentation.md) | Accepted thin controls, shared disclosure and contextual help; runtime qualification separate | [Browser UI](../BROWSER_UI.md), [Browser evidence](../OPERATIONS.md#browser-custody-and-offline-verification) |
| [Cross-platform quality ownership](ADR-cross-platform-quality-ownership.md) | Accepted native lifetimes plus appended cross-platform ownership; retained native checkpoint is not the full program gate | [Native checkpoint](../OPERATIONS.md#native-ownership-qualification-2026-10-27), [program qualification](../OPERATIONS.md#cross-platform-quality-program-qualification-2026-10-27) |
| [Single-pass preparation](ADR-single-pass-preparation-ownership.md) | Accepted composition and staged adoption ownership; not broad preparation responsiveness qualification | [Program evidence and limits](../OPERATIONS.md#cross-platform-quality-program-qualification-2026-10-27) |
| [Bounded definition v2](ADR-definition-standard-v2.md) | Accepted D01-D12; Core v2 delivered, historical authoring defaults/vocabulary do not establish another interpreter | [Conformance profile](../DEFINITION_CONFORMANCE_PROFILE.md), [v2 qualification](../OPERATIONS.md#definition-v2-qualification) |
| [Evidence-aware import/comparison](ADR-artifact-import-comparison.md) | Accepted design; implementation and qualification in progress | [Import/comparison contract](../IMPORT_COMPARISON.md); client/package qualification pending, earlier package evidence not inherited |
| [Public-scope document editing](ADR-browser-developer-editing.md) | Accepted in approved Browser simplification revision 3; one opaque candidate and accepted-state commit, not a dependency approval | [Shared document contract](../PRESENTATION_STATE.md#bounded-document-sessions-and-atomic-apply), [Browser integration](../BROWSER_UI.md); runtime and assistive-technology qualification remain separate |
| [Deployment context and recipient destinations](ADR-deployment-context-and-destinations.md) | Accepted scope-before-requiredness architecture, atomic recipe/component choices and explicit destination dialects; implementation verification in progress | [Core contract](../CORE_DEFINITION_CONTRACT.md), [Presentation state](../PRESENTATION_STATE.md), [Browser custody](../LAUNCH_CONTRACT.md) |
| [Secret-store integration and credential sources](ADR-secret-store-integration-and-credential-sources.md) | Accepted 2026-11-14, Browser scope amended 2026-11-16: SecretKit only for secret-store operations; vendor CLIs only as explicit opt-in credential sources; Azure bring-your-own client ID; Infisical Universal Auth first with OIDC as expansion. Public/static Browser exclusion remains; a separate operator-owned connected host is the future exception after native pilots. Nothing implemented; target apply still deferred | [Current decision](ADR-secret-store-integration-and-credential-sources.md#current-decision), [hosting amendment](ADR-self-hosted-connected-browser-and-public-static-boundary.md), [Quick reference](../QUICK_REFERENCE.md) |

## Proposed records

| Record | Explicit recorded disposition | Reading boundary |
| --- | --- | --- |
| [Browser answer intent and denial](ADR-browser-answer-intent-and-denial.md) | Proposed; design refined, not implemented or I-VSD-revalidated according to this record | Related later implementation narratives do not silently change this record's acceptance status |
| [Configuration experience and answer authority](ADR-configuration-experience-and-answer-authority.md) | Proposed planning decision, not implemented or production-qualified according to this record | Use [workspace ADR](ADR-browser-workspace-presentation.md) and current contracts for their narrower accepted scope; do not infer blanket acceptance |
| [Guided Browser workspace ownership](ADR-browser-guided-workspace-ownership.md) | Original record remains proposed; the later approved implementation plan has delivered related guided-workspace boundaries | This index does not silently promote the record; use current [execution evidence](../reports/BROWSER_UX_SIMPLIFICATION_EXECUTION.md) for delivery. Developer editing remains in the same implementation plan |

## Using historical decision text

Read amendments and successor contracts before copying version numbers or
surface descriptions. Accepted rationale may survive while an earlier wire
version, original Browser restriction or owner name is historical. A proposed
record remains proposed until its disposition is explicitly resolved; a later
test total cannot resolve that decision. [Knowledge map](../KNOWLEDGE_MAP.md)
connects these records to strategy without inventing missing approvals.
