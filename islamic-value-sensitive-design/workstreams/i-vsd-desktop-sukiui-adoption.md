# Avalonia Desktop SukiUI Adoption And Refactoring — Provider Responsibility Assessment

Last Updated: 2026-11-23

## Review Metadata
- Mode: planning
- Subject: ISLAMU Configurator Avalonia Desktop UI adoption of SukiUI and retirement of bespoke Fluent theme/density overrides
- Workstream: desktop-sukiui-adoption
- Report kind: planning assessment (revalidated against completed `desktop-sukiui-adoption` triad)
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-23
- Reviewed input: completed `desktop-sukiui-adoption` triad (`dev/active/desktop-sukiui-adoption/desktop-sukiui-adoption-{plan,tasks,context}.md`, 2026-11-23), working-tree Desktop source (`src/Configurator.Desktop/`), Desktop tests (`tests/Configurator.Desktop.Tests/`, `tests/Configurator.Desktop.NativeHost/`), license/notice tooling (`.ci/scripts/validate-dependency-license-policy.cs`, `.ci/scripts/write-dependency-notices.cs`, `eng/configurator/Configurator.Release.proj`), decision ADR (`docs/internal/adr/ADR-desktop-ui-library-and-sukiui.md`), Blazor dual-sidebar references (`src/Configurator.Browser/Layout/MainLayout.razor`, `NavMenu.razor`; `/home/amir/ISLAMU/Github/Event/src/Explore.Blazor.Client/Layout/MainLayout.razor`, `AppWorkspaceRail.razor`), local SukiUI documentation (`/home/amir/dev/Github/SukiUI/docs/docs/documentation/`), and NuGet flatcontainer metadata (`SukiUI 7.0.1` vs `7.0.2-nightly20261121.525`, `SukiUI.Motion 7.0.2-nightly20261121.525`, `Material.Icons.Avalonia 3.0.2`, `FluentIcons.Avalonia 2.1.345`, `Lucide.Avalonia 0.2.32`)
- Supersedes: none (does not retroactively rewrite `i-vsd-avalonia-desktop-ui-ux-redesign.md` or `i-vsd-configuration-experience-unification.md`; supersedes Desktop's Fluent-override direction going forward per `ADR-desktop-ui-library-and-sukiui.md`)

## Executive Summary

ISLAMU Configurator is a supporting, optional offline utility alongside ISLAMU's flagship open-source product, ISLAMU Event. Following the project steward's explicit decision recorded in [`ADR-desktop-ui-library-and-sukiui.md`](../../docs/internal/adr/ADR-desktop-ui-library-and-sukiui.md) and the Grill-Me intake choices (dual-sidebar `SukiWindow` + `SukiSideMenu` global shell with contextual `Configure` section rail, plus `Material.Icons.Avalonia 3.0.2`), `Configurator.Desktop` is migrating from bespoke Fluent theme overrides (`Avalonia.Themes.Fluent`, a 418-line manual WCAG/HSL brush engine, custom High Contrast variants, and a 4-step density scale) to **100% MIT-licensed `SukiUI`** (`7.0.2-nightly20261121.525` + `SukiUI.Motion`) so Desktop leverages an actively maintained component library and minimizes custom UI maintenance.

This revalidated assessment confirms that the completed 4-phase implementation plan triad maps every one of the five provider-controlled responsibilities to testable RFC 2119 scenarios, Red-before-Green invariant tasks, and explicit fail-closed gates: preserving strict secret custody across SukiUI dialogs, toasts, and input extensions (`IVSD-F001` -> `IVSD-M001`); resolving third-party package and embedded-asset license provenance cleanly by rejecting `SukiUI 7.0.1` and pinning the MIT-declared `7.0.2` modular build + `Material.Icons.Avalonia 3.0.2` with verified supplemental notices for `Roboto`, `Quicksand`, and Pictogrammers Material Design Icons (`IVSD-F002` -> `IVSD-M002`); truthfully scoping Settings UI, documentation, and NativeHost qualification receipts where Desktop intentionally relaxes WCAG High Contrast clamping, Desktop RTL parity, and custom HSL/density controls while signposting `Configurator.Browser` (`IVSD-F003` -> `IVSD-M003`); defaulting SukiUI's GPU shader background to non-animated `Flat` mode (`IVSD-F004` -> `IVSD-M004`); and preserving cooperative physical-settlement close, operation generations, layered `Escape` dismissal, and Linux `0600` create-only protected writes across the dual-sidebar `SukiWindow` migration (`IVSD-F005` -> `IVSD-M005`).

`current / plan-aligned` certifies **planning alignment only**: the proposed plan addresses every identified provider responsibility coherently. It is **not** implementation proof, restored-`.nupkg` binary clearance, runtime X11 verification, legal certification, or a religious-legal ruling.

## Scope

This assessment covers the planned `desktop-sukiui-adoption` workstream affecting:
- `src/Configurator.Desktop/` (package dependencies, `App.axaml`/`App.axaml.cs`, theme/resource layer, `MainWindow : SukiWindow`, all Desktop views, dialog/toast host services, and Desktop-only ViewModel appearance/shell plumbing);
- `Directory.Packages.props`, project lockfiles, `docs/internal/legal/dependencies/configurator-ui.md`, `THIRD-PARTY-NOTICES.md`, and supplemental dependency notices under `eng/configurator/dependency-notices/`;
- `tests/Configurator.Desktop.Tests/`, `tests/Configurator.Desktop.NativeHost/`, and `tests/Configurator.Architecture.Tests/` (guarding safety, custody, close settlement, dependency licensing, and shipped package closure while cleanly replacing retired Fluent/HighContrast/density assertions);
- Desktop operator and maintainer documentation (`docs/internal/DESKTOP_CLIENT.md`, `docs/public/desktop.md`, `docs/internal/STATUS.md`, `docs/internal/adr/ADR-desktop-ui-library-and-sukiui.md`).

Out of scope:
- `Configurator.Core`, `Configurator.Artifacts`, `Configurator.Cli`, `Configurator.Terminal`, and `Configurator.Browser` runtime behavior (shared `Configurator.Presentation.Theming` remains untouched because `Configurator.Browser` actively depends on it);
- Non-Linux Desktop protected-save host admission (Desktop releases remain bounded to verified `linux-x64` per `eng/configurator/Configurator.Release.proj:43`);
- Third-party commercial Avalonia libraries (explicitly rejected in `ADR-desktop-ui-library-and-sukiui.md`).

## Claim Boundary

This assessment evaluates maintainer-controlled software, UX, privacy, licensing, and truthful-disclosure decisions within `desktop-sukiui-adoption` based on inspected repository files, SukiUI public documentation, and NuGet package metadata as of 2026-11-23. It does **not** constitute legal advice, a formal license audit of uninspected transitive binaries, assistive-technology certification, runtime X11/GPU performance proof, or a religious-legal (fiqh) ruling.

## Findings

| ID | Summary | Lifecycle | Severity | Linked Mitigation |
| --- | --- | --- | --- | --- |
| `IVSD-F001` | Secret custody exposure risks across SukiUI hosts, dialogs, toasts, and input extensions | open | high | `IVSD-M001` |
| `IVSD-F002` | SukiUI NuGet metadata gaps (`7.0.1`) and embedded font provenance (`Roboto`, `Quicksand`) | open | high | `IVSD-M002` |
| `IVSD-F003` | Risk of stale or misleading Desktop RTL, High Contrast, and WCAG parity claims | open | medium | `IVSD-M003` |
| `IVSD-F004` | Continuous CPU/GPU overhead and Xvfb/headless instability if SukiUI animated shaders run by default | open | medium | `IVSD-M004` |
| `IVSD-F005` | Regression risk to physical-settlement close, stale-result fencing, and create-only protected writes during shell migration | open | high | `IVSD-M005` |

### IVSD-F001: Secret Custody Exposure Risks Across SukiUI Hosts, Dialogs, Toasts, And Input Extensions

- **Lifecycle:** open
- **Severity / Claim Type:** high / source-supported security and privacy boundary risk
- **Principles / Domains:** Amanah (Trust), Non-Harm (Adha), Privacy / security, technical, design
- **Stakeholders:** self-hosting operators entering database passwords, API tokens, or signing keys; operators working in shared or screen-recorded environments
- **Provider-Controlled Decision:** how `Configurator.Desktop` integrates `SukiDialogHost`, `SukiToastHost`, `SecretGeneratorDialog`, `SecretActionsView`, `ProfileWorkspaceView`, and SukiUI `TextBoxExtensions`
- **Evidence IDs / Validation Level:** direct source inspection (`src/Configurator.Desktop/Views/SecretGeneratorDialog.axaml.cs:10-64`, `src/Configurator.Desktop/Views/FieldTemplates.axaml`, `src/Configurator.Desktop/Views/SecretActionsView.axaml`, `src/Configurator.Desktop/Views/ProfileWorkspaceView.axaml`, `docs/internal/QUICK_REFERENCE.md`) and SukiUI docs (`hosts/dialog.md`, `hosts/toast.md`, `controls/inputs/textbox.md`)
- **Linked Mitigation:** `IVSD-M001`
- **Owner / Next Validation:** Desktop UI & Security maintainer; Red-before-Green Headless custody/masking/redaction tests and `D21-secret-generation-validation` / `D29-private-metadata-discovery` / `D31-profile-library-custody`
- **Escalation Boundary:** OS clipboard manager history and external screen-capture tools remain outside application custody and stay documented honestly

Self-hosting operators trust Configurator Desktop with live production secrets such as database credentials, encryption passphrases, and private keys. Introducing SukiUI introduces new UI primitives — `ISukiToastManager` notifications, `ISukiDialogManager` overlay dialogs inside `SukiWindow.Hosts`, and `TextBoxExtensions` (such as clear buttons) — that can inadvertently break secret custody if wired naively.

Specifically, queueing toast notifications containing raw validation messages, file paths, or copied values would flash sensitive data across the window corner; hosting the secret generator inside a shared `SukiDialogHost` with `.Dismiss().ByClickingBackground()` could bypass deterministic `OnClosed` cancellation and secret-buffer clearing or leave stale secret ViewModels attached to host content; and applying unreviewed input extensions to masked secret boxes could interfere with `PasswordChar` masking or reveal/copy semantics.

As maintainers, we control the exact dialog, toast, and input boundary. Keeping `SecretGeneratorDialog` as an isolated modal `SukiWindow` preserves OS modal focus, `Escape` cancellation, and guaranteed `OnClosed` clearing, while restricting `SukiDialogHost` to non-secret informational dialogs (such as Store Project Details and Keyboard Shortcuts) and enforcing value-free, path-safe strings on every `SukiToastManager` notification eliminates accidental visual leakage. Evidence is currently source- and documentation-level; runtime verification occurs in implementation.

### IVSD-F002: SukiUI NuGet Metadata Gaps (`7.0.1`) And Embedded Font Provenance (`Roboto`, `Quicksand`)

- **Lifecycle:** open
- **Severity / Claim Type:** high / verified package-metadata and IP-compliance constraint
- **Principles / Domains:** Sidq (Truthfulness), Wafa bil-'Ahd (Promise-Keeping), IP Integrity / legal, governance, supply chain
- **Stakeholders:** downstream MIT redistributors, upstream open-source authors (`kikipoulet`, Google/Roboto, Andrew Paglinawan/Quicksand, Avalonia contributors, icon authors), project steward
- **Provider-Controlled Decision:** exact pinned SukiUI NuGet version, optional icon package choice, supplemental dependency notice maintenance, and shipped Desktop package closure
- **Evidence IDs / Validation Level:** direct inspection of `.ci/scripts/validate-dependency-license-policy.cs:53-68`, `.ci/scripts/write-dependency-notices.cs:125-164`, `docs/internal/legal/dependencies/configurator-ui.md`, `tests/Configurator.Architecture.Tests/StandalonePackageTests.cs:33-34`, NuGet flatcontainer `.nuspec` responses (`SukiUI 7.0.1`, `SukiUI 7.0.2-nightly20261121.525`, `SukiUI.Motion 7.0.2-nightly20261121.525`), and local SukiUI package asset list (`Roboto-*.ttf`, `CustomFont/Quicksand-*.ttf`)
- **Linked Mitigation:** `IVSD-M002`
- **Owner / Next Validation:** Dependencies & Legal Governance maintainer; Phase 1 `dotnet run .ci/scripts/validate-dependency-license-policy.cs`, `DependencyNoticeTests`, `PackageProvenanceTests`, and `StandalonePackageTests`
- **Escalation Boundary:** Formal legal interpretation of mixed third-party font/icon licenses belongs to legal counsel if an unexpected non-permissive asset appears in the restored `.nupkg`; any non-permissive asset is an immediate fail-closed blocker

Honest open-source stewardship requires honoring every upstream license condition without silently swallowing third-party attribution or weakening repository license gates. Live inspection of NuGet metadata revealed two concrete traps: first, `SukiUI 7.0.1` omits a `<license>` declaration in its `.nuspec` (causing `.ci/scripts/validate-dependency-license-policy.cs` to fail closed) and drags in `System.Linq.Dynamic.Core` (dynamic C# expression compilation) plus unused `DataGrid` and `ColorPicker` dependencies. By contrast, `SukiUI 7.0.2-nightly20261121.525` declares `<license type="expression">MIT</license>` cleanly, depends only on `SukiUI.Motion` (MIT) and Avalonia 12 core packages (MIT), and aligns with the inspected SukiUI documentation.

Second, `SukiUI`'s project embeds font files (`Roboto-Regular.ttf`, `Roboto-Medium.ttf` under Apache-2.0 and `CustomFont/Quicksand-*.ttf` under SIL OFL 1.1) directly as Avalonia resources. Treating `SukiUI`'s top-level MIT package tag as sufficient without preserving the embedded font licenses would omit required third-party notices, repeating the exact omission class already guarded for `TextMateSharp.Grammars` via `eng/configurator/dependency-notices/`.

Because the exact `.nupkg` contents have not yet been restored into Configurator's lock graph during planning, Phase 1 must inspect the restored `.nupkg` payload directly, verify every embedded font and icon license, add matching supplemental notices under `eng/configurator/dependency-notices/`, update `docs/internal/legal/dependencies/configurator-ui.md` and `THIRD-PARTY-NOTICES.md`, and keep test-only `Avalonia.Headless` and `Avalonia.Fonts.Inter` excluded from shipped packages.

### IVSD-F003: Risk Of Stale Or Misleading Desktop RTL, High Contrast, And WCAG Parity Claims

- **Lifecycle:** open
- **Severity / Claim Type:** medium / source-supported truthful-communication and fairness gap
- **Principles / Domains:** Sidq (Truthfulness), 'Adl (Justice), Avoiding Deception / communication, product, accessibility
- **Stakeholders:** Arabic RTL operators, low-vision / high-contrast desktop operators, documentation readers choosing between Browser and Desktop clients
- **Provider-Controlled Decision:** honest retirement of bespoke Desktop High Contrast / HSL / density controls, clear documentation pointing RTL/high-contrast users to `Configurator.Browser` (or CLI/manual editing), and truthful NativeHost qualification check names/metadata
- **Evidence IDs / Validation Level:** direct inspection (`docs/internal/adr/ADR-desktop-ui-library-and-sukiui.md`, `docs/internal/DESKTOP_CLIENT.md`, `docs/public/desktop.md`, `docs/internal/STATUS.md:45-62`, `tests/Configurator.Desktop.NativeHost/Program.cs:486-542,751-773`)
- **Linked Mitigation:** `IVSD-M003`
- **Owner / Next Validation:** Desktop & Documentation maintainer; Phase 2/5 Settings UI cleanup, NativeHost appearance check realignment, and dual-doc parity updates
- **Escalation Boundary:** User-approved priority boundary (`ADR-desktop-ui-library-and-sukiui.md`) already resolved the trade-off: Desktop safety is non-negotiable while Desktop RTL, automated WCAG contrast clamping, and Browser visual/density parity are optional bonuses

The project steward explicitly decided that because Configurator is an optional quality-of-life utility (with ISLAMU Event as ISLAMU's flagship project), Desktop should strongly delegate to SukiUI rather than maintaining custom High Contrast variants, a 418-line WCAG contrast-clamping brush engine, custom Natural/Brand HSL sliders, a 4-step density scale, or Desktop RTL layout overrides.

The ethical risk is not that an optional desktop utility delegates theming to SukiUI — it is **misrepresentation**. Leaving non-functional High Contrast options in Settings, leaving `STATUS.md` or `docs/public/desktop.md` claiming Desktop delivers unified 8-preset HSL / Light+Dark High Contrast / Browser parity, or retaining a NativeHost check claiming WCAG 4.5:1 brush verification after retiring `Configurator*Brush` would deceive operators who actually depend on High Contrast or full Arabic RTL.

Mitigating this cleanly means deleting retired Desktop appearance controls completely (zero dead toggles), keeping `Configurator.Browser` (which preserves full RTL, WCAG High Contrast, and 4-step density) prominently signposted in Desktop docs and Settings help copy as the recommended surface for RTL or high-contrast workflows, and aligning `Configurator.Desktop.NativeHost` qualification checks and docs truthfully with what SukiUI Desktop actually verifies.

### IVSD-F004: Continuous CPU/GPU Overhead And Xvfb/Headless Instability Under SukiUI Shaders

- **Lifecycle:** open
- **Severity / Claim Type:** medium / documentation-supported performance and reliability risk
- **Principles / Domains:** Ihsan (Excellence), Non-Harm (Adha) / operational, technical
- **Stakeholders:** operators on battery-powered laptops, low-resource Linux workstations, headless CI and Xvfb native-qualification environments
- **Provider-Controlled Decision:** default `SukiWindow` background style, shader animation toggle, and transition/motion policy
- **Evidence IDs / Validation Level:** SukiUI official documentation (`/home/amir/dev/Github/SukiUI/docs/docs/documentation/theming/background.md:63-69`), `tests/Configurator.Desktop.NativeHost/Program.cs:88-117`, and `tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj`
- **Linked Mitigation:** `IVSD-M004`
- **Owner / Next Validation:** Desktop Shell maintainer; Phase 2 `SukiWindow` defaults + Headless boot verification + Phase 5 Linux X11 `Configurator.Desktop.NativeHost` execution
- **Escalation Boundary:** GPU driver bugs on untested desktop environments remain outside repository control; CPU `Flat` fallback minimizes exposure

SukiUI's own documentation explicitly warns that enabling animated GPU shader backgrounds (`BackgroundAnimationEnabled="True"`) triggers continuous full visual-tree repaints consuming roughly **5% CPU and 20% GPU continuously**, even while the application is idle. For a calm configuration utility left open while an operator reads deployment docs or edits server secrets, burning 20% GPU continuously is wasteful and degrades responsiveness, and GPU shader initialization adds failure surface under software-rendered Linux Xvfb or headless test sessions.

Setting `BackgroundStyle="Flat"` and `BackgroundAnimationEnabled="False"` as the non-animated default across `MainWindow` and `SecretGeneratorDialog` eliminates background repaint waste and keeps software fallback deterministic, while preserving SukiUI's clean flat aesthetic and smooth lightweight control styling. Verification requires actual Headless and X11 `NativeHost` runs during implementation.

### IVSD-F005: Regression Risk To Physical-Settlement Close, Stale-Result Fencing, And Protected Writes

- **Lifecycle:** open
- **Severity / Claim Type:** high / source-supported data-integrity and concurrency risk
- **Principles / Domains:** Amanah (Trust), Ihsan (Excellence), Non-Harm (Adha) / security, technical, reliability
- **Stakeholders:** operators saving secrets and deployment bundles to disk during concurrent edits or window close
- **Provider-Controlled Decision:** preserving `MainWindow.OnClosing` physical-settlement drain, `OnClosed` disposal, `FocusBehavior`/`FieldFeedbackBehavior`, `Escape` layered dismissal, and `Configurator.Presentation` / `Configurator.Artifacts` boundaries when refactoring `MainWindow` onto `SukiWindow`
- **Evidence IDs / Validation Level:** direct source inspection (`src/Configurator.Desktop/Views/MainWindow.axaml.cs`, `src/Configurator.Desktop/ViewModels/DesktopShellViewModel.cs:804-898`, `src/Configurator.Desktop/Views/FocusBehavior.cs`, `tests/Configurator.Desktop.NativeHost/Program.cs:1330-1470`)
- **Linked Mitigation:** `IVSD-M005`
- **Owner / Next Validation:** Desktop Shell & Verification maintainer; Phase 2 & Phase 5 `DesktopApplicationTests`, `DesktopCloseSettlementTests`, and `Configurator.Desktop.NativeHost` (`D06`, `D07`, `D09`, `D14`–`D17`, `D30`–`D33`)
- **Escalation Boundary:** Windows/macOS protected-save admission remains explicitly disabled and outside this workstream

When an operator closes the window while a create-only protected artifact save (`0600` on Linux) is still in flight, Configurator Desktop must intercept close, wait up to the 2-second `TimeProvider` deadline for physical settlement, surface the `StayOpen` / `ForceClose` unknown-publication choice if unsettled, and zero/dispose workspace state cleanly on exit. Re-parenting `MainWindow` onto `SukiWindow` and reworking titlebar chrome, dialogs, and views could accidentally swallow `OnClosing` events, break `Window` type checks in `FocusBehavior`, leak `SukiTheme` event subscriptions across repeated test windows, or break `Escape` precedence over open popups and dialogs.

Mitigation requires keeping `MainWindow` as a `SukiWindow` subclass with unchanged `OnClosing` / `OnClosed` settlement ownership, guarding `SukiTheme` singleton subscription disposal cleanly so Headless tests and live lifetimes never leak window references, and executing failing-first / invariant regression checks on close settlement, stale-result fencing, and protected writes in the owning phases.

## Recommendations

1. **Pin `SukiUI 7.0.2-nightly20261121.525` (with `SukiUI.Motion`) rather than `7.0.1`:** `7.0.1` is rejected by `.ci/scripts/validate-dependency-license-policy.cs` (missing `<license>` metadata) and pulls in `System.Linq.Dynamic.Core` + unused `DataGrid`/`ColorPicker`. Pinning the MIT-declared `7.0.2` nightly keeps the dependency graph minimal and policy-compliant, with a clean one-line bump to stable `7.0.2` once upstream tags it.
2. **Isolate secret custody from general SukiUI overlay hosts (`IVSD-M001`):** Use `SukiDialogHost` for non-secret dialogs (Store Project Details, Keyboard Shortcuts) and `SukiToastHost` for strictly value-free/path-safe transient notifications, while keeping `SecretGeneratorDialog` as a dedicated modal `SukiWindow` with guaranteed `OnClosed` zeroing/cancellation.
3. **Verify and preserve embedded font and icon notices (`IVSD-M002`):** Inspect the restored `SukiUI` `.nupkg` in Phase 1 and record `Roboto` (Apache-2.0) and `Quicksand` (SIL OFL 1.1) — plus the chosen icon package — in `eng/configurator/dependency-notices/`, `docs/internal/legal/dependencies/configurator-ui.md`, and `THIRD-PARTY-NOTICES.md`.
4. **Retire bespoke Fluent/HSL/High-Contrast/Density code cleanly and document scope honestly (`IVSD-M003`):** Delete `DesktopTheme` WCAG/HSL clamping, `ThemePalettes.axaml`, `DesktopDensity.cs`, and Natural/Brand sliders on Desktop (leaving `Configurator.Presentation.Theming` untouched for Browser), update `DESKTOP_CLIENT.md`, `docs/public/desktop.md`, `STATUS.md`, and `Configurator.Desktop.NativeHost`, and direct RTL/high-contrast users to `Configurator.Browser`.
5. **Default `SukiWindow` to non-animated `Flat` background (`IVSD-M004`) and preserve close-settlement/custody invariants (`IVSD-M005`).**
6. **Rejected alternatives:**
   - Adopting `SukiUI 7.0.1` with a license-validator bypass (rejected: weakens license enforcement and drags in `System.Linq.Dynamic.Core`).
   - Adopting IRIHI `Semi.Avalonia` + `Irihi.Ursa` (rejected in `ADR-desktop-ui-library-and-sukiui.md` due to IRIHI's competing commercial `Asura` desktop control library and closed-source adapters).
   - Keeping dual Fluent + SukiUI compatibility themes or shims (rejected: creates template conflicts and defeats delegation to SukiUI).

## Stakeholders

- **Self-hosting operators (ISLAMU Event and general open-source targets):** directly benefit from cleaner desktop UX and must retain uninterrupted secret custody, protected-write safety, and truthful readiness/close feedback.
- **Arabic RTL and high-contrast operators:** affected by Desktop retiring custom High Contrast clamping and Desktop RTL guarantees; protected via truthful signposting to `Configurator.Browser` (which keeps full RTL, WCAG High Contrast, and 4-step density) and preservation of keyboard navigation/automation semantics.
- **Volunteer project maintainers (student steward):** primary beneficiary of delegating Desktop UI chrome/theming/dialogs/toasts to SukiUI so engineering effort focuses on ISLAMU Event and Configurator's core safety engine.
- **Downstream MIT redistributors and upstream library/font authors:** protected through exact `.nupkg` hash pinning, SPDX MIT verification, and preserved supplemental font/icon notices.

## I-VSD Principles And Domains

- **Amanah (Trust & Custody):** Governs `IVSD-F001` and `IVSD-F005` — secrets entered in Desktop must remain masked, zeroed on cancel/close, absent from toasts/diagnostics, and written only via create-only `0600` protected commits with honest physical-settlement close semantics.
- **Sidq (Truthfulness) & Avoiding Deception:** Governs `IVSD-F002` and `IVSD-F003` — third-party licenses/embedded fonts must be accurately attributed without metadata bypasses, and Desktop docs/qualification checks must never claim retired WCAG High Contrast or Desktop RTL parity.
- **Ihsan (Excellence) & Non-Harm (Adha):** Governs `IVSD-F004` and `IVSD-F005` — avoiding wasteful 20% GPU idle shader repaints and preventing shell/dialog migration regressions on operator data or focus behavior.
- **'Adl (Justice / Fair Access):** Governs `IVSD-F003` — openly preserving and documenting `Configurator.Browser` as the full RTL / WCAG High Contrast / dense-mode alternative rather than hiding Desktop's relaxed visual-adaptation trade-off.
- **Wafa bil-'Ahd (Promise-Keeping):** Governs `IVSD-F002` — honoring upstream MIT, Apache-2.0, and SIL OFL 1.1 attribution conditions.

## Validation Gaps

- **Unrestored `.nupkg` binary inspection:** exact SHA-512 content hashes and exact packaged font/icon/license archive entries inside `SukiUI 7.0.2-nightly20261121.525`, `SukiUI.Motion 7.0.2-nightly20261121.525`, `Material.Icons.Avalonia 3.0.2`, and `Material.Icons 3.0.2` are not yet restored in Configurator's lock graph (resolved and fail-closed checked in Phase 1 `TASK-1.1` / `TASK-1.2`).
- **Avalonia Headless & Linux X11 runtime verification:** live execution of `SukiTheme`, `SukiWindow`, dual-sidebar `SukiSideMenu` + `Configure` `SplitView`, `SukiDialogHost`, `SukiToastHost`, and `SukiUI.Motion` under `Avalonia.Headless` and Linux x64 X11 (`Configurator.Desktop.NativeHost` `33/33`) remains unexecuted until implementation Phases 1–4.

## Escalation Needed

- **Implementation fail-closed gate (Phase 1, `TASK-1.2`):** If inspecting the restored `SukiUI 7.0.2-nightly20261121.525` or `Material.Icons.Avalonia 3.0.2` / `Material.Icons 3.0.2` `.nupkg` archives reveals any embedded font, icon, or asset under a non-permissive or incompatible license, stop immediately and escalate to the project steward before proceeding.
- **No religious-legal (fiqh) escalation required:** This workstream governs offline desktop UI library migration, license attribution, local secret custody, and truthful capability disclosure with no religious-legal determination.

## Evidence Reviewed

- Completed `desktop-sukiui-adoption` triad (`dev/active/desktop-sukiui-adoption/desktop-sukiui-adoption-{plan,tasks,context}.md`, 2026-11-23)
- `docs/internal/adr/ADR-desktop-ui-library-and-sukiui.md` (decision record locking SukiUI MIT adoption and safety-vs-bonus boundary)
- `docs/internal/DESKTOP_CLIENT.md`, `docs/public/desktop.md`, `docs/internal/STATUS.md`, `docs/internal/QUICK_REFERENCE.md`
- `src/Configurator.Desktop/` (all views, `App.axaml*`, `Program.cs`, `DesktopTheme.cs`, `DesktopDensity.cs`, `ThemePalettes.axaml`, `DesignTokens.axaml`, `DesktopShellViewModel.cs`, `SecretGeneratorDialog.axaml.cs`, `FocusBehavior.cs`, `FieldFeedbackBehavior.cs`, `AvaloniaSecretClipboardService.cs`)
- `src/Configurator.Browser/Layout/MainLayout.razor`, `NavMenu.razor`, `Components/Docking/DockLayoutHost.razor`, and `/home/amir/ISLAMU/Github/Event/src/Explore.Blazor.Client/Layout/MainLayout.razor`, `Components/Shell/AppWorkspaceRail.razor`
- `tests/Configurator.Desktop.Tests/` (`DesktopThemeTests.cs`, `DesktopAppearanceTests.cs`, `DesktopNavigationTests.cs`, `Views/MainWindowLayoutTests.cs`, `Views/MainWindowBindingTests.cs`, `Configurator.Desktop.Tests.csproj`)
- `tests/Configurator.Desktop.NativeHost/Program.cs` (`RequiredChecks`, `D25`, `D27`, `CaptureAsync`, `RequireContrast`, `PhysicalSettlementAsync`)
- `.ci/policy/dependency-licenses.json`, `.ci/scripts/validate-dependency-license-policy.cs`, `.ci/scripts/write-dependency-notices.cs`, `eng/configurator/Configurator.Release.proj`, `eng/configurator/dependency-notices/textmatesharp.grammars/2.0.4/*`, `docs/internal/legal/dependencies/configurator-ui.md`, `tests/Configurator.Architecture.Tests/StandalonePackageTests.cs`
- `/home/amir/dev/Github/SukiUI/docs/docs/documentation/` (installation, launch, theming, backgrounds, hosts, dialogs, toasts, layout, navigation, notification, progress, data, and input controls) and `/home/amir/dev/Github/SukiUI/LICENSE` + font asset listing
- NuGet flatcontainer `.nuspec` metadata (`api.nuget.org`) for `SukiUI 7.0.1`, `SukiUI 7.0.2-nightly20261121.525`, `SukiUI.Motion 7.0.2-nightly20261121.525`, `Material.Icons.Avalonia 3.0.2`, `Material.Icons 3.0.2`, `FluentIcons.Avalonia 2.1.345`, `Lucide.Avalonia 0.2.32`

## Missing Evidence

- Restored NuGet package SHA-512 hashes and exact archive entry listings for `SukiUI 7.0.2-nightly20261121.525`, `SukiUI.Motion 7.0.2-nightly20261121.525`, `Material.Icons.Avalonia 3.0.2`, and `Material.Icons 3.0.2` (to be inspected during Phase 1 `TASK-1.1` / `TASK-1.2`).
- Live `Avalonia.Headless` and Linux x64 X11 (`Configurator.Desktop.NativeHost`) execution evidence on the dual-sidebar `SukiWindow` + `SukiSideMenu` + `SukiTheme` build.
- Upstream stable `SukiUI 7.0.2` release tag date (currently `7.0.2-nightly20261121.525` is the latest MIT-declared modular build on NuGet.org).

## Context Inventory

- **Workstream:** `desktop-sukiui-adoption` (`dev/active/desktop-sukiui-adoption/desktop-sukiui-adoption-{plan,tasks,context}.md`)
- **Resolved Grill-Me Intake Decisions:** Dual-sidebar SukiUI shell (`SukiWindow` + collapsible global `SukiSideMenu` + contextual `Configure` Definition Sections rail matching `Configurator.Browser` and `Explore.Blazor.Client`) and `Material.Icons.Avalonia 3.0.2`
- **Active sibling workstreams checked:** `dev/active/browser-launch-readiness/` (Browser static host/qualification only — zero overlap with `src/Configurator.Desktop/`)
- **Paused workstreams checked:** `dev/pause/` absent
- **Working-tree state:** Documentation decision updates from 2026-11-23 present across `docs/` and `dev/_journal/MAJOR_DECISIONS.md`; no runtime code edits made

## Planning Handoff
- Workstream: desktop-sukiui-adoption
- Status: current
- Reviewed input: `desktop-sukiui-adoption` implementation plan triad (`plan.md`, `tasks.md`, `context.md`, 2026-11-23)
- Findings and mitigations: `IVSD-F001 -> IVSD-M001`, `IVSD-F002 -> IVSD-M002`, `IVSD-F003 -> IVSD-M003`, `IVSD-F004 -> IVSD-M004`, `IVSD-F005 -> IVSD-M005`
- Required plan mappings:
  - `IVSD-F001` / `IVSD-M001` -> Scenarios `3.3A`, `3.3B`; `TASK-2.4`, `TASK-2.5`, `TASK-3.2`, `TASK-3.3`, `TASK-3.4`, `TASK-4.1`, `TASK-4.2`, `TASK-4.3`
  - `IVSD-F002` / `IVSD-M002` -> Scenario `3.7A`; `TASK-1.1`, `TASK-1.2`, `TASK-1.3`, `TASK-1.5`, `TASK-4.6` (with Phase 1 fail-closed escalation gate on non-permissive restored assets)
  - `IVSD-F003` / `IVSD-M003` -> Scenarios `3.2A`, `3.2B`; `TASK-2.1`, `TASK-2.2`, `TASK-2.3`, `TASK-4.4`, `TASK-4.5`, `TASK-4.7`
  - `IVSD-F004` / `IVSD-M004` -> Scenario `3.6A`; `TASK-2.5`, `TASK-4.3`, `TASK-4.6`
  - `IVSD-F005` / `IVSD-M005` -> Scenarios `3.4A`, `3.5A`, `3.5B`; `TASK-2.4`, `TASK-2.5`, `TASK-2.6`, `TASK-4.2`, `TASK-4.5`, `TASK-4.6`
- Escalations required before: Phase 1 implementation only if restored `.nupkg` inspection discovers an unexpected non-permissive asset; no planning-time escalations remain open
- Refresh triggers: change in SukiUI or icon package version/source; moving `SecretGeneratorDialog` into `SukiDialogHost`; enabling animated SukiUI shader backgrounds by default; changing Desktop's non-negotiable secret/close/protected-write contract; or changing Desktop's RTL/High-Contrast support claims

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-11-23 | none | draft | Initial planning intake for `desktop-sukiui-adoption` | Shared repository, SukiUI docs, and NuGet metadata evidence packet |
| 2026-11-23 | draft | current | Revalidation against completed `desktop-sukiui-adoption` triad after Grill-Me resolution of dual-sidebar shell and `Material.Icons.Avalonia 3.0.2` | `dev/active/desktop-sukiui-adoption/desktop-sukiui-adoption-{plan,tasks,context}.md` (Section 3 scenarios, Section 9 1:1 mappings, `TASK-1.1`–`TASK-4.10`) |
