# Browser interface ownership

The static WebAssembly shell owns navigation and appearance. `Layout/MainLayout`
provides skip navigation, the header, sidebar, main landmark and footer. Home,
Store, Open configuration and Help are route destinations; the configuration
workspace remains owned by Presentation and the Browser editor.

Below the sidebar breakpoint, navigation expands inline from a named Menu
button. Escape closes it and restores focus to that button. This disclosure
does not create a modal or trap focus. Skip navigation focuses the main landmark
without changing the URL, so it cannot discard a live workspace through a
fragment navigation.

`AppearanceThemeService` owns a separate immutable `AppearanceState`. The dedicated
`configurator.appearance.v1` storage key accepts only bounded `themeMode` and
`direction` strings. Unsupported, duplicate, unknown, malformed or oversized
preferences fall back to system appearance and automatic direction. Browser
storage failures leave the current tab usable. Appearance has no answer,
artifact, definition, target or transport reference.

The light/dark palettes and fluid typography adapt first-party Event precedents.
High-contrast modes strengthen palette contrast; system mode follows operating
system appearance. Direction changes update the document and MudBlazor RTL
provider without replacing route content or workspace state. System fonts are
used; no remotely hosted font or icon asset is fetched.

Global CSS follows reset, base, tokens, mudblazor-overrides, components and
utilities layers. The local MudBlazor stylesheet is imported into `reset`
before application normalization. An unlayered vendor import would outrank
all application layers, removing native-control borders and page spacing.
Theme-provider styles and component isolation remain unlayered.
Three-tier tokens bind primitive spacing/radii to palette
semantics and component tokens. Colocated component styles use logical
properties. Local MudBlazor assets supply controls and providers. Runtime theme
styles require the explicit CSP style allowance documented in the launch
contract; script origins remain local.

See [Browser guide](../public/browser.md), [launch boundary](LAUNCH_CONTRACT.md)
and [transfer provenance](extraction/BROWSER_UI_TRANSFER.md). Component tests
verify rendered structure, preference admission and presentation changes.
Published-browser keyboard, zoom/reflow, theme, RTL, offline and CSP evidence
is separate from component-test evidence.

## Interface refinement

Shared native text controls explicitly opt into `form-control`; MudBlazor
continues to own its outlined inputs. Control borders use the input palette,
while decorative card borders use the divider palette. Normal themes use a
teal action accent; high-contrast themes retain their monochrome palettes.
The shell shares header/sidebar dimensions through component tokens. Its main
column fills the viewport and keeps the footer below content without a fixed
overlay. Navigation icons are local MudBlazor SVGs with decorative semantics.

Open configuration groups local-file and HTTPS entry in independently named
sections with labels and connected format/help text. Store filters have a
separate surface and a recoverable no-match state. Cards retain complete
descriptions and align revision actions without clipping content. The editor
groups field view and explicit secret consent in a responsive toolbar,
distinguishes complete and draft preparation, and keeps optional artifact
import in its own labelled section. Container queries reflow editor controls
within the available workspace width; logical spacing supports RTL.

The structure and styling are independently designed from existing repository
components. No packages, remote fonts or external design assets were added.
Research used only functional standards and API facts from
[W3C form guidance](https://www.w3.org/WAI/tutorials/forms/),
[CSS cascade layers](https://www.w3.org/TR/css-cascade-5/#layering), and
[Microsoft CSS isolation documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/css-isolation?view=aspnetcore-10.0).
No external snippets or expressive UI composition were copied. The AFC/SSO
review filtered platform semantics and standard form grouping; discretionary
layout, tokens and component organization remain repository-owned.

## Guided workspace

`Components/Configuration/ConfigurationEditor` retains the shared
`DefinitionFormModel` and workspace operation generations. Browser-owned
`ConfigurationSection` projections group the shared validated fields; every
panel remains mounted while inactive panels are hidden. Moving between sections
therefore preserves both typed answers and temporarily invalid input without
weakening validation of hidden active fields.
Controls mount when first visible and remain mounted after being visited;
never-active controls are omitted so large conditional definitions stay usable.

`ConfigurationField` sends parsed typed values through the shared form. An absent
boolean differs from false, and absent objects/lists differ from empty values.
Masked scalar controls inherit sensitive or secret ancestors; secret consent
still uses the form's edit permission, allowing public siblings to remain
editable. Authored titles are preserved. Mechanical identifier formatting is
used only when title equals key, with the exact key available separately.

`ConfigurationReview` projects shared diagnostics into value-free explanations
and correction buttons. A correction selects the section and focuses its field
or the diagnosed collection row. Secret recovery also covers required secret
descendants inside optional supplied objects. Draft and complete preparation
remain distinct; filenames and completion locations come from shared artifacts.
Reveal, copy and download remain deliberate operations owned by
`BrowserArtifactExport`, and edits, revocation and disposal invalidate output.

`BrowserNavigationState` and `WorkspaceNavigationGuard` confirm route departure.
Cancellation keeps the workspace. Confirmation invalidates current operations
synchronously, then awaits discard before allowing another source/store route.
See [Presentation state](PRESENTATION_STATE.md) for generation ownership and
[Operations](OPERATIONS.md#browser-custody-and-offline-verification) for actual
published-browser evidence.
