# Desktop workspace composition

> **Status:** Accepted design; implementation verification in progress
> **Owner:** Desktop
> **Date:** 2026-11-06
> **Source Anchors:** `src/Configurator.Desktop/ViewModels/DesktopNavigationViewModel.cs`, `src/Configurator.Desktop/ViewModels/FormWorkspaceViewModel.cs`

The flat Desktop stack presents intake, recursive editing and publication at
once. Large definitions need bounded visual mounting without losing drafts,
row identities or global readiness.

Use a local Open/Configure/Review navigation projection over the existing
workspace. Authored sections borrow the factory's field viewmodels; root maps
and variants receive a neutral section. Root pages contain at most 50 editor
references. Hiding a stage or changing a page never disposes the form or its
surviving field projections. Native view handlers have a separate lifetime.

Mounted slices use stable read-only observable collections. Reconciliation
removes retired references and inserts/moves current references in place.
Replacing an ItemsSource array for an ordinary edit or disclosure change
would recreate surviving Avalonia controls even when the field viewmodels
remain stable, losing focus, caret and the live binding target. Paging can
retire off-page views; ordinary refresh must preserve surviving containers.

Presentation continues to own accepted values, raw drafts, revisions,
activation, discovery and typed correction locations. Navigation must resolve
a current correction, reveal its section/page and ancestors, and recheck its
identity after native layout before focus. Visual selection cannot grant
preparation or publication readiness.

The publisher retains preparation and physical settlement, and Artifacts
retains create-only protected writes. Appearance remains a session preference
under the existing native high-contrast owner. This needs no navigation
framework, answer cache, persisted preference or compatibility layer.

Explicit pages trade uninterrupted scrolling for bounded visible scopes.
Per-level limits do not prove bounded total evaluator work or latency.
Actual native accessibility, keyboard and platform qualification remain
separate evidence gates in the [Desktop contract](../DESKTOP_CLIENT.md).
