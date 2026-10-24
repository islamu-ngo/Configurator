# Dev Journal

Last Updated: 2026-10-19 Europe/Brussels

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

[2026-10-16 Europe/Brussels] — Cancelled pipe reads can hang during non-owning disposal

**Context**: Native CLI lifetime work replaced synchronous execution with token-aware asynchronous I/O. A real child process consumed more than a pipe's capacity while stdin remained open, then received SIGTERM or Ctrl+C.

**Symptom / Observation**: The work token cancelled and the pending read threw, but the child never delivered its final receipt or exited. In-memory cancellation tests passed; all four native signal variants timed out.

**Root Cause**: The Unix standard-stream adapter wrapped inherited descriptors with non-owning `SafePipeHandle` objects. Cancellation reached `ReadAsync`, but disposing that asynchronous pipe wrapper stalled. The hang occurred during cleanup, not cancellation registration. Console fallback also cannot interrupt a pending terminal read.

**Resolution**: The invocation owns each inherited Unix handle through the same safe-handle object shared with its stream. Redirected pipes use asynchronous pipe I/O; ordinary files retain the console fallback. Terminal-backed `--input -` rejects before reading. The exact command `dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj --configuration Release -- --minimum-expected-tests 1 --no-progress` passed all 105 tests, including the native signal and terminal cases. Commit: `d6ba9739ee3868e124b99c8c1c5f00e75fc29f15`.

**Why This Matters for Future Work**: Cancellation, disposal and final-delivery lifetimes need separate assertions. A cancelled task does not prove its process can release native resources. Keep the stdin producer open during process tests; closing it would hide this defect. This evidence is Linux-specific, not proof of Windows/macOS behavior.

**References**:
- `src/Configurator.Cli/Program.cs`
- `tests/Configurator.Cli.Tests/SetupCliProgramTests.cs`
- `docs/internal/adr/ADR-cli-command-runtime.md`
- https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream.-ctor?view=net-10.0

**Promotion Consideration**:
- [x] Runtime ADR records the ownership decision; retain the debugging evidence here.

[2026-10-19 Europe/Brussels] - Sequential cleanup needs all-owner invalidation evidence

**Context**: CTO review of the Browser production-v1 plan traced consent, shared edit revisions and export ownership without implementing fixes or running product tests.

**Symptom / Observation**: `ConfigurationEditor.InvalidateOutputAsync` removes artifact UI entries, then awaits each artifact's disposal in sequence. Holding the first cleanup delays reaching later owners. The existing lifecycle regression asserts artifact counts/status after release, while its JS fake models cleanup completion but not private bytes or open/id/generation behavior.

**Root Cause**: Removing UI references, invalidating each output owner and completing asynchronous resource cleanup are different transitions. A one-artifact fake that only delays disposal cannot establish that all detached outputs lose authority before the first await, nor that replacement output retains the correct bytes.

**Resolution**: No runtime fix is claimed. The revised plan requires synchronous invalidation of the entire detached artifact set before awaited cleanup, plus a failing two-artifact regression. It must assert stale-capability rejection while the first cleanup remains held and verify replacement content afterward, using real form/Core objects and a boundary fake faithful to the actual JS generation contract. Cleanup failure and replacement orderings remain required. Product verification is pending.

**Why This Matters for Future Work**: In multi-resource privacy cleanup, verify every owner's authority before releasing the first asynchronous gate. DOM counts and final status do not prove byte or capability safety. An already completed external clipboard/download transfer remains outside revocation guarantees.

**References**:
- `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs:343`
- `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs:411`
- `src/Configurator.Browser/BrowserArtifactExport.cs`
- `src/Configurator.Browser/wwwroot/browser-artifacts.js`
- `tests/Configurator.Browser.Tests/BrowserWorkspaceLifecycleTests.cs:37`
- `docs/internal/adr/ADR-browser-answer-intent-and-denial.md`
- Source revision: `37bfa9b492f54f0e746150edc9a624ad668774b3`.

**Promotion Consideration**:
- [x] Stays in journal only while implementation evidence is pending; the related ADR remains proposed, not an accepted new policy.

[2026-10-19 Europe/Brussels] - HTML number controls can conceal invalid answer drafts

**Context**: Browser answer-integrity implementation reached actual published Chromium 153 after the component suite passed.

**Symptom / Observation**: Trusted entry of `1e` into an optional numeric control produced DOM value `""` with `validity.badInput === true`. The shared parser saw absence, and complete preparation incorrectly succeeded. bUnit's event boundary preserved the supplied test string and did not reproduce browser number-input sanitization.

**Root Cause**: `ConfigurationField.razor` used an HTML number input for a string-backed draft. Browser sanitization happened before the event reached the shared typed conversion owner, so malformed text and deliberate absence became indistinguishable.

**Resolution**: Keep raw drafts in text inputs with numeric/decimal input-mode hints, then use the shared parser for validity. Actual Chromium now retains `1e`, marks the input invalid and rejects preparation with no artifact. The exact command `dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress` passed 150 cases; a freshly published package also preserved empty values, false and zero in revealed and downloaded JSON.

**Why This Matters for Future Work**: A component fake can test parser and event ordering without implementing the browser's native control normalization. Verify malformed drafts on the real platform before claiming that an HTML input type preserves typed-answer intent.

**References**:
- `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor`
- `src/Configurator.Presentation/Presentation/DefinitionScalarEditing.cs`
- `tests/Configurator.Browser.Tests/BrowserCollectionWorkflowTests.cs`
- `docs/internal/PRESENTATION_STATE.md`

**Promotion Consideration**:
- [x] Stays in journal only; the shared draft contract is documented in the presentation anchor.

---

[2026-10-20 Europe/Brussels] — Held input callbacks need a workspace revision fence

**Context**: While implementing guided configuration and consented credential entry, the Browser had to reject fill notifications retained across workspace changes. Secret controls and shared editing already checked current consent.

**Symptom / Observation**: A component regression retained the production-generated input callback, revoked consent, enabled it again and invoked the old callback. It published a synthetic secret even though the callback came from the earlier workspace state. `HeldFillCannotCrossWorkspaceInvalidation` now covers reconsent, intervening edits, form replacement and disposal.

**Root Cause**: The event handler read mutable component `Form` and `Path` properties when invoked. A currently true `CanEdit` result established present permission, not that the event belonged to the current answer revision or form. Stable control IDs alone could not establish that relationship.

**Resolution**: Each rendered scalar callback captures its form, copied literal path and input revision. The component rejects a disposed owner or mismatched form, path or revision before calling the existing shared typed edit and permission checks. Commit `d85af1a` records the implementation. The regression obtains the actual generated callback through a test subclass and standard render builder, without replacing the editing owner or reflecting private methods. The executed command `dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 176 --no-progress --no-ansi`, with `TMPDIR="$HOME/.cache/agent-tmp"`, passed all 176 cases, including the nine credential-entry cases. Real Browser checks also confirmed absent denied controls and empty reconsent; this is not a claim about a particular password-manager extension.

**Why This Matters for Future Work**: Permission can return to the same Boolean state while the workspace has changed. Retain an actual emitted callback when testing delayed input, and bind it to the owning form and revision rather than relying only on current consent or DOM identity.

**References**:
- `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor:99`
- `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.cs:166`
- `tests/Configurator.Browser.Tests/BrowserCredentialEntryTests.cs:34`
- `docs/internal/LAUNCH_CONTRACT.md`
- `docs/internal/PRESENTATION_STATE.md`
- Local commit: `d85af1a`

**Promotion Consideration**:
- [x] Stays in journal only; the production state and launch contracts already own the invariant.

[2026-10-24 Europe/Brussels] — Drain queued long-task records before observer teardown

**Context**: Browser editor qualification measured trusted input through rendered
completion, including warmed main-thread long tasks.

**Symptom / Observation**: A controlled 80 ms trusted callback was absent from
all four observations using the original teardown. Draining pending records
detected the long task in all four matching observations.

**Root Cause**: The completion callback disconnected `PerformanceObserver`
before its queued long-task records were delivered. A zero result described
an undrained observer, not necessarily an uninterrupted interaction.

**Resolution**: Collect `takeRecords()` before `disconnect()`, retaining delivered
entries and filtering against the interaction boundary. Preserve the original
samples, invalidate their zero-long-task interpretation and requalify the final
package with the corrected observer. The controlled probe and exact old/new
adapters are retained in task-owned evidence; this is a measurement correction,
not a product telemetry feature.

**Why This Matters for Future Work**: Validate a performance observer with a
known positive control before treating absence of entries as acceptance.
Rendered completion and observer delivery are separate events.

**References**:
- `docs/internal/OPERATIONS.md`
- `.omo/evidence/browser-ui-ux-redesign/observer-drain-probe.json`
- `.omo/evidence/browser-ui-ux-redesign/observer-teardown-correction.json`

**Promotion Consideration**:
- [x] Stays in journal only; browser qualification owns the measurement protocol.
