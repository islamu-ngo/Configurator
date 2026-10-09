---
name: blazor-client
description: Apply when editing Configurator.Browser components, styles, or UI services.
paths:
  - "src/Configurator.Browser/**/*.razor"
  - "src/Configurator.Browser/**/*.cs"
  - "src/Configurator.Browser/**/*.css"
related_skills: [blazor-ui-conventions, blazor-css-isolation, accessibility, design-system]
related_docs: [AGENTS.md, PROJECTS.md, docs/internal/GOVERNANCE.md, docs/internal/QUICK_REFERENCE.md, docs/internal/LAUNCH_CONTRACT.md]
minimum_tests: [Configurator.Browser.Tests]
related_intents: [client-interface, browser-session-privacy]
---

# Blazor Client Rules

Twin files at `.agents/rules/blazor-client.md` and `.omo/rules/blazor-client.md` must remain byte-identical.

## Applies To
- `src/Configurator.Browser/**/*.cs`, `**/*.razor`, `**/*.css`.

## Path-Specific Constraints
- **Component Parameters**: Auto-properties only (`public string Value { get; set; } = string.Empty;`). Never mutate or set component parameters imperatively via `@ref`.
- **Parameter State**: Manage updates through the component lifecycle and callbacks. Use `ParameterState<T>` only when an existing owning component supports it; do not introduce a MudBlazor dependency to satisfy historical guidance.
- **Theming & CssBuilder**: Use established class composition, scoped CSS (`.razor.css`) and theme variables. Never hard-code inline style attributes or colors; use `CssBuilder` where the actual component library supplies it.
- **Browser Custody**: Static Blazor WebAssembly keeps all answers transient. Secret entry requires explicit per-workspace consent; revocation clears secret leaves and output. Deliberate restricted copy/download does not promise native file protection. Public caches contain reviewed app assets and explicitly retained admitted definitions only, never answers/artifacts. No cookies, telemetry, target authentication or answer-bearing requests. Public metadata is not host apply/admin authority.
- **Render Mode**: Preserve static WebAssembly hosting. No `InteractiveAuto`, SSR/BFF assumptions, Event generated API client or HAL affordance requirements.
- **Component APIs**: Use installed component APIs and established wrappers, not hypothetical Event/MudBlazor contracts.
- **CSS Isolation (BEM)**: Component-specific styling lives in matching `.razor.css`; use BEM names for scoped classes.
- **Deep Selectors**: `::deep` is a last resort for third-party overrides, not a substitute for component ownership.
- **Accessibility**: Structural semantics (headings, focus, labels) take precedence over visual shortcuts. Preserve accessible keyboard, focus, status announcements and RTL/logical styling.
- **Record-Owned State**: Follow [GOVERNANCE.md](../../docs/internal/GOVERNANCE.md). Published immutable snapshots use records where value semantics fit; reasoned mutable component/edit/service state remains classes.
- **Launch Ownership**: Resolve bundled or bounded public credential-free HTTPS/local-file definitions before answer collection; pin family/revision/digest. Remote sources require exact digest/selector checks, bounded streaming and cancellation fencing. Definition text and filename hints never supply arbitrary launch or target authority.
- **Published Collections**: Snapshot caller-owned lists, dictionaries, sets and bytes; expose immutable/read-only members and preserve component rerender semantics. Browser references browser-safe Presentation/Core, never native Artifacts or sibling Event assemblies.
- **Draft Truthfulness**: Complete composition remains strict. Secret-deferred preparation labels unresolved paths and does not defer malformed values or ordinary missing fields; it never implies target acceptance.
- **Offline Lifecycle**: Published PWA caches exclude answers/output. No source fetch during live answers or offline work; no activation/reload may replace a live workspace in any tab. Platform QA must inspect actual published caches, requests and update behavior separately from unit tests.

## Must Read
- [Quick Reference](../../docs/internal/QUICK_REFERENCE.md)
- [Governance](../../docs/internal/GOVERNANCE.md)
- [Launch Contract](../../docs/internal/LAUNCH_CONTRACT.md)
- [Browser UI conventions](../../.agents/skills/blazor-ui-conventions/SKILL.md) and [Accessibility](../../.agents/skills/accessibility/SKILL.md), with absent Event layers remaining reference-only.

## Verification
- `dotnet build src/Configurator.Browser/Configurator.Browser.csproj --configuration Release --verbosity quiet`
- `dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release`
- Inspect rendered desktop/mobile semantics and keyboard/focus behavior for changed UI. Run architecture/package closure checks only if dependency or packaging boundaries changed, per `PROJECTS.md`.

## Related
- Intents: `client-interface`, `browser-session-privacy`
- Rules: [Tests](tests.md), [Privacy](privacy-and-pii.md)
