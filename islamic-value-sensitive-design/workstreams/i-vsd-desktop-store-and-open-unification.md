# Desktop Store & Unified Open Experience — Provider-Responsibility Assessment

Last Updated: 2026-11-09 Europe/Brussels

## Review Metadata
- Mode: planning
- Subject: Embedded offline Definition Store in `Configurator.Presentation`, Avalonia Desktop 3-tier Open experience (Remote URL + Local File + Embedded Store), managed file dialog fallback via `Avalonia.Dialogs`, and elimination of browser JS-fetch store dependency
- Workstream: desktop-store-and-open-unification
- Report kind: implementation-planning assessment
- Report revision: desktop-store-ivsd-r1
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-09
- Reviewed input: Implementation plan and tasks triad for `desktop-store-and-open-unification` against repository `HEAD` and live Linux Wayland/XWayland diagnostic evidence
- Supersedes: None (new workstream)

## Executive Summary

Configurator is an offline configuration preparation utility designed for sovereign, private, and localized deployment workflows. Currently, an architectural asymmetry and UI gap exist between the Blazor web client and the Avalonia desktop client:
1. **Store Asymmetry & Web-Coupling:** The curated schemas in `store/` are accessible only in Blazor WebAssembly through an optional publish-time flag (`DefinitionStoreSourceRoot`) and a JavaScript `fetch()` network layer (`browser-store.js`), whereas the Avalonia desktop application has no access to the repository store.
2. **Fragile Native File Dialogs:** On Linux desktop environments, Avalonia's reliance on DBus `org.freedesktop.portal.FileChooser` causes silent dialog failures when GNOME/Nautilus portal daemons crash or disconnect under Wayland/XWayland. The app treats this failure as user cancellation, resulting in unresponsive buttons with zero diagnostic feedback.
3. **Disjointed Open Workflows:** The desktop client presents only a bare local file path input, lacking the ability to fetch remote public schemas via URL or to select verified repository schemas from an embedded catalogue.

Under Islamic Value-Sensitive Design (I-VSD), software tools must respect operator sovereignty (**Istiqlal**), truthfulness and clarity (**Sidq** and **Bayan**), asset preservation (**Hifz al-Mal**), and equitable capability (**Adl**). 

This assessment evaluates the unified plan:
- Embedding `store/` directly into application assembly assets in `Configurator.Presentation` to provide 100% offline, zero-network schema access across both Desktop and Web.
- Incorporating an in-process managed file picker fallback via `Avalonia.Dialogs` to eliminate silent portal crashes.
- Unifying the Desktop `Open` experience into three clear, accessible tiers: (1) Remote HTTPS Schema URL, (2) Local File with working picker, and (3) Embedded Repository Store catalogue directly below.

## Scope

- Assembly-embedded store asset generation and streaming in `Configurator.Presentation`.
- Shared `IDefinitionStoreProvider`, `EmbeddedDefinitionStoreProvider`, and MVVM catalog models in `Configurator.Presentation.Store`.
- Bounded remote schema fetcher (`DefinitionUrlFetcher`) with HTTPS enforcement and size limits.
- Avalonia Desktop `DefinitionLoaderViewModel` and `DefinitionLoaderView.axaml` multi-source unification.
- `Avalonia.Dialogs` managed system dialog configuration on `AppBuilder`.
- Elimination of legacy JS `fetch()` and `browser-store.js` in `Configurator.Browser`.
- Test suites in `tests/Configurator.Presentation.Tests` and `tests/Configurator.Desktop.Tests`.

## Claim Boundary

Evidence reviewed includes live runtime execution on Linux x64 (Wayland / `xwayland-satellite`), journal logs confirming `xdg-desktop-portal-gnome` disconnection, static code inspection across `src/Configurator.Browser`, `src/Configurator.Desktop`, and `src/Configurator.Presentation`, and `store/` structure. No Sharia/fiqh ruling is issued; this is an engineering provider-responsibility assessment.

## Findings & Mitigations

### IVSD-F001: Sovereign Local Custody & Zero Unintended Network Leaks (Istiqlal / Hifz al-Khas)
Embedding definitions directly into compiled application binaries ensures operators can configure critical infrastructure completely offline, air-gapped, and without external dependencies. However, adding a remote schema URL fetch input introduces an outbound network boundary that must not compromise privacy or act as an answer-bearing vector.

- **Mitigation (`IVSD-M001`)**: 
  - Embedded store access MUST execute entirely in-memory using .NET `Assembly.GetManifestResourceStream`, requiring zero API calls, zero web requests, and zero disk permissions.
  - Remote URL fetching MUST be strictly user-initiated, restricted to public HTTPS (rejecting unencrypted HTTP, private/loopback IP addresses, and embedded credentials), bounded to a strict 4 MiB transfer limit, and used solely for schema intake. It MUST NEVER transmit user answers, drafts, or configuration payloads.

### IVSD-F002: Truthful Feedback & Error Transparency (Sidq / Bayan)
When the Linux desktop portal crashes or when an operator clicks "Load definition" on an empty path, silently swallowing the error or displaying an internal code (`definition-invalid:source-format-invalid`) violates truthfulness and misleads the operator into believing the application is broken.

- **Mitigation (`IVSD-M002`)**:
  - Incorporate `Avalonia.Dialogs` `.UseManagedSystemDialogs()` to provide an in-process managed file dialog that guarantees a working picker even if system portals are dead.
  - If a file picker operation fails or is unavailable, display clear, human-readable guidance.
  - Validate empty inputs explicitly: when "Load definition" is clicked without a path, inform the operator to enter a valid file path or pick a project from the embedded store below.

### IVSD-F003: Equitable Capability Across Platforms (Adl / Musawah)
Operators using the Avalonia Desktop client on Linux, Windows, or macOS should have first-class access to verified repository definitions, matching and exceeding the Blazor web experience without artificial tiering or platform degradation.

- **Mitigation (`IVSD-M003`)**:
  - Implement a rich, responsive card grid directly in the desktop `Open` screen, displaying title, description, application ID, tags, and a direct "Configure Project" button.
  - Unify the presentation models in `Configurator.Presentation` so both Blazor and Avalonia share the exact same catalog discovery and admission pipeline.

## Status & Disposition

- Report status: current
- Disposition: plan-aligned
- Every finding (`IVSD-F001` through `IVSD-F003`) maps to an executable requirement and verification task in the triad.
