---
name: blazor-ui-conventions
description: "Load for Configurator Blazor/MudBlazor components, dialogs, parameters/events, immutable workspace rendering, themes or component tests; not for Event HAL, generated API clients or BFF hosting."
type: pattern
enforcement: suggest
priority: high
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- ABOUTME: Blazor UI conventions for Razor components, MudBlazor v9 usage, render modes, HAL-driven affordances, and shared design-system wrappers. -->
<!-- ABOUTME: Keeps Explore.Blazor and Explore.Blazor.Client aligned with InteractiveAuto, wrapper components, immutable state flows, and BFF-safe UI behavior. -->

## Resources
- Blazor architecture: `docs/internal/BLAZOR.md` (Event source, historical only) — hosting, service, generated-client, and component boundaries.
- Record contracts: `docs/internal/RECORD_CONTRACTS.md` (Event source, historical only) — generated record eligibility, initialization, `with`, AOT extension data, and diagnostic privacy.
- [Design system](../design-system/SKILL.md) — load for tokens or shared wrappers; `docs/internal/DESIGN_SYSTEM.md` is an Event source reference, historical only.
- [Accessibility](../accessibility/SKILL.md) — load only when forms, dialogs, focus, keyboard, landmarks, ARIA, or contrast are touched; `docs/internal/ACCESSIBILITY.md` is an Event source reference, historical only.

## Rules

- Configurator Browser is static WASM over browser-safe Presentation/Core.
  Follow the browser path rule and launch/state contracts; do not add
  InteractiveAuto, target authentication, HAL authority or a BFF.
- Use `[Parameter]`/`EventCallback` with immutable workspace snapshots.
  Operation generations fence cancelled, replaced or edited asynchronous work.
- Use existing shared wrappers and current APIs; inspect rendered behavior,
  keyboard/focus/status and narrow/wide layouts for visible changes.
- Select the affected Browser/Presentation checks in `PROJECTS.md`.
  Prose-only changes use links and metadata, not a product build.

## Historical Event UI Contracts

The remaining rules, workflow, commands and related backend skills are retained
for Event reference only. Linked render-mode, generated-client, HAL, BFF and
record resources describe that source system, not Configurator requirements.

- `InteractiveAuto` is the default. Client-capable paths do not use `HttpContext`, raw access tokens, or direct component-to-API calls; they use generated clients through the established BFF/service boundary.
- HAL `_links` is the sole authority for resource action affordances. Do not infer Edit/Delete or other resource actions from roles, claims, source type, or local state.
- Generated response/value records use object initializers and `with` copies. Do not add mutable compatibility setters or hand-edit `EventApiClient.g.cs`.
- Protocol inputs, HAL resources, inherited shapes, PATCH/update DTOs, files, exceptions, and the exact mutable UI/service manifest remain classes. Adding or removing a mutable-manifest entry requires a consuming behavior test.
- `[JsonExtensionData] AdditionalProperties` remains settable for System.Text.Json AOT. Unknown HAL extension data must round-trip through `AppJsonSerializerContext`.
- Generated record diagnostics are value-free, but whole DTO/record logging remains forbidden; log bounded approved scalars.
- Child-to-parent communication uses `[Parameter]` and `EventCallback`; immutable ranges, dates, and generated records are replaced rather than mutated.
- Use MudBlazor v9 APIs and the shared three-tier token/wrapper system instead of removed APIs or ad hoc styling.

## Workflow

1. Subscribe to the exact component/service behavior before triggering it; do not use timing waits.
2. When a generated contract changes, regenerate through MSBuild, migrate consumers to initialization/`with`, and preserve HAL/PATCH/AOT behavior.
3. Run the component/service slice, generated serialization tests when applicable, then visual QA for rendered changes.

## Verification
- `dotnet test --project tests/Event.Architecture.Tests/Event.Architecture.Tests.csproj --configuration Release --verbosity quiet -- --treenode-filter "/*/*/BlazorClientArchitectureTests/*" --minimum-expected-tests 1 --no-progress --maximum-parallel-tests 1`
- `dotnet test --project tests/Explore.Blazor.Client.Tests/Explore.Blazor.Client.Tests.csproj --configuration Release --verbosity quiet`
- `dotnet test --project tests/Explore.Blazor.IntegrationTests/Explore.Blazor.IntegrationTests.csproj --configuration Release --verbosity quiet`

## Related
- [../blazor-bff-patterns/SKILL.md](../blazor-bff-patterns/SKILL.md)
- [../blazor-css-isolation/SKILL.md](../blazor-css-isolation/SKILL.md)
- [../design-system/SKILL.md](../design-system/SKILL.md)
- [../auth-patterns/SKILL.md](../auth-patterns/SKILL.md)
