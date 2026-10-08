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
related_intents: [client-interface]
---

# Blazor Client Rules

Twin files at `.agents/rules/blazor-client.md` and `.omo/rules/blazor-client.md` must remain byte-identical.

## Applies To
- `src/Configurator.Browser/**/*.cs`, `**/*.razor`, `**/*.css`.

## Path-Specific Constraints
- **Component Parameters**: Auto-properties only (`public string Value { get; set; } = string.Empty;`). Never mutate or set component parameters imperatively via `@ref`.
- **Parameter State**: Manage updates through the component lifecycle and callbacks. Use `ParameterState<T>` only when an existing owning component supports it; do not introduce a MudBlazor dependency to satisfy historical guidance.
- **Theming & CssBuilder**: Use established class composition, scoped CSS (`.razor.css`) and theme variables. Never hard-code inline style attributes or colors; use `CssBuilder` where the actual component library supplies it.
- **Public Boundary**: Static public-only Blazor WebAssembly accepts registered bounded public metadata. No secret entry, private preview, private persistence, cookies, service worker or target authentication. Public metadata is not host apply/admin authority.
- **Render Mode**: Preserve static WebAssembly hosting. No `InteractiveAuto`, SSR/BFF assumptions, Event generated API client or HAL affordance requirements.
- **Component APIs**: Use installed component APIs and established wrappers, not hypothetical Event/MudBlazor contracts.
- **CSS Isolation (BEM)**: Component-specific styling lives in matching `.razor.css`; use BEM names for scoped classes.
- **Deep Selectors**: `::deep` is a last resort for third-party overrides, not a substitute for component ownership.
- **Accessibility**: Structural semantics (headings, focus, labels) take precedence over visual shortcuts. Preserve accessible keyboard, focus, status announcements and RTL/logical styling.
- **Record-Owned State**: Follow [GOVERNANCE.md](../../docs/internal/GOVERNANCE.md). Published immutable snapshots use records where value semantics fit; reasoned mutable component/edit/service state remains classes.
- **Launch Ownership**: Resolve registered definitions only; pin family/revision/digest. Definition text and filename hints never supply arbitrary remote launch URLs or authority.
- **Published Collections**: Snapshot caller-owned lists, dictionaries, sets and bytes; expose immutable/read-only members and preserve component rerender semantics. Browser references Core only, never native Artifacts/Presentation or sibling Event assemblies.

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
- Intent: `client-interface`
- Rules: [Tests](tests.md), [Privacy](privacy-and-pii.md)
