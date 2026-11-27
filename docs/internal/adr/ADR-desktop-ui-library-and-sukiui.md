# ADR: Desktop UI library selection (SukiUI) and optional-utility pragmatism

> **Audience:** Contributors | Maintainers | AI agents
> **Kind:** Accepted decision
> **Status:** Accepted 2026-11-23 product-owner direction; implementation, dependency admission and qualification remain planned (Desktop currently runs `Avalonia.Themes.Fluent` 12.1.6)
> **Owner:** Desktop
> **Last Verified:** 2026-11-23
> **Source Anchors:** `Directory.Packages.props`, `src/Configurator.Desktop/App.axaml`, `src/Configurator.Desktop/Resources/DesktopTheme.cs`, `src/Configurator.Desktop/Resources/DesktopDensity.cs`, `docs/internal/DESKTOP_CLIENT.md`, `docs/internal/PRODUCT_VISION.md`
> **Review basis:** Product-owner decision on 2026-11-23 informed by source-free public ecosystem evaluation; no dependency, lockfile, code or runtime qualification changed in this documentation record.

Date: 2026-11-23  
Decision authority: Product Steward / Owner.  
Implementation status: **Decision and documentation only.** `Configurator.Desktop` currently builds on `Avalonia 12.1.6` and `Avalonia.Themes.Fluent 12.1.6`. No `SukiUI` package reference, lockfile entry, notice update or code migration exists yet. Dependency admission (pinned version, restored lockfile closure, license verification and `THIRD-PARTY-NOTICES.md` / `docs/internal/legal/dependencies/configurator-ui.md` updates) and implementation planning remain separate next steps.

## Context

`Configurator.Browser` uses `MudBlazor` (MIT) as a full-featured component library so Browser UI work can delegate control behavior, dialogs, drawers, notifications and visual polish instead of hand-building every widget. `Configurator.Desktop` currently uses Avalonia's baseline `Avalonia.Themes.Fluent` (`12.1.6`) with a local `DesignTokens.axaml` / `ThemePalettes.axaml` / `DesktopTheme` / `DesktopDensity` override layer. That baseline is functional, but delivering a modern, cohesive desktop UI/UX by hand-styling raw Fluent primitives creates ongoing UI maintenance overhead.

Two strategic realities govern how ISLAMU chooses a Desktop UI component library:

1. **Non-profit full-open-source alignment:** ISLAMU develops open-source software as a non-profit organization. Just as `MudBlazor` was selected for Blazor because it is 100% open source under MIT with no commercial superset competing against its MIT core, `Configurator.Desktop` requires a component library whose long-term incentives are aligned with community MIT open source rather than an open-core commercial tier.
2. **Product role and maintenance pragmatism:** **ISLAMU Event** is ISLAMU's flagship product; **ISLAMU Configurator** is a supporting, optional quality-of-life utility. Any operator can configure target application files manually without Configurator. Desktop UI engineering must therefore favor **delegating as much UI, theming, chrome and component work as possible** to the chosen library so maintenance stays lean and does not drain bandwidth from ISLAMU Event or Configurator's core safety guarantees.

## Decision

1. **Adopt `SukiUI` (`github.com/kikipoulet/SukiUI`, MIT, Avalonia 12 `7.x` line)** as the planned UI component and theme library for `Configurator.Desktop`.
2. **Delegate to SukiUI and adapt Desktop around it:** Strongly integrate `Configurator.Desktop` with SukiUI's controls, dialogs, notifications, navigation patterns, theming and spacing rather than fighting SukiUI's templates or recreating `Configurator.Browser`'s MudBlazor implementation on Desktop. Desktop is allowed to look and behave like a SukiUI desktop application rather than a 1:1 clone of Browser.
3. **Contribute upstream where helpful:** Where SukiUI lacks a useful capability or benefits from refinement (for example, tighter density options or theme polish), prefer contributing clean upstream pull requests to `kikipoulet/SukiUI` over maintaining complex local template workarounds inside Configurator.
4. **Separate non-negotiable safety invariants from optional UI bonuses:** Never compromise security, secret custody, canonical definition identity or protected native output, while explicitly treating Desktop RTL, strict automated WCAG contrast clamping, custom multi-preset surface-tinting parity and 4-step Browser density parity as **optional bonuses** rather than blocking requirements.

## Non-negotiable safety invariants vs. optional UI bonuses

Future plans, implementations and reviews for `Configurator.Desktop` must respect this explicit priority boundary:

| Category | Scope | Policy under SukiUI adoption |
| --- | --- | --- |
| **Non-negotiable (never compromised)** | **Secret custody & masking:** masked secret entry, deliberate reveal/copy, transient derivation handling, buffer clearing boundaries, zero secret leakage in diagnostics, status text or logs | Mandatory in every SukiUI view, dialog, toast and inspector |
| **Non-negotiable (never compromised)** | **Data-only definition admission & canonical identity:** bounded 4 MiB intake, family/revision/SHA-256 pinning, zero executable plugins, fail-closed public HTTPS fetch policy | Mandatory; UI library never alters Core admission or identity |
| **Non-negotiable (never compromised)** | **Shared state & lifetime safety:** `Configurator.Presentation` owns immutable snapshots, input revisions, `OperationGeneration` stale-result fencing, cooperative cancellation and physical close/settlement receipts | Mandatory; SukiUI views remain thin adapters over Presentation |
| **Non-negotiable (never compromised)** | **Protected native publication & target authority:** `Configurator.Artifacts` create-only restricted writes (`0600` on Linux), no ordinary-file fallback on unsupported hosts, factual publication receipts, target retains apply/admin authority | Mandatory; SukiUI dialogs/toasts never replace Artifacts write guards |
| **Optional / bonus (adapt to SukiUI)** | **Right-to-Left (RTL) layout** (`FlowDirection`) | Welcome bonus where SukiUI supports it cleanly; not a blocker for adopting SukiUI controls or layouts |
| **Optional / bonus (adapt to SukiUI)** | **Automated WCAG contrast clamping & custom Light/Dark High Contrast modes** | Welcome bonus if cleanly supported; do not maintain heavy custom brush-clamping overrides or fight SukiUI themes to force synthetic contrast ratios |
| **Optional / bonus (adapt to SukiUI)** | **Browser theme-engine parity** (8 Event presets + custom Natural/Brand HSL surface tinting) | Delegate Desktop theming to SukiUI's theme/accent system rather than forcing custom surface-palette mutation across SukiUI internals |
| **Optional / bonus (adapt to SukiUI)** | **Browser 4-step density parity** (`Spacious` through `26px Dense`) | Adapt Desktop compactness naturally around SukiUI's control geometry and built-in compact patterns (plus optional upstream SukiUI PRs) instead of fighting hardcoded templates |

> [!IMPORTANT]
> Relaxing optional UI parity and accessibility-clamping requirements on Desktop
> never relaxes any secret-custody, definition-admission, generation-fencing,
> protected-save or target-authority invariant. Safety remains non-negotiable
> because Configurator handles live credentials and deployment secrets.

## Evaluated alternatives and rationale

Public ecosystem evaluation conducted on **2026-11-23** against Avalonia 12 compatibility, outbound MIT compatibility, open-source incentive alignment, visual quality and maintenance ergonomics:

| Option | License & model | Avalonia 12 | Disposition | Rationale |
| --- | --- | --- | --- | --- |
| **`SukiUI`** (`kikipoulet/SukiUI`) | **MIT** (100% open-source community ecosystem across core and companions) | Yes (`7.0.1+`) | **Accepted (planned migration)** | Matches ISLAMU's full-MIT open-source values (parallel to `MudBlazor` on Blazor), provides the desired modern desktop UI/UX, has active maintenance, multiple community contributors and practical maturity, and accepts substantial upstream PRs (including RTL and Avalonia 12 support). Tradeoffs—less built-in density/token granularity than open-core enterprise suites and a smaller maintainer core—are explicitly accepted by adapting Desktop to SukiUI and contributing upstream where useful. |
| **`Semi.Avalonia` + `Irihi.Ursa`** (`irihitech`) | **Hybrid / Open-Core:** core `Semi` & `Ursa` are MIT (`.NET Foundation` backed), while IRIHI sells a commercial suite including **`Asura`** (*flagship commercial control library*) and **`Hydra`**, plus closed-source free NuGet adapters (`Semi.Avalonia.Dock`, `Semi.Avalonia.Tabalonia`, `Semi.Avalonia.AvaloniaEdit`) | Yes (`Semi 12.1.x`, `Ursa 2.3.x`) | **Rejected** | Technically mature and complete in its MIT core today, but having two desktop control libraries under one commercial vendor—one MIT (`Ursa`) and one flagship commercial (`Asura`)—creates a structural open-core incentive conflict that does not align with ISLAMU's pure open-source non-profit priority. Separate commercial niche tools (`Mantra`, `Huska`) were not the objection; the overlapping flagship commercial control library (`Asura`) and closed-source theme adapters were decisive. |
| **`FluentAvaloniaUI`** (`amwx/FluentAvalonia`) | **MIT** | Yes (`3.x`) | **Not selected** | Mature MIT WinUI 3 port, but strongly Windows 11 flavored and not the desired visual aesthetic for Configurator Desktop. |
| **Keep `Avalonia.Themes.Fluent` + bespoke custom `ControlTheme` layer** | **MIT** (official Avalonia core) | Native (`12.1.6`) | **Current baseline; planned to supersede** | Functional today, but requires ISLAMU to design, style and maintain desktop chrome, dialogs, toasts, badges, transitions and polish in-house—poor maintenance leverage for a supporting optional utility. |
| **`ShadUI` (`accntech/shad-ui`), `Huskui.Avalonia`, `Flowery.NET`, `Material.Avalonia`** | MIT / OSS (verify per package) | Mixed / emerging | **Not selected** | Earlier pre-1.0 maturity, smaller contributor ecosystems or dated Material 2 styling compared to SukiUI. |
| **`Montago/NocturneTheme`** | **No declared OSS license** on GitHub | Unestablished (~9 stars, WPF-first) | **Rejected** | Missing open-source license blocks MIT distribution under [IP Governance](../legal/IP_GOVERNANCE.md); minimal Avalonia ecosystem footprint. |
| **`AtomUI`** (Ant Design) | **LGPL v3** / commercial | Yes (`12.x`) | **Rejected** | Copyleft relinking/modification obligations conflict with frictionless MIT distribution. |
| **`Actipro Avalonia` / `Eremex`** | **Proprietary commercial EULA** (including Actipro's free tier) | Yes | **Rejected** | Incompatible with ISLAMU's pure MIT open-source stack and self-buildable distribution model. |

## Relationship to existing Desktop records

- **[Desktop workspace composition](ADR-desktop-workspace-composition.md):** Remains accepted for semantic workspace ownership (`Configurator.Presentation` / `Configurator.Artifacts` boundaries, non-destructive stage switching, stable editor projection identity and generation fencing). Its sentence tying appearance to the custom native high-contrast owner is superseded for the planned SukiUI direction by this ADR.
- **[Desktop client contract](../DESKTOP_CLIENT.md) and [public Desktop guide](../../public/desktop.md):** Continue to document the currently shipped `Avalonia.Themes.Fluent 12.1.6` implementation accurately until the SukiUI implementation plan is authored and executed, with an explicit note linking this accepted planned direction.
- **Dependency and legal records (`docs/internal/legal/dependencies/configurator-ui.md`, `THIRD-PARTY-NOTICES.md`):** Remain unchanged by this prose decision. Adding `SukiUI` to `Directory.Packages.props` during implementation requires native NuGet restore, lockfile hash verification, transitive dependency/license validation (`validate-dependency-license-policy.cs`) and notice updates.

## Consequences and implementation prerequisites

When the separate SukiUI implementation plan is authored and executed:

1. **Dependency admission:** Pin an exact Avalonia 12-compatible `SukiUI` version in `Directory.Packages.props`, inspect its full resolved transitive closure (including any SkiaSharp/icon/motion packages), update `docs/internal/legal/dependencies/configurator-ui.md` and `THIRD-PARTY-NOTICES.md`, and pass `.ci/scripts/validate-dependency-license-policy.cs`.
2. **Maximum delegation & simplification:** Replace bespoke Fluent-override boilerplate (`DesignTokens.axaml`, `ThemePalettes.axaml`, custom WCAG contrast clamping in `DesktopTheme.cs`, and custom dialog/overlay scaffolding where SukiUI provides native equivalents like `SukiDialogHost` / `SukiToastHost`) with idiomatic SukiUI usage wherever practical.
3. **Runtime & test stability on Linux/Headless:** Verify `SukiUI` under `Configurator.Desktop.Tests` (`Avalonia.Headless`) and Linux X11/Wayland (`Configurator.Desktop.NativeHost` / Xvfb), keeping flat/non-shader or software-safe background configuration where needed so GPU-less Linux environments remain reliable.
4. **Proportional test updates:** Preserve every safety, secret-custody, validation, import/comparison, generation-fencing, close-settlement and create-only protected-save test unchanged. Update or retire only old Fluent-specific theme-palette/contrast assertions that tested the superseded bespoke theming layer, matching the new SukiUI contract cleanly.

## Provenance and independent authoring

This decision record was authored from public product/package metadata, license disclosures and repository-native architectural context. No third-party implementation source code, XAML templates, shaders, tests, prose or visual assets were copied.

### Source register (accessed 2026-11-23)

- SukiUI repository & license: `https://github.com/kikipoulet/SukiUI` (MIT, v7.0.1+ Avalonia 12 compatibility via PR #598, RTL support via PR #331).
- SukiUI NuGet package metadata: `https://www.nuget.org/packages/SukiUI`.
- IRIHI Technology open-source repos & suite licensing: `https://github.com/irihitech/Semi.Avalonia` (MIT core; closed-source notice for `Dock`, `Tabalonia`, `AvaloniaEdit`), `https://github.com/irihitech/Ursa.Avalonia` (MIT, .NET Foundation), `https://irihi.tech` (IRIHI Suite tiers including commercial `Mantra`, `Huska`, `Asura`, `Hydra`).
- FluentAvalonia repository & NuGet: `https://github.com/amwx/FluentAvalonia` (MIT, v3.x Avalonia 12).
- NocturneTheme repository: `https://github.com/Montago/NocturneTheme` (no declared OSS license file on GitHub as of access date).
- Additional ecosystem references verified for license/compatibility: `https://github.com/accntech/shad-ui` (ShadUI, MIT), `https://github.com/d3ara1n/Huskui.Avalonia` (Huskui.Avalonia, MIT), `https://github.com/AtomUI/AtomUI` (AtomUI, LGPL v3), `https://www.actiprosoftware.com` (Actipro Avalonia, proprietary EULA).
