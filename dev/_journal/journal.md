# Dev Journal

Last Updated: 2026-10-14 Europe/Brussels

## Index & Recent

Use [README.md](README.md), [FINDING_TEMPLATE.md](FINDING_TEMPLATE.md) and [PROMOTION_RULES.md](PROMOTION_RULES.md) for recording and promotion. This is Configurator-owned evidence; Event findings are not copied as Configurator history.

## Durable Findings

Append observed target findings chronologically using the template.

- 2026-10-10 — [Inherited privacy, expansion budgets, schema reference scope, lineage and published closure](domains/definition-authoring.md).

- 2026-10-09 — [Browser cache capabilities and download filename custody](domains/presentation-and-clients.md).

[2026-10-09 Europe/Brussels] — Deferred text events can invalidate imported Desktop state

**Context**: During Desktop functional-readiness implementation, the isolated native X11 harness exercised supported inverse import through the actual controls after the headless baseline passed.

**Symptom / Observation**: Import initially reported `import-ready`, but after the native dispatcher drained it reverted to `inputs-changed`. Native qualification rejected `D04-json-yaml-dotenv-import:import-status-invalidated`.

**Root Cause**: `FieldEditor.Build` assigned initial TextBox text before subscribing to its deferred `TextChanged` event. That queued event arrived after construction and after `MainWindow.BuildFields` attached the editor's answer-change callback. Initializing an imported control therefore looked like a second operator edit and invalidated preparation/status.

**Resolution**: Observe synchronous `TextBox.TextProperty` changes, retaining the existing build guard and shared form owner. Initial assignments occur before subscription; later operator changes still enter the same typed-answer path. The real native host command `dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 10` now passes all 13 named checks, and the Desktop suite passes 64 tests.

**Why This Matters for Future Work**: A synchronous headless dispatcher drain can miss native deferred-event ordering. Control initialization is not an answer edit; native workflow proof must complement headless focus and state tests.

**References**:
- `src/Configurator.Desktop/Views/FieldEditor.cs`
- `src/Configurator.Desktop/Views/MainWindow.axaml.cs`
- `tests/Configurator.Desktop.NativeHost/Program.cs`
- `docs/internal/DESKTOP_CLIENT.md`

**Promotion Consideration**:
- [x] Stays in journal only (native control event-ordering lesson).

[2026-10-10 Europe/Brussels] — Optional worker assets must not gate core readiness

**Context**: While implementing the optional browser definition store, real published-browser verification used an Apache virtual host that denied every `definition-store/` path.

**Symptom / Observation**: The complete published interface rendered, but every configuration control stayed disabled with `Offline app preparation is incomplete.` The same application worked when discovery files were available.

**Root Cause**: Adding every store manifest entry to the worker's mandatory `cache.addAll` inventory coupled install success and workspace readiness to optional discovery. Browser component tests and package inventory checks verified separate owners but could not expose worker installation behavior.

**Resolution**: `service-worker.published.js` gates installation/status on required UI/runtime URLs only. Exact manifest-listed store URLs are read deliberately before custody, checked against SDK integrity hashes and cached as public content. Reloading the denying host activated the worker, enabled the workspace and prepared three draft artifacts without a store request. Store discovery and the selected canonical revision also reopened through the cache with Playwright offline emulation.

**Why This Matters for Future Work**: A file appearing in the SDK static-assets manifest does not make it a prerequisite for private work. Keep optional discovery failure out of core readiness, and test worker installation against actual unavailable assets in addition to component and package inventories.

**References**:
- `src/Configurator.Browser/wwwroot/service-worker.published.js:14`
- `src/Configurator.Browser/wwwroot/pwa-lifecycle.js:131`
- `docs/internal/adr/ADR-optional-definition-store.md`
- `docs/internal/OPERATIONS.md`

**Promotion Consideration**:
- [x] Stays in journal only (optional asset installation lesson; boundary already documented in the ADR).

[2026-10-11 Europe/Brussels] - MVVM picker replacement and focus require actual rendered-control signals

**Context**: The Desktop MVVM cutover replaced procedural fields and click handlers
with compiled templates, injected child viewmodels and command bindings.

**Symptom / Observation**: Headless tests that admitted a definition before
materializing the window passed, while native captures showed a blank default
artifact selector. Picker cancellation also failed to return focus despite a
valid viewmodel focus request.

**Root Cause**: Publishing the default selection before replacing ItemsSource
let a two-way selector reset erase that default. Focus arrived while the async
command still disabled the picker button; one dispatcher post was not a reliable
signal that the button had become focusable. Raising routed ClickEvent directly
does not invoke a command-bound button's action.

**Resolution**: `ArtifactPublisherViewModel.ReplaceForm` publishes the choices
inside its replacement guard before assigning their default. The regression
materializes the empty window before admission and checks its selection box and
rendered identifier. `FocusBehavior` subscribes to attachment and effective
enablement, cancels superseded requests and removes pending handlers on detach.
Control tests invoke the command resolved from the rendered button with its
CanExecute gate. Native captures await an actual resize and animation frame,
not sleeps, and inspect both top and footer at normal/minimum sizes in both themes.

**Why This Matters for Future Work**: Viewmodel state alone does not prove
two-way control initialization or command-completion focus. Exercise the real
admission order and subscribe to the precise UI readiness signal.

**References**:
- `src/Configurator.Desktop/ViewModels/ArtifactPublisherViewModel.cs`
- `src/Configurator.Desktop/Views/FocusBehavior.cs`
- `tests/Configurator.Desktop.Tests/Views/MainWindowBindingTests.cs`
- `tests/Configurator.Desktop.Tests/DesktopLocalSelectionTests.cs`
- `tests/Configurator.Desktop.NativeHost/Program.cs`

**Promotion Consideration**:
- [x] Stays in journal only; existing desktop custody/operation boundaries remain authoritative.

[2026-10-14 Europe/Brussels] — Native adoption uses the portable skill and CLI

**Context**: While reviewing Event's integration with Configurator, the user
rejected the Event-specific exporter and requested its removal. This records
Configurator's adoption contract rather than importing Event's implementation
history.

**Symptom / Observation**: A contributor needed a separate engineering project,
fixture-driven generator and compiler wrapper to maintain the integration.
The user stated: "we need a native supported way, to help maintainers produce
their correct file" and identified the Agentic Skill as that path.

**Root Cause**: The integration treated target-specific generation as a required
authoring authority instead of using the supported portable skill and native CLI.
This made adopting Configurator feel like maintaining another toolchain.

**Resolution**: The supported workflow is `configurator-integrate` with an agent
that researches the target's public declarations, schemas, validators and
synthetic examples, authors one maintained compact source, and uses native
`definition init/check/revise/build` plus synthetic preparation checks. The
contributor reviews the source, evidence and missing facts. A custom exporter,
compiler wrapper, downloader or target runtime dependency is not required.
Direct native checking exposed ambiguous `Off` and empty null scalars in the
previous serializer output. Quoting the string and writing explicit `null` made
`definition check` and `definition build --against` pass while preserving the
existing canonical digest. A general YAML serializer round trip had not proved
admission through Configurator's bounded YAML grammar.

**Why This Matters for Future Work**: The skill supplies repository-aware
guidance; the CLI supplies machine-enforced admission and preparation. A
successful compiler check does not establish target compatibility, acceptance
or apply authority. Unsupported target facts must be surfaced rather than
invented. Good adoption UX keeps those responsibilities explicit without adding
bespoke target tooling.

**References**:
- `skills/configurator-integrate/SKILL.md`
- `skills/configurator-integrate/resources/authoring.md`
- `docs/public/integrate-your-tool.md`

**Promotion Consideration**:
- [x] Documented in the public integration guide; preserve as an adoption decision.
