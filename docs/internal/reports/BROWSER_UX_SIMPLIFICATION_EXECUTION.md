# Browser UX simplification execution evidence

> **Audience:** Contributors | AI agents | Product steward
> **Kind:** Workstream execution evidence
> **Status:** Revision-5 source delivered and native checks passed; browser performance and human qualification incomplete
> **Owner:** Browser and Presentation maintainers
> **Last Verified:** 2026-11-02
> **Source Anchors:** `src/Configurator.Browser/`, `src/Configurator.Presentation/`, `src/Configurator.Core/Definitions/`, `dev/active/browser-ux-simplification/`
> **Evidence period:** 2026-10-31 through 2026-11-02

## Revision-5 execution and evidence boundary

Full implementation was authorized on November 1 in the existing checkout.
The current source passes Core 362/362, Presentation 277/277, Browser 370/370,
CLI provider integration 1/1 and final-package Architecture 77/77. Local packages,
format and freshness checks pass. Actual browser verification corrected focus,
required-state announcements and narrow RTL overflow. Performance qualification
failed and remains incomplete; no full-plan completion or measured benefit is
claimed. The final package identities and remaining limits are recorded below.

The historical passes below qualify the earlier implementation only. Revised
entry, search, workspace, definition semantics and handoff changes require new
affected-owner checks and final-package evidence. Participant, physical-device,
OS-IME and assistive-technology qualification remains open.

Documentation research used official interface and behavior facts:

- [Microsoft Blazor synchronization context](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0):
  an incomplete await permits re-entry, so asynchronous UI operations must retain
  and recheck their workspace/revision authority.
- [MudBlazor focus trap](https://mudblazor.com/components/focustrap): dialogs
  already own focus trapping; avoid a competing focus owner.
- [W3C focus visibility](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html)
  and [modal dialogs](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/):
  focused controls must remain reachable and visible within the active surface.

No third-party implementation, documentation prose or new dependency was copied.
Implementation structure follows existing repository components and state owners.
Context7 was requested and attempted through its configured MCP endpoint, which
returned `-32001 Authentication required`; successful retrieval is not claimed.
Initially the computer-use session listed no browsers and rejected both Chrome and
in-app browser creation as unavailable. These are tooling gaps, not browser QA
passes; native component checks do not replace actual surface qualification.
The user subsequently enabled Context7 and Chrome DevTools. Context7 successfully
resolved `/dotnet/aspnetcore.docs` and returned the official synchronization-context
and lifecycle documentation: re-entry/disposal can occur across incomplete awaits,
so continuations must check their current authority. Chrome DevTools successfully
listed a fresh browser page. Final surface checks must still bind new packages.

### Revision-5 focused verification

Commands use SDK 10.0.302 and `TMPDIR="$HOME/.cache/agent-tmp"`. Builds that
share outputs are serialized with `flock --close`; runtime tests remain scoped
to the named owner. The following results precede final integration and do not
substitute for the final package checks.

| Check | Executed result |
| --- | --- |
| `dotnet restore Configurator.slnx --locked-mode` | Exit 0; locked restore completed. |
| Core `DefinitionDraftArtifactTests` | 17 passed; inactive secrets are excluded from draft completion obligations. |
| Presentation `DefinitionSessionPrivacyTests` | 9 passed; retained inactive secrets are cleared by revocation. |
| Entry Browser Release build | Exit 0; zero warnings/errors, 6.50 seconds. |
| Entry StoreIndependence / DefinitionStore / SourceEntry / LaunchRoute | Separate class runs: 9 / 8 / 9 / 27 passed, zero skipped; 0.641 / 0.337 / 1.126 / 1.373 seconds. |
| Handoff Browser Release build | Exit 0; zero warnings/errors, 29.95 seconds. |
| Review / ImportComparison / ExportLifecycle / OutputSelection | Separate class runs: 11 / 13 / 8 / 6 passed; 0.960 / 4.148 / 2.284 / 1.915 seconds. |
| ConfigurationWorkflow / SecretBoundary / WorkspaceLifecycle / Localization | Separate class runs: 10 / 13 / 28 / 9 passed; 2.328 / 2.792 / 2.982 / 0.436 seconds. |
| Public enum dropdown regression | Red 2 failed; Green 2 passed, 1.790 seconds. |
| Example authoring/runtime freshness | Native `--write-examples` then `--check-examples`: agreement 3/3. |

Entry and handoff test commands used
`dotnet run --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --no-build -- --treenode-filter "/*/*/<Class>/*" --minimum-expected-tests 1`,
once for each named class. Pipe-separated class filters selected only the first
class in this environment and are not counted as combined verification. Final
small source/test changes after these runs require the integrated rebuild.

Retained failure evidence includes the original entry grouping/direct-opening/
revision-recovery failures, explicit-link admission failure, Review correction
ordering and Files correction failures, stale search/consent failures, optional
disclosure failure, and provider metadata/CLI/Developer rejection failures.
These are defect anchors, not omitted tests or weakened acceptance criteria.

### Focused November 2 continuation

The section renderer no longer constructs a second localized editor presentation
just to obtain an authored title. The field component remains the title owner;
untitled fields keep the mechanical fallback. The Configure panel now reads
root visibility from its existing immutable projection instead of reevaluating
the same predicates three times. Independent review checked locale, answer and
disclosure invalidation. The affected Browser Release build passed with zero
warnings/errors (7.35 s), and Browser passed 369/369 with zero skipped (9.528 s):
`/tmp/browser-ux-projection-build.log` and `/tmp/browser-ux-projection-Browser.log`.

Context7 and web research both retrieved Microsoft's
[Blazor rendering guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0).
The change reuses existing ownership and invalidation; no dependency, global
metadata cache or scheduler change was introduced.

The bounded six-transition diagnostic for each package is **not qualification**.
Before: Advanced 223.3/131.8/130.6 ms and Essentials 48.1/34.6/37.5 ms.
After: Advanced 195.4/98.8/114.3 ms and Essentials 40.3/31.4/35.8 ms.
First observations and all failures remain in `projection-{before,after}-diagnostic.json`
under `.tmp/browser-ux-simplification-revision5/`. No significance, percentile or
budget pass is claimed from this small sample. Five initial repeated clicks used
stale snapshot IDs and failed before producing browser events; the corrected
diagnostic acquired current IDs for every action. All recorded clicks are trusted.

The earlier 4x typing timeout remains unresolved: four focused trusted
single-character replacements each emitted exactly one input and retained the
expected text. The archived failing cohort's preceding warmup emitted five input
events, so it does not establish application text loss.
`typing-timeout-focused-probe.json` retains the focused event sequence.

The `package-projection-{free,store}` AOT pair published successfully. Runtime
`dotnet.native.ko5d1klns9.wasm` has SHA-256
`04df2bed8126ce7b225a58422c392623cf8bf431b2630dec39e77658dc64cd9c`.
Actual-package Architecture passed 77/77 in 46.865 s; format exited 0.
Native CLI/Terminal/Desktop trees were reused from the unchanged verified native
package, rather than reported as newly published. Actual browser checks on this
runtime established:

- Environment editing rejected inactive keys and malformed text, kept invalid
  source editable with Apply disabled, applied two valid public settings, and
  preserved those accepted values after cancelling a subsequent invalid edit.
  Returning to Form focused its visible section heading and retired the editor.
- Complete preparation produced one private artifact. Revocation removed it,
  disabled consent and left no synthetic secret in visible document content.
  Download was clicked; this pass did not verify its saved filesystem bytes.
- Cache inspection found no synthetic canaries in cached text, URLs or browser
  storage across 88 entries; no IndexedDB databases existed. The captured 84
  requests were GETs: 82 local asset requests and two inline data images, with
  no answer markers. Offline reload retained app availability but cleared answers,
  consent and artifacts; an uncached request rejected.
- An actual older-to-newer worker update remained waiting while a workspace held
  public and secret answers, without controller replacement or answer loss.
  Both caches (175 entries) contained no synthetic canaries. After all old clients
  left, the new worker activated, only its cache remained and its runtime loaded.
  An initial secret probe used the wrong field selector; the corrected probe
  verified `env.SETUP_SECRET`. Both observations are retained.

Evidence files are `projection-{malformed,recovery,custody,revocation,offline,update-selector-probe,update-waiting,update-activated}.json`.
The newer `package-current-{free,store}` pair additionally corrects two stale
English/Arabic consent directions that referred to controls as being “above.”
Resource-only validation passed 9/9 localization checks (193 ms), and its Release
build had zero warnings/errors (7.64 s). Runtime `dotnet.native.4p9xvc56cd.wasm`
has SHA-256 `58ae722e10ef47997212628d1033095fa48f52f0bed8f1fe568eb3024b9efbca`;
actual-package Architecture passed 77/77 (45.647 s). Actual English/Arabic copy
and RTL direction were inspected. These copy-only checks do not requalify timing.

The final lifecycle refinement returns immediately from `ConfigurationField.OnParametersSet`
when its existing unchanged-parameter fence holds. It previously skipped rendering
but still rebuilt metadata and reevaluated visibility. No new cache or invalidation
rule was introduced. Independent review checked every parameter, the consent
cascade, privacy/input revisions, projection replacement and focus generations.
`IdenticalParametersPreserveFocusUntilANewFocusGenerationArrives` guards an unchanged
refresh followed by a focus-only update without changing answers or projection.
The Release Browser build passed with zero warnings/errors (7.67 s), focused
workflow tests passed 12/12 (767 ms), and full Browser passed 370/370, zero skipped
(7.964 s). Logs: `/tmp/workspace-lifecycle-{build,targeted,full}.log`.

The final `package-lifecycle-{free,store}` publications both succeeded. Runtime
`dotnet.native.ifp4ea5emr.wasm` has SHA-256
`42a0f64d5983b009ace7e27c6bb52b31e64690dad4605fd50224efca3651724d`.
Actual-package Architecture passed 77/77 with zero skipped (91.683 s), recorded
in `/tmp/browser-ux-lifecycle-Architecture.log`. These are the latest source
packages; the earlier roots in this report remain scoped historical evidence.
The app loaded and section transitions completed in the final runtime.
`lifecycle-contended-diagnostic.json` retains six trusted transitions: Advanced
302.2/402.2/322.1 ms and Essentials 132.1/134.3/149.0 ms. Native checks overlapped
this sample and host load reached 24.93 on 16 cores; it establishes neither a
causal improvement nor regression. Do not discard it or count it as a pass.
Future timing must run after owned native checks finish, with environmental
conditions recorded. The same bounded diagnostic protocol is not a substitute
for the approved qualification cohort.

After native checks finished, the final runtime also passed a functional bulk-edit
and large-document journey. An explicit deployment selection admitted ten useful
Environment settings; Apply then switching to canonical answers confirmed all ten
independent expected typed values, the retained capability, and no protected keys.
A 279315-byte Environment document (9000 synthetic comment lines plus those ten
settings) used a clearly labeled synthetic DOM paste for fixture setup. Trusted
Preview and Apply completed in 111.1 and 267.4 ms; these are one-off completion
observations, not immediate-feedback budgets or paste/performance qualification.
The accepted port changed to 8125 while all eleven canonical values remained.
Editing it again to 8126 and cancelling preserved 8125 on reopening the source.
Evidence: `lifecycle-{bulk10,large-setup,large-actions,large-result,cancel}.json`.
Format verification exited 0 (`/tmp/browser-ux-lifecycle-format.log`), and the
final documentation check passed 363 files with zero failed batches
(`/tmp/browser-ux-final-continuation-docs.log`).

The Arabic keyboard probe on the copy-corrected package selected `keycloak` via
`k` then Enter, retained focus on the authentication field, and raised the active
required count to six; `arabic-keyboard-choice.json` retains locale/selection/focus.
`consent-copy-arabic.json` records corrected copy and RTL direction. The native
documentation link check passed all 363 files with zero failed batches before
these evidence-only additions (`/tmp/browser-ux-continuation-docs.log`).

The bounded diagnostics do not close performance, large-work latency, physical-device,
OS-IME, assistive-technology or participant-comparison gates.

### Revision-5 integrated native and package verification

The final source advances both distinct Event families to revision 6. The
example family remains `c0d02619-0134-411b-b495-ef302f592669`, with digest
`83a40fe40ecbf3d7639be76054e385c33f5b8f211e12b70e7c6f921f2f54b868`.
The provider inventory pins target evidence to Event commit
`b6ad1594b7f92ac3399a5a8bf8d241e4f8d69c66`. Historical Store revisions remain
immutable. Browser inspection found that the local Keycloak HTTP port incorrectly
followed authentication selection alone; revision 6 gates it on deliberate local
container inclusion, like its internal URL. An independent provider assertion
guards that deployment distinction.

| Final check | Physically executed result |
| --- | --- |
| `dotnet build Configurator.slnx --configuration Release --verbosity quiet` | Exit 0, zero warnings/errors, 15.88 s; `/tmp/browser-ux-final-build3.log`. |
| Complete Core suite | 362/362 passed, zero skipped, 10.265 s; `/tmp/browser-ux-final-Core6.log`. |
| Complete Presentation suite | 277/277 passed, zero skipped, 1.985 s; `/tmp/browser-ux-final-Presentation.log`. |
| Complete Browser suite | 365/365 passed, zero skipped, 8.265 s; `/tmp/browser-ux-final-Browser4.log`. |
| CLI Event provider integration | 1/1 passed, 0.870 s; `/tmp/browser-ux-final-CliProvider.log`. |
| Architecture with actual package roots | 77/77 passed, zero skipped, 118.993 s; `/tmp/browser-ux-final-Architecture.log`. |
| `dotnet format Configurator.slnx --verify-no-changes --no-restore --verbosity quiet` | Exit 0 after fixing two test import-order diagnostics; repeated after final test edits, exit 0. |
| Definition generator `--check-examples` | 3/3 current, exit 0. |
| `BuildDefinitionStore.cs --source store --output .tmp/browser-ux-definition-store --check` | Seven revisions current, exit 0. |
| Ratchet checks and editor asset check | Three ratchets current; editor closure `0494667e39ad04b311fd84b37ed53397e156020156198ee3ce1626b090664bd9`, two outputs current; `/tmp/browser-ux-exit-checks.log`. |

Suite commands execute the existing `tests/Configurator.<Owner>.Tests/` project
in Release with `--no-build`; the CLI run selects `SetupCliEventProviderTests`.
Earlier failed integrated runs remain in `/tmp/browser-ux-final-Browser.log`,
`Browser2.log` and `Browser3.log` with the same prefix. Corrections align synthetic
test setup with explicit optional disclosure, output ownership and masked choice
replacement. They retain the tested workload and privacy assertions.

The native release owner published CLI, Terminal and Desktop for `linux-x64`,
plus AOT Browser with and without the Store. All five commands exited 0. Local
roots are `.tmp/browser-ux-simplification-revision5/package-free` and
`package-store`; native trees are identical in both. Both Browser packages contain
`dotnet.native.mgebmg8vqv.wasm`, SHA-256
`f6731e16561c6ee52c307a653f2f98fd3a980f9105b76429fb950f768d4a2d66`.
The final Browser tests use local origins 5197 (Store) and 5198 (store-free).
These are local package checks, not publication or actual-host certification of
additional native platforms. Logs are `/tmp/browser-ux-package-{cli,terminal,desktop,free,store}.log`.

### November 2 pointer-focus correction

The first six final-package timing cohorts retained 480 measured samples,
60 warmups and 12 first observations. Section feedback/content limits failed at
both CPU rates; Review/Files entry was faster but Configure feedback still had
outliers. Raw records are in `pre-focus-normal-partial.json`. No failed sample
was removed. Four subsequent Developer first/warm observations are also retained.

Developer-to-Form pointer activation then exposed a deterministic functional
failure. Captured pointer-down targeted Form; pointer-up/click targeted its
surrounding `details`. The fallback workspace heading had been visually hidden
until focused; it occupied 55.5 px while focused and collapsed during Form's
pointer-down focus transfer. This moved the button before pointer-up. The
two-click reproduction completed in two seconds and failed the assertion that
one Form click changed mode. `developer-return-pointer-red.json` retains targets,
coordinates and the original timing observations. Earlier waits without an
observed action were instrumentation failures, not completed latency samples.

`ConfigurationEditor` now resolves Configure focus to the mounted Form section
or Developer heading, using the same disposed/current-view checks as the other
panels. The hidden workspace label is not focusable and no longer changes layout
on focus. The visible Developer heading uses the existing focus-ring tokens.
`EditorModeFocusTargetsTheMountedPanelWithoutChangingAnswers` failed 1/1 against
the old target, then the complete Browser suite passed **366/366**, zero skipped,
20.045 s. Its Release build passed with zero warnings/errors in 11.82 s. These
logs are `/tmp/browser-ux-focus-{red,red-build,green-build,Browser}.log`.
The rebuilt AOT pair passed actual pointer Green: two complete round trips
retained the Form/Developer button as all three pointer targets and focused the
visible panel heading. Raw evidence is `developer-return-pointer-green.json`.
Those packages used `dotnet.native.gkgmgpcttu.wasm`, SHA-256
`a0ee7481132040bedbc62f1df898afc19ec03ac36a7092892d232f40b7172dc7`;
their actual-package Architecture run passed 77/77 in 52.057 s and format exited 0.
They are superseded by the accessibility correction below, not silently treated
as the final source's package proof.

### November 2 required-control accessibility correction

Actual keyboard type-ahead (`k`, Enter) selected canonical `keycloak`, retained
focus on its control and changed required work from two to six answers. All five
output modes remained reachable; Manifest, Tenant, Operator and Legal had distinct
headings and no raw zero labels or horizontal overflow. Arabic Legal rendered
`lang=ar`, `dir=rtl` without overflow. These observations are retained in
`focus-output-rtl-observations.json`.

Inspection then found required inputs announcing `aria-required=false`, despite
their correct visible requirement. The Arabic Legal metadata-name assertion is
retained in `required-control-red.json`. MudBlazor's input and radio group own that
attribute through `Required`; splatting it alone did not override the default.
The existing text wrapper now forwards `Required`, and the field renderer supplies
it to text, enum and boolean controls. The enum wrapper also compares requiredness
before suppressing a parameter render. Radio `aria-invalid` still follows the
shared validation result; no new validation owner or private-value copy was added.

The installed MudBlazor 9.7.0 XML API, official
[input API documentation](https://www.mudblazor.com/api/MudBaseInput%601) and
Context7's version-pinned component guidance informed the parameter choice;
the rendered regressions establish its actual behavior. No upstream source was
copied. Three dependency-driven control cases verify required/invalid transitions
without extra answer edits. Masked required choices also pass blur, replacement
privacy and retired-callback checks. The complete Browser suite passes **369/369**,
zero skipped, 7.800 s (`/tmp/browser-ux-accessible-Browser2.log`).

Initial test-fixture attempts incorrectly used `RequiredOverride` in place of
shared requiredness, then omitted the declared relationship capability; those
failures and a mistaken test API call are retained in `/tmp/browser-ux-required-*`.
They are test-setup failures, not additional product defect evidence. The boolean
cohort independently reproduced the same required-state mismatch and retained an
explicit group invalid-state assertion. The accessibility packages passed
actual-package Architecture 77/77 in 49.970 s and format exit 0. Published
Space activation of focused consent deliberately enabled entry; Setup secret
then exposed `aria-required=true` and `aria-invalid=false`. Arabic Legal metadata
also exposed the correct required state.

### Final scoped reflow correction and package identity

At 320 CSS pixels, Arabic Legal revealed unbroken labels and metadata paths
overflowing 30 field containers. Mobile `innerWidth` expanded to 332, while
`documentElement.clientWidth` remained 320: comparing scroll width to inner width
would miss this failure. A scoped wrapping probe reduced document scroll width
to 320 and field overflows to zero; `rtl-reflow-red-probe.json` retains both
observations. The owning isolated stylesheet now sets `overflow-wrap:anywhere`
on `.configuration-field`, without clipping content or changing authored text.

Published Green is `rtl-reflow-green.json`: Arabic/RTL, client and scroll widths
both 320, zero field overflows, required metadata input correctly announced.
Final package roots are `.tmp/browser-ux-simplification-revision5/package-final-free`
and `package-final-store`; both native release-owner publications exited 0. Their
actual-package Architecture run passed **77/77**, zero skipped, 59.999 s.
The prior native suites and 369/369 Browser pass remain current: this final
correction changes isolated CSS only.

Both packages have native runtime `dotnet.native.a3cvgs38rb.wasm`, SHA-256
`0a817f3a29df7bc7923e6cee7c523e4fe8714393485ecdada2f37020ee60710e`.
The isolated CSS bundle SHA-256 is
`5ded9a8b6755632091e10ff0554aa525aa0358442b7d63ebf9569c75a25d3774`.
Service-worker asset manifest SHA-256 is
`7c01b8524aee3ab8bf1684c38cc8bdf612342176f960f0002eea1bdb55170f7a`
without Store and
`5e4adef93df80a386c4ecb5a655d55ec4dc0c5c98099a62b2386d01abcdeec90`
with Store. CSS/manifest identities matter because wrapping did not change
native WASM. Final local origins are 5203 and 5204.

The final with-Store catalogue has two project groups, retaining seven Event and
two minimal-service source choices. The included Event action opens its admitted
revision directly. Keyboard Tab/Enter selected the `setup` result and focused
unchecked consent; no consent or answer mutation preceded Space.

### Final timing outcome and remaining qualification

The settled `a3cvgs38rb`/wrapped-CSS package retained **720 measured actions**,
96 warmup records and 20 first observations. The nine complete directional pairs
have 40 measured samples per direction and rate. Every measured record has trusted
input and finite, nonnegative, ordered clocks. The intended full normal cohort
requires 960 measured actions and was not completed. Raw data is
`final-normal-partial.json`; the native jq audit and directional summaries are
`final-normal-partial-summary.json`. Earlier cohorts and all slow samples remain.

At 1x, Advanced feedback maximum was 51.5 ms and content p95 168 ms; Essentials
feedback maximum was 50 ms, which fails the strict below-50 requirement. At 4x,
Advanced feedback maximum was 288 ms and content p95 909.6 ms. Other completed
cohorts also retained failures. No budget or percentile definition was relaxed.

A separate 34-field mount diagnostic and Chrome INP summary did not establish a
bounded source correction. Chrome reported 80 ms INP, split into 0.5 ms input
delay, 66 ms processing and 14 ms presentation. These standard INP figures do not
replace the stricter task budgets. Chrome's configured workspace permissions
rejected raw trace-file export; the mount diagnostic and tool summary remain.
A temporary following-task-yield browser probe did not materially improve
completion; it was restored, with no scheduling change committed to source.

The scalar measurement setup initially replaced a full URL through `type_text`,
which emitted 17 input events. That 1,197.7 ms observation remains separately in
`ordinary-scalar-full-replacement-probe.json`. The normal scalar cohort instead
uses one trusted character replacement per action; every completed 1x scalar
record has exactly one input event. The 4x scalar cohort stopped during warmup
with a retained `content-timeout` (feedback 300.4 ms); its cause is unresolved.
No 4x scalar measured samples or final short-choice cohorts are claimed.

After the user's status challenge, repetitive timing work ended. The task-owned
browser was returned to 1x CPU and a fresh catalogue, clearing the synthetic
workspace. Further work should diagnose the failed performance paths and the
scalar timeout before repeating qualification. Final-package large Preview/
Apply/cancel cohorts, update/worker custody checks, native browser zoom and any
remaining full journey scenarios are not certified by these partial results.
Earlier scoped functional observations remain evidence only for their named
assets. Physical devices, OS IME/AT and the 8–12 participant comparison remain
unperformed. All corresponding task and I-VSD qualification gates stay open.

### Earlier revision-5 package functional observations

Source review retains the existing ownership boundaries: definitions declare
choices and activation; Core validates/composes; Presentation owns accepted
answers and generations; Browser renders and performs deliberate exports.
The new dropdown uses the existing `AppSelectField`. For a masked choice it
offers public replacement options without reflecting the accepted selection,
then replaces the control and fences its retired callback. The two sensitivity
variants in `BrowserMaskedChoiceTests` cover selection privacy, replacement,
retired callbacks and revocation. No new service, dependency abstraction or compatibility adapter was
introduced. The tooling lockfile's additional linux-x64 compiler target was
generated by native publication, not edited to satisfy a gate.

The independent implementation review's findings on conflicting directory
metadata, import-format synchronization, correction reopening and stale search
callbacks were corrected before the integrated checks. The final masked-choice
and mobile-order changes received parent review and regression verification;
there was no second independent review of those last changes after agent quota
exhaustion. Actual target acceptance remains outside this workstream.

Before the later focus/accessibility/reflow corrections, Chrome DevTools exercised
the `mgebmg8vqv` AOT package with synthetic answers only.
At 390 × 844, the first ordinary input occupied y=659.8–703.8 and the document
had no horizontal overflow. Essentials began at y=466. The attention count was
one, without counting a deferred requirement twice. The mobile `setup` search
result owned its center hit target and moved focus to `secret-consent`, leaving
consent unchecked and the Configure panel active.

After explicit consent, a synthetic endpoint and setup secret produced a
complete `event.env`. Download worked without Copy or Reveal. A local Blob probe
observed 111 bytes with the synthetic values. Revocation removed prepared files
and secret entry authority. A subsequent deliberate draft download was 354 bytes,
retained the public endpoint, excluded the secret and identified the unresolved
setup-secret obligation. No synthetic value appeared in the 87 inspected cache
entries' text assets/URLs, localStorage, sessionStorage or resource URLs; no
IndexedDB database was present. This scan does not claim a byte scan of binary
assets or browser-extension custody.

Offline reload loaded the cached application and cleared answers and consent.
An uncached fetch probe rejected under offline emulation even though
`navigator.onLine` reported true. Trusted click/type input subsequently accepted
the full synthetic endpoint offline and retained focus. Earlier DevTools `fill`
timeouts left partial text and are retained as tool failures, not successful
input evidence. Debug previews on ports 5195/5196 are preliminary evidence only.

## Historical revision-3 status and evidence limits

The task-aware definition, single-rail workspace, direct controls, Review/Files
recovery and local Developer editor are implemented. Form and Developer share
one accepted answer workspace; document Preview/Apply is bounded and atomic.
The reviewed local editor closure, consent and retirement boundaries have scoped
native and actual-browser evidence below. Native/package verification passes.
The current Apply-corrected artifact passes Browser 345/345 and actual-package
Architecture 77/77. Its complete 960-sample normal cohort and targeted 1x
Cancel/Reopen recheck pass after correcting the task-owned renderer's observed
`SCHED_IDLE` policy to `SCHED_OTHER`. No product source, sample count or budget
changed for that requalification. Earlier failed cohorts remain below and in raw
evidence; earlier passing artifacts do not qualify this artifact by substitution.
Bounded large-work feedback passes, with completion times reported separately.

This is workstation evidence, not a participant study, assistive-technology or
physical-device certification. The nine-task comparative evaluation and all
uncovered acceptance scenarios remain open. No benefit percentages are claimed.
No Git commit, publication or deployment is implied by local validation.

## Direct editing

Browser uses the shared form's explicit Direct visibility policy. Ordinary
eligible inputs mount immediately; activation, selected experience, edit
authority and secret consent still govern them. Other clients retain Authored
visibility. Short unmasked choices use controlled typed radio groups with a
distinct absence identity. Accepted answers, invalid drafts and preparation
authority remain in their existing owners.

### Native verification

All commands used SDK 10.0.302 with `TMPDIR="$HOME/.cache/agent-tmp"`.

- Presentation scalar baseline: 29 passed. The direct-policy visibility Red
  failed 1/3; the Browser direct-control Red failed 2/2. Fixture admission errors
  were repaired before recording these Reds.
- Affected Presentation policy/presence/experience/collection check: 52 passed.
  The final direct-policy class passed 4/4, including selected-experience and
  denied-secret invariants.
- Browser isolated classes passed navigation 5/5, typed privacy 8/8, secret
  boundary 13/13 and incremental mount 9/9. The repaired experience/direct-edit
  cohort then passed together, 13/13. Final radio-target changes passed 2/2.
- `dotnet build src/Configurator.Browser/Configurator.Browser.csproj --configuration Release --verbosity quiet`
  exited 0. Isolated Release AOT publication exited 0.

The experience guidance fixture previously constructed a new admitted definition
inside its layout render fragment, causing a CPU loop before initial render.
Capturing one definition, as the passing workspace-inspector fixture does,
passed the same inert-guidance/link assertions. No production help workaround,
test skip or weaker assertion was introduced.

### Actual browser behavior

The final AOT package was served locally from
`.tmp/browser-ux-simplification/direct-controls-final-aot/wwwroot`.
Native Chrome inspection verified:

- Ten distinct scalar edits required zero per-field Configure clicks; ten
  distinct short-choice selections required zero choice-menu opens.
- All ten Event capability sections were reachable. One workspace search owner
  found an obscure advanced setting and focused its active input directly.
- Radio accessible names were human English/Arabic text, not element IDs;
  normal label hit areas measured 48 CSS pixels high.
- Arrow-key selection moved focus and changed activation; the inverse selection
  restored ordinary fields. Locale keyboard selection reached Arabic/RTL.
- A 375 CSS-pixel Arabic capture had no horizontal overflow or overlapping
  controls. Acquisition instructions appeared inline; technical host
  descriptions remained available in help.
- A synthetic CDP Arabic composition/commit preserved exact text and focus.
  This does not establish OS IME or assistive-technology behavior.

### Focused timing cohort

Chrome for Testing 153.0.8010.52, Linux x64, actual viewport 1280 by 813 CSS
pixels. Canonical definition SHA-256:
`2f136c5e3a4de673fb6ae3cae1defcb676befa8cbe26f909a2253c036eaa5eef`.
Published service-worker asset-manifest SHA-256:
`889ec61cb775d22357d6ac699c17c19099c1b28682106ee7de3d437a3b1ddb01`.

Each direction/rate retained its first invocation, five completed warmups and
all 40 measured samples. Feedback is captured trusted input/click dispatch to
the first animation frame. Final content requires the expected active state,
completed selected-section mounting and two animation frames. Long-task
attribution includes tasks intersecting dispatch through final content,
including tasks beginning before dispatch. Percentiles use nearest rank.

An unrelated native test was active at cohort start. No parent or Core-child
build, test or publish ran during measurement. These results describe that
observed workstation load, not a quiescent machine or a matched comparison.

| Journey | CPU rate | Samples | Final p50 / p95 / maximum, ms | Maximum feedback, ms | Intersecting long tasks |
| --- | --- | --- | --- | --- | --- |
| Advanced section | 1x | 40 | 99.2 / 99.4 / 99.5 | 16.1 | 0 |
| Basics return | 1x | 40 | 31.7 / 32.0 / 57.9 | 32.0 | 0 |
| Direct text | 1x | 40 | 31.9 / 32.2 / 43.3 | 42.8 | 0 |
| Platform off | 1x | 40 | 32.4 / 34.5 / 35.3 | 34.5 | 0 |
| Platform on | 1x | 40 | 32.8 / 36.6 / 42.2 | 41.5 | 0 |
| Advanced section | 4x | 40 | 164.2 / 195.5 / 198.1 | 70.0 | 7 |
| Basics return | 4x | 40 | 39.4 / 49.0 / 70.1 | 64.7 | 2 |
| Direct text | 4x | 40 | 109.1 / 128.4 / 141.3 | 136.2 | 40 |
| Platform off | 4x | 40 | 116.1 / 166.8 / 204.0 | 182.1 | 40 |
| Platform on | 4x | 40 | 123.8 / 149.7 / 201.7 | 197.5 | 40 |

These focused measurements satisfy feedback below 50 ms at 1x and 200 ms at
4x, no measured warmed 1x long task, and the new section-transition p95 target
below 200 ms at 1x. The 4x final-content maxima above 200 ms and every 4x long
task are retained; final completion is not mislabeled as feedback. First 1x
advanced opening was 97.8 ms final / 24.2 ms feedback; first direct text was
44.7 / 43.9 ms. A first invocation at 4x is not cold application startup.

Raw first-use, warmup, measured, Event Timing and long-task observations are
retained locally in
`.tmp/browser-ux-simplification/direct-controls-performance.json`.
This ignored task evidence is not a portable published artifact. No scale,
Developer, complete nine-task comparison or participant gate is closed by this
cohort.

## Review and Files

Review now distinguishes ordinary blockers, secret-deferred drafts and complete
preparation. Its counts and inventory use the shared value-free projection.
Required corrections and positive-effect changes both retain an explicit return
route. Files retains the editor's preparation/export authority; the mounted file
component owns only its element references, accessible actions and local status.
The loss guard queries actual work rather than treating every workspace as dirty.

### Native verification

The settled Release build completed with no warnings or errors. The following
affected Browser cohort passed **95/95**, exit 0:

```bash
dotnet build src/Configurator.Browser/Configurator.Browser.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --treenode-filter "/*/*/(BrowserConfigurationReviewTests|BrowserConfigurationWorkflowTests|BrowserExportLifecycleTests|BrowserNavigationStateTests|BrowserLocalizationTests|BrowserTemplateReadLifecycleTests|BrowserWorkspaceLifecycleTests|BrowserSecretBoundaryTests)/*" --minimum-expected-tests 1 --no-progress
```

The seven original Red cases cover readiness, local Copy recovery, meaningful
loss confirmation and stale confirmation/output. Actual Browser inspection
found two additional defects in the existing readiness scenario: optional
absence was incorrectly inferred from validity, and required corrections did not
set the existing return context. Each failed independently at runtime, exit 2,
before correction. The settled cohort includes both stronger assertions.
Optional absence now uses `IsProvided`, not `IsReady`; required corrections reuse
the already-fenced Review change route.

The lifecycle fixtures retain one admitted definition instance across layout
renders, and panel lookup/dispatch runs as one renderer action. These changes
remove fixture-created definition replacement and stale-handler races without
weakening the privacy or concurrency assertions.

### Actual browser behavior

Two isolated Release AOT packages published with exit 0. The initial completion
package exercised real clipboard denial, download, Reveal/Hide, clear
confirmation and Arabic phone layout. The final package,
`review-files-final-aot`, contains the two Review corrections and rechecked the
affected paths plus the Files-exit matrix. Earlier File-action evidence remains
scoped to the unchanged File/export owners.

- Fresh Event Review was secret-deferred. The corrected final summary counted
  **34** active optional settings without a value, rather than the erroneous
  zero. Complete preparation was not offered as its primary action.
- Required-secret correction focused consent without enabling it or mounting
  that secret input. Explicit consent and synthetic masked input survived
  **Return to Review**, which became complete-ready with no corrections.
- A public synthetic definition was admitted through the actual local file
  chooser and reviewed-source opening action. Missing ordinary inputs produced
  blocked Review with neither preparation primary. A malformed hostname
  remained blocked, was absent from Review text, and could be corrected through
  the exact input. Selecting the conditional controller's cluster choice then
  activated the required replicas correction: readiness changed from
  secret-deferred to blocked, with **2/4** required answers ready.
- Draft and complete preparation produced their respective readiness states and
  focused Files. Output preview text remained empty until deliberate Reveal;
  Hide cleared it again.
- A real origin-scoped clipboard permission denial produced file-local recovery
  while Download stayed available. Download created a completed, nonempty
  `event.env` of **317 bytes** in ignored task evidence; its contents were not
  printed. This is Browser download evidence, not native protected-save
  certification.
- Cancelling the actual native clear confirmation retained the draft. Accepting
  clear on a complete workspace retired all files, preview nodes and masked
  secret controls. The QA runtime did not deliver the attempted raw dialog
  event subscription; explicit CDP dialog handling completed these real dialogs.
  No mocked clipboard outcome is presented as this native denial.
- At **390 x 844**, Arabic complete Files used RTL layout and localized actions
  without horizontal overflow or private preview text. Desktop inspection used
  **1280 x 900**.
- In the final package, leaving revealed Files removed private preview DOM.
  Returning retained the parent complete bundle but mounted an empty hidden
  preview. Consent revocation subsequently removed the files and preview nodes.

Public Browser guidance and the Browser/Presentation ownership anchors describe
these mechanisms and their limits. They do not promote this bounded cohort into
the complete comparative, device or assistive-technology qualification.

## Shared atomic document editing

The Presentation API now owns one disposable public-scope document session and
opaque candidate per form. Canonical source uses the existing answer envelope;
Environment source uses Core's captured editable mapping. Parsing constructs a
detached replacement, and Apply rechecks its full authority before one accepted
payload transfer. Stable field facades survive that transfer. Missing required
work is allowed; invalid supplied content is not.

The field-payload migration passed the existing **216/216** Presentation cases
both before and after that structural change. After the document implementation,
the Release Presentation build exited 0 and the following selected runs passed:

- **60/60** `DefinitionDocumentEditingTests`.
- **132/132** selected presence, collection, container, direct editing, import,
  private clear, review, scalar, privacy, typed-editing and Event form cases.
- Parent verification: **50/50** discovery, experience projection, prepared
  adoption and presentation operation cases.

These are separate retained runs on the relevant slices, not a new single full
276-case owner run. The settled full-owner/workstream gate remains required.

The source introduced runtime Red before its boundary corrections:

| Boundary | Red result | Exit |
| --- | --- | --- |
| Cancellation and invalid supplied-source admission | 40/42 passed | 2 |
| Changed imported container with untouched sibling provenance | 0/1 passed | 2 |
| Cancelled Apply candidate revival | 6/7 passed | 2 |
| Unchanged dormant canonical roundtrip | 53/54 passed | 2 |
| Invalid artifact request replacing an existing lease | 59/60 passed | 2 |

The completed 60-case class includes these regressions. It also exercises
false/null/empty/reset/omission, dotted identities, protected scope, source and
candidate retirement, one revision/event, retained facades and row identities,
partial requiredness, exact merge/replace semantics and observer/reentrant
commit behavior. No timing sleeps, private reflection or weakened assertions
were used for these behavior cases.

Executed command forms:

```bash
dotnet build src/Configurator.Presentation/Configurator.Presentation.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj --configuration Release --no-build --treenode-filter "/*/*/DefinitionDocumentEditingTests/*" --minimum-expected-tests 1
dotnet test --project tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj --configuration Release --no-build --treenode-filter "/*/*/(DefinitionDiscoveryTests|DefinitionExperienceProjectionTests|DefinitionPreparedAdoptionTests|DefinitionPresentationOperationTests)/*" --minimum-expected-tests 1 --no-progress
```

The Core codec/projection evidence remains its **88-case** retained cohort plus
the corrected **13-case** projection class; it was not rerun or relabeled as a
fresh combined 101-case result. Public/technical contracts and the
[document custody ADR](../adr/ADR-browser-developer-editing.md) accompany the
implemented seam. Link validation passed eight files and 106 local links.

Native-generated local release fragments record the captured document/codec
contract and atomic application separately:
`CHG-01M6A8HETGEPA1KRQHC854VEQ3` and
`CHG-01M6A8VTC7GH7MT6YP8AY8KDT0`. They have not been staged or committed.
The YAML language server is unavailable; no unapproved editor-tool dependency
was installed to replace the native release tooling.

This proves the shared API, not an integrated Browser Developer surface. The
Browser must still fence its own visible model/version, acquire source only
after consent, preserve text on Apply, resolve dirty transitions, and retire
JavaScript/private state on every required lifecycle exit.

## Developer integration source evidence - November 1, 2026

The local Developer panel now uses the shared document session over the existing
accepted Form workspace. Its bridge owns one private editor slot and transfers
only version/dirty/history-limit metadata during typing. Preview reads a bounded
UTF-8 transfer; the managed/session and JavaScript copies are cleared after use.
Atomic Apply preserves the current text/history and reopens document authority.
Form/Developer, Review and application navigation resolve pending text through
Apply/Discard/Stay before unmounting or separately confirming accepted-answer
loss. Public coverage remains available without consent.

`ConfigurationDeveloper` and its bridge are implemented source, not a qualified
running engine. Native Release compilation and **38 Developer component cases**
passed, including six root Form/Review transitions and three application
navigation decisions. **13 navigation-owner cases** passed separately. The
external editor alone is replaced in those component tests; document parsing,
scope, candidate application, consent, revisions and root navigation are real.
These checks do not prove CodeMirror exports, real DOM editing, IME, history
budgets, asset payload or offline behavior.

The native evidence includes failures rather than only the final pass:

- The two new navigation boundaries initially failed at runtime, exit 2: Stay
  still reached whole-workspace confirmation, and discarding only pending text
  prompted for an untouched Form. The navigation owner now awaits its pending
  resolver, checks the registration lease, then captures the updated work
  generation before its separate loss confirmation.
- The standalone panel's original consent/late-callback Red was 0/2. Its first
  complete run had 17 passed and 11 failed because the test boundary returned a
  Boolean to a void unfreeze call. Correcting that mock contract produced 28/28.
  The integration tests then exposed a real consent-enable render loop: the root
  rendered before the panel's input notification, and an obsolete session kept
  scheduling failed mounts. Those native runs were terminated; they are not
  passes. The panel now waits for current document metadata before mounting.
  All six root transitions pass with that gate.
- The new no-satellite resource case failed at runtime, exit 2. Both Developer
  languages now ship in the main assembly and use invariant resource lookup,
  matching the existing Browser localization contract. That case is included
  in the final 38/38 run.
- Three actual package cases failed at runtime, exit 2: both existing published
  variants lack the new editor assets, and native freshness checking rejects
  missing approval with `editor-approval-required`. No package pass is claimed.
- The native asset owner's synthetic offline self-test exposed accepted
  toolchain tampering, exit 1. Checking manifest toolchain metadata against the
  approved recipe fixed it: **10/10 checks** now pass, including nine deliberately
  rejected mutations. This uses disposable synthetic authority and no network,
  dependency installation or real approval.

Executed source checks:

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release -- --treenode-filter "/*/*/BrowserNavigationStateTests/StayingWithPendingDocumentPreventsWorkspaceLossConfirmation|/*/*/BrowserNavigationStateTests/DiscardingOnlyPendingTextDoesNotPromptForAnUntouchedForm"
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release -- --treenode-filter "/*/*/BrowserNavigationStateTests/*|/*/*/BrowserDeveloperEditingTests/*"
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release -- --treenode-filter "/*/*/BrowserDeveloperEditingTests/*" --minimum-expected-tests 38 --timeout 20s
TMPDIR="$HOME/.cache/agent-tmp" dotnet build src/Configurator.Browser/Configurator.Browser.csproj --configuration Release --verbosity quiet
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release -- --treenode-filter "/*/*/BrowserEditorPackageTests/*"
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --self-test
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release -- --treenode-filter "/*/*/BrowserEditorPackageTests/NativeEditorAssetOwnerRejectsTamperingOffline" --minimum-expected-tests 1
TMPDIR="$HOME/.cache/agent-tmp" dotnet msbuild src/Configurator.Browser/Configurator.Browser.csproj -target:VerifyBrowserEditorAssets -property:Configuration=Release -verbosity:minimal
```

The first command is the 0/2 navigation Red; the second selected **only the 13
navigation cases**, not both classes. The third is the final 38/38 Developer
Green, and the Release build exited 0. The architecture class command is the intentional package Red
described above. Do not infer combined owner-suite coverage from the filter
union. C# diagnostics for the panel, navigation owner/tests and package tests
are clean; Razor language-server support is unavailable, so native compilation
is the source gate.

The native self-test and its separate architecture case exited 0. The explicit
publish gate exited 1 while approval/assets are missing, as required; it did not
publish or install anything.

The native asset builder's README documents metadata-only review, exact approval
bindings, generation and offline checking. Browser publication now calls that
offline check and requires the editor bundle, manifest and original terms.
The service worker admits only those four fixed public integration assets;
actual published cache/network qualification remains open. At this source-only
checkpoint, the 17-package closure still awaited its documented disposition and
no dependency tree or vendor output had been installed/generated. The subsequent
decision and generation below supersede that pause.

Source-host visual checks, not a published-package qualification, inspected the
consent-denied panel at 1280x900 and 375x812, including native keyboard language
switching to Arabic/RTL and representation switching to Environment. No
horizontal overflow, editor DOM, editor-module requests or satellite requests
were observed. Canonical coverage showed 285 public editable units; captured
Environment coverage showed 27 editable mappings. These are metadata counts,
not proof of bulk-editing runtime behavior.

That inspection caught a misleading first-use retirement status. An added
machine-status assertion failed at runtime, exit 2; initialization now reports
`developer-consent-required` and uses a short localized waiting status. The final
38-case run includes that correction. A rebuilt source host must be restarted
before fresh-page QA: its old static asset identities produced a failed reload,
which was not treated as a product or package pass.
The settled source surface was inspected again after the correction at desktop
and Arabic mobile widths. Its consent-review action focused the existing
checkbox without checking it or creating an editor. Temporary source hosts and
their QA pages were closed after inspection; earlier frozen comparison packages
remain separate evidence.

## Source-only continuation while closure approval is pending

The additional
`StayingWithPendingTextRestoresTheVisibleRepresentationChoice` case exercises the
native selection change before the Blazor event, then chooses Stay with dirty
text. It passed 1/1 against unchanged production source: the visible Canonical
choice, pending text, dirty state and single editor instance remained intact,
and accepted answers stayed empty. This is a baseline guard, not a reproduced
defect or a claimed engine/browser pass. The prior 38-case pass is retained; no
fresh combined 39-case run is claimed.

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --treenode-filter "/*/*/BrowserDeveloperEditingTests/StayingWithPendingTextRestoresTheVisibleRepresentationChoice" --minimum-expected-tests 1
```

The proposed editor lock also passed a metadata-only `npm audit
--package-lock-only --ignore-scripts --json --registry=https://registry.npmjs.org`
on November 1, exit 0, reporting zero known vulnerabilities and 17 total
dependencies. Native hashes confirmed the reviewed manifest and lock were
unchanged, with no installed `node_modules`. The dependency record retains this
dated result without treating it as the missing closure approval.

## Reviewed local asset generation and first real activation

The initial Steward-only approval pause was an added interpretation, not a
requirement of the authoritative dependency policy. The implementation owner
recorded a technical dependency decision within the approved workstream after
independent review of all 17 original MIT license texts. No user approval of a
specific hash is asserted. The decision preserves original notices and component
identity and found no unclear-rights or compatibility condition requiring legal
escalation. The unanswered participant/device resource question remains
unanswered, not evidence of availability or completed qualification.

Native production resolution and archive/license review reproduced closure
`0494667e39ad04b311fd84b37ed53397e156020156198ee3ce1626b090664bd9`.
The production lock is
`fe664dfbbf3eeac5f530e172ef7125fefced26ae3b016cfef142fe838217a6bd`.
Two native generations exited 0 and all three output hashes matched:

| Output | Bytes | SHA-256 |
| --- | ---: | --- |
| `configuration-editor.js` | 415601 | `702060e50391f18d4bf92bfeb2a91af6f8927e490f880ddb8da59b633ba8249b` |
| `configuration-editor.LICENSE.txt` | 19819 | `281fa74258cf5b84111aa2f77a749ac06de39ac4d3c34c3eb4e1e3d03f248d5d` |
| `configuration-editor.manifest.json` | 9857 | `dd4841635973e0ab82cbc45bfe10065646d9cc498433a344f6b0a67991967f59` |

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --write "$PWD"
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --check "$PWD"
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --self-test
TMPDIR="$HOME/.cache/agent-tmp" dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .
TMPDIR="$HOME/.cache/agent-tmp" dotnet build src/Configurator.Browser/Configurator.Browser.csproj --configuration Release --verbosity quiet
```

The second generation, offline freshness, all ten hostile-input checks, license
policy and Browser Release build exited 0. Original terms remain in the generated
notice file and the component manifest supplements the NuGet/runtime inventory.

A fresh Chrome-backed source host loaded the actual bundle, not an editor mock.
Before consent there were zero editor nodes and zero editor-module requests.
An observer armed before the native consent action measured first ready-editor
state at **1223.7 ms**; this includes consent transition and first activation on
the non-published source host and is retained as first-use cost, not a warmed
percentile or AOT package claim. The bridge and bundle resource durations were
2.5 ms and 3.1 ms; the bundle's decoded resource size was 415601 bytes.
Exactly one real editor mounted, with no visible app error or horizontal
overflow at 1280x900.

Native text input then produced an invalid synthetic document. Actual Preview
returned one value-free diagnostic, kept the complete pending text and the same
editor, and left Apply disabled. No synthetic sentinel appeared in diagnostic
text. Complete typed Apply, history limits, published custody/offline operation
and the nine-task qualification are still required.

The real native representation selector then exposed a gap that the bUnit
baseline did not reproduce. Choosing Stay after requesting Environment left
Canonical scope and pending text intact, but the visible native choice still
said Environment. The browser assertion failed with
`representation-stay-visible-choice-mismatch`. The select now keys on the
existing transition generation, without a second selection model. A full
39-case Developer run passed after this correction. The exact native browser
recheck then passed: Canonical remained selected, pending text and the single
editor survived, and focus returned to its content. No second selection state
was introduced.

Before closing that pre-fix page, an observer on the consent checkbox ran after
the bridge's capture listener and before managed event bubbling. It recorded
zero editor nodes and zero source hosts on revocation; the normal rendered
consent-denied state also contained no editor. This is actual source-host
retirement evidence, not a claim of physical string erasure or published-cache
qualification.

## Real Developer editing progress

A second fresh source host measured **567.6 ms** from the native consent-change
event to ready editor state. This is a different, more precise timing boundary
than the earlier pre-gesture 1223.7 ms observation; both are retained, neither
is a warmed percentile or published/AOT qualification result.

Real native undo and redo restored the clean answer envelope and pending invalid
text respectively. Returning cleanly to Form removed the editor without a dirty
decision. The redo probe initially used the wrong CDP modifier bit; correcting
the probe to Control+Shift verified the actual binding. No editor defect is
inferred from that driver error.

The operator deliberately selected **Include basic platform settings** in Form,
then opened captured Environment editing. Its 27 editable public mappings were
read through the paged coverage UI. Ten useful synthetic API, authentication,
authorization, managed-provider, MCP and public-address settings produced ten
Preview changes with no diagnostics. Actual Apply succeeded, retained the text
and editor, and a fresh canonical representation exposed the exact admitted
values: two booleans and eight strings under literal dotted answer keys.
Returning to Form preserved both the capability selection and ordinary values.
The initial inspection probe incorrectly assumed nested keys; correcting that
probe confirmed the actual literal-key contract, not a product data-loss defect.

The required masked `SETUP_SECRET` was then supplied using a synthetic 64-character
fixture. Required readiness reached 2/2. A newly opened real Developer source
contained the ten public values but neither the protected field name nor its
synthetic value. No secret value is recorded here.

Explicit JSON Format produced 16 lines, retained ten values and their Boolean
types, and did not reveal the accepted secret. Preview remained present after
switching the workspace to Arabic. That transition exposed another real gap:
the editor's accessible label changed, but its explicit `lang` stayed `en` and
the open search panel retained English Find/Replace controls. The browser
assertion failed with `developer-editor-locale-metadata-stale`. Localized public
metadata must be reconfigured without replacing text/history or invalidating
Preview. The expanded preference regression first exposed an invalid test
localization catalogue; adding an admitted English-default/Arabic translation
catalogue fixed that fixture without changing admission. It then failed at the
actual boundary because no translated phrase dictionary was supplied, exit 2.
After the correction, the focused case and full 39-case Developer class passed.

The bridge now passes localized public phrases into the engine's owned metadata
compartment, which updates content language and attributes without a document
transaction. Public search-panel commands refresh an open panel and restore
connected focus; Core diagnostics are reapplied after metadata reconfiguration.
Only installed public API declarations were inspected for these commands.
Native regeneration and offline freshness passed for the updated 416109-byte
bundle, SHA-256
`2a305e1380492834a8172e59f50c4afc36376db9d291af93b32cc3206ef7bff3`.
The exact closure and original terms are unchanged. The actual browser recheck
preserved exact pending source, Preview authority, the search query and external
locale-control focus while updating the content language and Find label to
Arabic. Undo and redo remained functional; a subsequent canonical Apply
succeeded without replacing the editor.

That recheck also exposed untranslated secondary search controls and the
completion-list accessible label. The supplied phrase dictionary used
capitalized keys where the visible engine controls requested lowercase keys.
An expanded machine-key regression failed with exit 2 before correction.
The dictionary now supplies the observed exact search and completion keys,
with English and Arabic resources. The full Developer class and targeted
Browser Release build then passed, both exit 0; the changed panel has no
reported C# language-server errors. A fresh actual source-host page then exposed
Arabic next/previous/all/replace/replace-all controls, all three search-option
labels, the close-button accessible name and the completion-list accessible
name. The old page stalled when navigating after its host was replaced; that
probe was cancelled and replaced with a fresh browser context.

An isolated real-browser probe loaded the generated engine through its public
`createEditor` export, without an answer workspace. After 105 distinct synthetic
replacements, 105 undo gestures stopped at document index 5, proving that only
the most recent 100 groups remained. Redo restored index 105. The initial redo
driver sent lowercase `z` with Shift and did not match the shortcut; uppercase
`Z` with Ctrl+Shift exercised the actual binding. This was a driver correction,
not a product change.

A separate synthetic 2097152-byte JSON source rejected a 2097153-byte full
replacement because current source plus inverse history would exceed 4 MiB.
The public operation threw `RangeError`, retained the exact source length and
content, preserved its version, cleared both consumed byte buffers and displayed
the history-limit status. Disposal removed the model afterward. These are engine
contract observations, not published-package or participant qualification.

With metadata completion open, Tab moved to its accessible list and a second
Tab left the editor for an external button. Without the popup, Tab left directly.
Neither gesture inserted text; the observed intermediate list focus is not a
keyboard trap.

Actual canonical completion after the already-typed prefix `"env.API` inserted
a second opening quote. Accepting the visible selected option reproduced
`""env.API_ENDPOINT": `, an invalid property insertion. The owned completion
range now includes the typed opening quote and, when present, the existing
closing quote and property separator. Environment completion retains its
line-start rule. Native JavaScript syntax checking passed, exit 0. Regeneration
produced a 416320-byte bundle, SHA-256
`c232ff096511cc4f9014e37bdeecf66c1fdbe324c797c37d92866cb6fbb6f864`.
Two generations reproduced all three output files exactly; native offline
freshness passed. The new manifest SHA-256 is
`0cb0399ac0f25ce5ee5c0a14b8895271b89d99d65ac459241bf907d87fe180b4`;
the original-license output hash remains unchanged.

The actual generated engine now completes an already-open quoted key without
duplicating its quote. Completing inside an existing quoted property also
replaces its closing quote and separator exactly once, retains the synthetic
value and yields parseable JSON with one property. Both checks used native
completion-option selection and public editor operations, not internal state.

A first full AOT store-free publication passed in the new task-owned
`.tmp/browser-ux-simplification/phase6-final-store-free` root. It began before
the completion correction, so it is an intermediate package, not settled final
qualification. Frozen comparator package roots are preserved.

Corrected AOT publications then passed, exit 0, in
`.tmp/browser-ux-simplification/phase6-settled-store-free` and
`.tmp/browser-ux-simplification/phase6-settled-with-store`, the latter using the
explicit local `DefinitionStoreSourceRoot="$PWD/store"` input. The complete
`BrowserEditorPackageTests` class passed against those exact roots, including
both published inventories, generated-input/output bindings and hostile native
asset-owner checks. This does not claim every architecture/package class passed.

The corrected store-free package is served by the native static host on 5179.
Its real service worker controls the workspace. Before consent, Developer showed
`developer-consent-required`, zero editor nodes and zero editor-module resource
requests. Deliberate consent mounted one editor and acquired the local 5172-byte
bridge and 416320-byte engine. A synthetic public sentinel produced one Preview
change with no diagnostics; Apply succeeded, retained source and reused the
editor. A probe initially expected the nonexistent status `developer-pending`;
the actual pending status is `developer-changed`. That probe timeout was not a
product failure.

After that Apply, a streamed scan of all 87 actual app-cache responses and their
URLs found no synthetic private sentinel. All four fixed editor assets were
present. Local storage and session storage were empty and no IndexedDB database
existed. Observers installed before the synthetic edit recorded no fetch calls,
service-worker messages or new Worker constructions through Preview and Apply.
Those observed seams do not establish unobserved transport or device behavior.

Real CDP freeze/active transitions retained the accepted document and single
editor. The resumed page remained hidden; the browser driver's high-level click
stalled, so that probe was cancelled. Direct trusted pointer events then retired
and reopened the editor with network emulation offline. The reopened source
retained the accepted public sentinel. This is freeze/resume and offline-editor
evidence, not a claim about every physical device's lifecycle.

While still offline, malformed synthetic text produced one value-free diagnostic,
disabled Apply and remained editable. Search then held a separate synthetic
query. Consent revocation's capture-phase observer saw zero editors and zero
search panels before the managed handler; the settled UI also had zero Preview
and diagnostic nodes and returned to `developer-consent-required`. The installed
fetch/worker/message observers remained empty.

After deliberate reconsent, the actual offline AOT application completed the
already-quoted public key with exactly one opening quote and separator. The
hidden page initially had an unpainted popup at y=-9999; taking and inspecting a
real screenshot positioned the popup before the trusted option gesture. That
driver geometry issue did not alter source. Phase 6.2 is complete; custody,
offline/update/replacement and full comparative qualification remain open.

The stale graph (built October 15 on a different head) did not cover the new
bridge. Exact event-owner reads showed that `pwa-lifecycle.js` emits
`configurator-custody-abandoned` on `pagehide`, but the new bridge had no
subscriber. A real public-bridge probe established Red before correction:
after that event the model remained readable, one editor remained, and an
outstanding transferred byte buffer was not cleared. The first exploratory
probe read again before checking the buffer, so only the second, precisely
ordered probe establishes its event-time lifetime.

The bridge now subscribes only while mounted and removes the abandonment
listener during retirement. The same public operation/event probe in a fresh
browser context establishes Green: no further read succeeds, zero editor nodes
remain and transferred bytes are cleared synchronously at the event. Native
bridge syntax checking passed, exit 0. Both AOT variants then published
successfully in the fresh `phase6-custody-store-free` and
`phase6-custody-with-store` task roots. The full editor-package class passed
against those exact roots, exit 0.

The old package's real live client retained private pending text while its
origin's native static host was switched to the custody-corrected package.
A state-change subscription, installed before `registration.update()`, observed
the new worker reach installed/waiting. The live page kept its original
controller, saw zero controller changes, retained one editor and exact pending
text, and recorded no observed private transport. No update activation or reload
replaced that workspace.

After closing the old live client, a new workspace was controlled with no waiting
worker and the new app-cache version `configurator-app-7m88Qmnt`. Consent was
denied, no editor was mounted and the old private sentinel was absent from the
rendered page. A deliberate dirty offline reload raised the expected native
before-unload decision. The high-level probe blocked on that decision and was
cancelled; accepting the actual dialog allowed reload. The new realm had no old
probe marker, remained offline under service-worker control, denied consent,
mounted zero editors and contained no old private sentinel. A screenshot request
from the cancelled probe remained pending, so direct new-realm DOM checks,
rather than a second screenshot, establish this reload result.

## Settled owner-suite exit checks

The complete Core suite passed 302/302 and Presentation passed 276/276 in
Release. The first full Browser run passed 324/334 and failed 10 cases, exit 2.
These failures are not quarantined as pre-existing:

- Two field-inspector replacement cases rendered `ConfigurationConfigurePanel`
  with an already-disposed old form. The current workspace key was inside the
  retained RTL cascading provider. Moving the form key to that provider did not
  fix either case; that hypothesis was reverted. The old Configure mount already
  receives a cancellation token which the root cancels before disposing the
  old form. Its render now respects that retired-mount boundary before reading
  form projection or publishing navigation content. An initial nested Razor
  block failed compilation with RZ1010; correcting its block syntax preceded the
  next full Browser verification.
- Two V2-editing assertions still expected select items or the old first-label
  title location after short choices became controlled ordinal radios.
- One typed-secret and one neutral-launch assertion expected readiness on the
  old artifact wrapper rather than the delivered Files surface.
- Four manifest cardinality cases expected an old artifact `h3` locator.

The test-only surface migration is bounded to those four existing test files;
all typed values, artifact counts, privacy, absence and readiness assertions
must remain. Any actual behavioral failure must be fixed in its owner rather
than weakening a test.

The independent test migration retained every existing behavioral assertion and
updated only actual control/semantic locations: `MudRadio<string>` ordinals,
field-title chrome, nested `[data-prepared-file]` readiness, Files heading and
explicit copy action. The parent reviewed all four changed ranges before running
the full Browser suite again. Core and Presentation passes remain preserved;
neither owner changed in this correction.

The full Browser rerun passed 334/334, zero failed/skipped, exit 0, after the
retired-render fence and reviewed locator migration. The targeted Browser Release
build and both current AOT variants also exited 0. Current package roots are
`phase6-current-store-free` and `phase6-current-with-store`, beneath the same
task-owned temporary directory.

The native static QA host exited when a browser aborted an asset response,
reporting a transport `HttpListenerException`. This blocked subsequent journeys;
it was not an application result. Its existing response-copy boundary now
handles a disconnected peer without ending the host. The engineering file has
no reported C# language-server errors. A native curl request deliberately
rejected the WASM response by declared size (expected exit 63), after which the
same host served `index.html` successfully, overall exit 0.

The Bun browser driver could not issue a second CDP command while its pointer
command was blocked by a native clear dialog. That isolated page was closed;
no successful cancel result is inferred from it. An owned QA browser with a
protocol-level dialog policy then observed both native `confirm` decisions.
Declining Clear retained the exact accepted synthetic public input. Accepting
Clear showed `workspace-cleared`, denied consent, zero editors and no private
sentinel in the rendered page.

Two attempted cross-navigation `pagehide` observers did not yield their expected
metadata, so neither establishes the event's actual dispatch. A separate measured
realm-boundary journey retained one editor before departure, then reached `/help`
with a different `performance.timeOrigin`, no old realm marker, zero editors
and no private sentinel. This proves actual realm replacement, while the precise
public abandonment-event probe separately proves synchronous bridge/buffer
retirement when that event is delivered. No physical-memory-erasure claim follows.

Phase 6.3 is complete. The nine matched journeys, actual performance matrix,
visual/device/accessibility qualification and human study remain required in 6.4.

## Integrated visual qualification in progress

The first attempted 320-pixel emulation was invalid: it was configured before
navigation, but the observed live page was actually 1440x900 at DPR 1. That image
is retained only as `qualification-visual/developer-1440-light-emulation-attempt.png`
under task scratch, not phone evidence. Applying emulation after navigation to
the live target yielded the required measured 320x740 CSS pixels at DPR 3 and a
960x2220 PNG, retained as `qualification-visual/developer-320-light.png`.

That actual 320-pixel view showed the editor below the first viewport of its own
panel: the full keyboard/formatting explanation dominated the remaining space.
The owning panel now keeps all of that explanation in a localized native help
disclosure, retaining its accessible-description identifier. Scope,
representation and pending status remain independently visible. The focused
Developer class passed 39/39 after this correction, exit 0; the fresh AOT
publication and before/after mobile recheck are pending.

The fresh store-free AOT publication then passed, exit 0. In its measured
320x740/DPR-3 after capture, the editor began at y=602.8 CSS pixels, the help
disclosure was closed, its description target remained present and the document
had no horizontal overflow. The editing surface now appears inside the first
viewport of its own panel. The image is retained as
`qualification-visual/developer-320-light-after.png`; the earlier correctly sized
capture remains the before image.

A measured 375x740/DPR-3 Arabic dark capture retained Preview, RTL panel/LTR code
direction and zero document overflow, but exposed fixed light-theme syntax
colors on a dark editor. The observed green and red text measured only 2.24:1
and 2.08:1 against the actual `rgb(32,36,41)` editor surface.
The capture is `qualification-visual/developer-375-dark-ar.png`.

Apply's apparent dark-mode problem required a separate causal check. After the
dark palette was already present, its new `rgb(26,26,26)` text initially remained
over the old `rgb(15,118,110)` fill, only 3.18:1. Waiting on the button's actual
CSS animation completion, rather than sleeping, produced its correct
`rgb(94,234,212)` resting fill and 11.77:1 contrast. This was a 250 ms background
transition, not an incorrect resting dark palette. The existing scoped button
primitive now excludes fill from its transitions, keeping fill and contrast text
in the same theme while preserving border/shadow transitions.

The engine uses only the installed public HighlightStyle declarations to adapt
the existing syntax specifications through CSS `light-dark`/`color-mix` and
project text tokens. Gutter and caret also use semantic tokens. No dependency
or original-license output changed. Two native generations reproduced all
three files exactly, and offline freshness passed. The new bundle is 416685
bytes, SHA-256
`2ed33d7c0c7675be5f237b128f45b7f4e70ff57b2f922b7bb66aeca4107e9f99`;
its manifest SHA-256 is
`74de21f0d628a738a09920ee3a21e7c1c8b48d833b376513761ffea647b5c6eb`.
Actual light/dark/forced-color rechecks remain open.

All three required Linux x64 native package surfaces published successfully into
the qualification store-free root, exit 0. The concurrent optional-store Browser
publish failed because the native DebugSymbols=false build removed a shared
Core PDB while Browser static-asset resolution still expected it. Exact failure:
`System.InvalidOperationException: No file exists for the asset at either location`
for `Configurator.Core.pdb`, from `DefineStaticWebAssets.ResolveFileDetails`.
No compiler setting or gate was weakened. Subsequent Browser build/publication
is serialized after the completed native matrix, followed by both actual AOT
variants before further qualification.

## Integrated task journeys and bounded Event comparison correction

The settled pre-import-correction AOT candidate passed Browser 334/334 and the
complete actual-package architecture suite 77/77, both exit 0. Its dark Arabic
recheck retained Preview and zero overflow. Syntax contrast improved from
2.24:1/2.08:1 to 8.51:1/7.63:1; Apply used its correct 11.77:1 dark fill immediately,
with no background animation. The after capture is
`qualification-visual/developer-375-dark-ar-after.png`.

Actual integrated task observations, not participant-study results:

| Task | Observed result |
| --- | --- |
| Fresh Environment | Output `0`, Basics and an ordinary public input were ready without an output-selection action; secret consent was denied. |
| Switch Manifest | An Environment draft existed first. Native dropdown keyboard selection reached Manifest, retired the old file, exposed ordinary blockers and four required answers; switching back retained the exact public input. |
| Ten scalars | Ten distinct synthetic string edits appeared in the canonical accepted answers with zero per-field expansion actions. This proves definition-admitted editing, not target semantic acceptance. |
| Ten short choices | Ten distinct capability choices survived section remounting with zero menu opens when driven through the component's owned yes/no labels. |
| Obscure correction | Exact-key search reached `env.ERASURE_WRITER_REPLICA_COUNT` in Advanced, delivered the subscribed focus event and left one search owner. |
| Import/compare | Initially failed with `inverse-limit` under rendered consent; the causal correction below is awaiting the fresh published recheck. |
| Deferred draft | Without a consent-grant action, Review explained missing secrets and Files produced one draft with explicit unresolved paths. A broad page-wide `pre` count is not private-reveal evidence; the scoped output check remains open. |
| Bulk/malformed recovery | After deliberate platform inclusion, ten Environment changes applied with no diagnostics. Canonical state retained those ten settings plus the already-explicit capability answer: eleven values, three booleans. Protected setup content remained outside that source. Malformed text stayed editable, disabled Apply and produced value-free diagnostics. |
| Revoke pending work | Capture-phase revocation saw zero editor/search nodes; the settled UI also had zero Preview and diagnostic nodes. |

The adapter's numeric string `selectOption("1")` did not establish Manifest
selection; native Arrow Down/Enter did. Early radio probes also used an absent
input-level label attribute or precomputed coordinates while activation changed
layout. Those runs yielded zero, nine or eight retained choices and are not
passing task evidence. Owned `-yes-label`/`-no-label` targets retained all ten.
No source change or suppressed failure was used to make these driver probes pass.

The comparison refusal was reproduced in a new Core regression using the exact
Browser family `c0d02619-0134-411b-b495-ef302f592669` and canonical digest
`2f136c5e3a4de673fb6ae3cae1defcb676befa8cbe26f909a2253c036eaa5eef`.
It failed with `inverse-limit`, exit 2, before file parsing. This resolved the
independent reader's uncertainty about a different store-family definition.
Compilation compared each copied source path with every prior assignment,
exhausting the inverse budget on hundreds of unrelated flat paths.

`DefinitionImportProjection` now uses a compilation-local ordinal segment index.
Each traversed segment charges the existing inverse budget; duplicate,
ancestor/descendant and selector conflicts still reject. Repeated rows retain
separate assignment scopes. This is a bounded overlap-check correction, not a
generic cache, a preparation rewrite or a relaxed limit.

The actual Event regression then passed 1/1 and all strict-import cases passed
15/15, both exit 0. The reverse ancestor-order case was added to the existing
ambiguity matrix. The first full Core run exposed two stale expectations: tiny
work totals now honestly include one charged `/x` segment, and Event no longer
inherits an incidental unsupported capability from the quadratic scan.
Totals changed from 28/5 to 29/6 work/inverse nodes; validation and yield counts
remain unchanged. The independent conditional-projection case still requires
whole-inverse refusal.

After those intentional expectation updates, full Release Core 303/303,
Presentation 276/276 and Browser 334/334 passed, all exit 0. Updated native and
both AOT packages are being published serially. The post-correction architecture
gate, published comparison recheck, performance/visual matrix and operator/device
qualification remain open. No operator cohort, physical-device/IME study or
assistive-technology study has run; availability remains unknown.

The bounded documentation check
`TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/knowledge/Knowledge.cs -- links clients`
completed with exit 0: 89 files, 693 local links and zero errors across three
batches. Its native evidence is `.tmp/knowledge/links-clients.log`. This checks
the selected client-documentation links and metadata, not product behavior or
the still-open human qualification.

The corrected candidate then completed the serial native CLI/Terminal/Desktop
and both AOT Browser publications, exit 0. Against those exact roots,
`CONFIGURATOR_PACKAGE_ROOT="$PWD/.tmp/browser-ux-simplification/phase6-qualification-store-free" CONFIGURATOR_STORE_PACKAGE_ROOT="$PWD/.tmp/browser-ux-simplification/phase6-qualification-with-store" TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release`
passed 77/77 with zero failures, exit 0.

On fresh origin 5182, the actual Event comparison now completed under explicit
consent. It reported incomplete assessed coverage rather than refusing projection
compilation, rendered fifty findings on its first page, left the existing public
answer unchanged and did not display the synthetic protected fixture content.
Incomplete coverage is not strict-autofill eligibility or target acceptance.
The actual screenshot is `qualification-visual/import-comparison-corrected.png`.
The scoped deferred-draft recheck also passed: denied initial consent, one draft,
eight unresolved paths, an empty hidden output preview with no visible rectangle,
and the reveal action still collapsed. Machine-readable observations are retained
in `.tmp/browser-ux-simplification/qualification-corrected-journeys.json`.

## Integrated qualification

### Final interaction cohort and activation attribution

The corrected-import candidate ran 960 measured trusted-input samples on
HeadlessChrome 153/Linux at 1280x813 CSS pixels, with five warmups per direction
and separate first-use observations at both 1x and 4x CPU. No build or test ran
alongside this cohort. Feedback measures dispatch to the first animation frame;
final content requires the expected DOM state and two animation frames.
Intersecting long tasks include tasks beginning before dispatch. Every sample
and maximum is retained in
`.tmp/browser-ux-simplification/final-interaction-performance-before-activation-correction.json`.

| Interaction | Worst directional final p95, 1x / 4x ms | Maximum feedback, 1x / 4x ms |
| --- | --- | --- |
| Basics / Advanced | 99.5 / 196.3 | 22.032 / 48.198 |
| Configure / Review | 32.8 / 44.3 | 16.032 / 47.064 |
| Configure / Files | 32.8 / 35.5 | 16.066 / 42.064 |
| Form / Developer | 48.5 / 171.9 | 15.232 / 44.186 |
| Ordinary scalar | 32.3 / 124.1 | 30.830 / 110.886 |
| Short-choice activation | 33.5 / 122.4 | **58.696** / 125.128 |

The activation-on sample at zero-based index 17 failed the unchanged warmed 1x
feedback and long-task gates: 58.696 ms feedback, 68.3 ms final content and a
73 ms task. The other groups initially appeared within their gates, but the
clock audit below invalidates qualification claims from this cohort. The first local editor mount took 74.7 ms final
content / 15.266 ms feedback at 1x. Scalar final-state observation is the edited
native input value plus two frames; separate integrated canonical-answer checks
establish acceptance, not that timing observation alone.

A consistency audit retained one invalid observation: Developer entry, 1x,
zero-based index 22 recorded -1.568 ms feedback. The sampler subtracted input
dispatch time from the animation frame's supplied timestamp, which can predate
an event dispatched later in that same frame. Feedback must instead use
`performance.now()` inside the animation-frame callback, on the same clock as
dispatch. The original 960 observations, 120 warmups and 24 first-use records
remain intact. The corrected sampler will rerun the complete cohort against the
corrected candidate; no record is silently clamped or removed. The retained
73 ms long-task failure is independent of that negative feedback observation.

Two diagnostic CPU-profile cohorts retained their observations rather than
replacing qualification samples. The clock-aligned cohort reproduced a 64 ms
activation task. Its first 15.588 ms sampled as native `(program)` before the
label-generated input click. After that click, 41.715 ms of sampled owned work
was under `ConfigurationField.Edit` / `EditText` / `DefinitionFormModel.EditValue`
/ `DefinitionFieldInput.TryValue`. Facts construction accounted for 29.584 ms
inclusive, including 24.277 ms in the validator. These inclusive samples overlap;
they are not additive elapsed-time phases. No GC cause is established.

A separate diagnostic publication with `WasmEmitSymbolMap=true` completed with
exit 0 and emitted `dotnet.native.js.symbols` in the native intermediate directory.
Its native asset remained `dotnet.native.2e649ud7vz.wasm`, matching the measured
candidate. The function map therefore resolved the retained numeric frames
without changing the qualification package.

Independent read-only consultation and owning-source inspection identified a
repeated `TryCorrectionPath` traversal for every resolved validation node inside
`DefinitionFormFacts`. The constructor now retains local node/path pairs from
its first traversal and reuses them for the later entry capture. Diagnostic-path
resolution, validation, eager immutable snapshot capture, ordering and unresolved
node exclusion stay intact. This is neither an answer cache nor a Core rewrite.
The earlier full Presentation 276/276 run is its behavior baseline; C# LSP reports
no errors after the edit. Affected-owner verification and the separate corrected
AOT candidate are running under `bash_268`. Runtime benefit is not yet claimed.

### Published keyboard and visual checks

The combined traversal/focus candidate completed the full corrected-clock
cohort, not only the previously failing activation direction. All 960 measured
records have finite nonnegative feedback and final content no earlier than that
feedback. Its 120 warmups and 24 first-use observations are retained separately;
none failed the applicable feedback or warmed 1x long-task limits. No native
build, test or profiler ran alongside this cohort.

| Interaction | Worst directional final p95, 1x / 4x ms | Maximum feedback, 1x / 4x ms |
| --- | --- | --- |
| Basics / Advanced | 99.6 / 197.2 | 18.5 / 70.9 |
| Configure / Review | 32.8 / 45.7 | 17.8 / 58.7 |
| Configure / Files | 32.8 / 45.4 | 17.3 / 45.9 |
| Form / Developer | 48.6 / 150.4 | 15.7 / 57.2 |
| Ordinary scalar | 32.3 / 122.6 | 30.5 / 131.5 |
| Short-choice activation | 32.4 / 131.3 | 33.1 / 132.7 |

Every measured feedback sample is strictly below 50/200 ms at 1x/4x; no warmed
1x task exceeds 50 ms. All directional final-content p95 values also satisfy
the retained stricter 100/400 ms transition thresholds. The cold editor mount
was 77.7 ms final content / 15.1 ms feedback at 1x. The slowest section final
sample at 4x was 226.9 ms and remains in the data; p95 is a separate gate, not
permission to omit that maximum.

Raw evidence is
`.tmp/browser-ux-simplification/final-interaction-performance-corrected-clock.json`,
bound to `phase6-activation-correction`, origin 5183 and the same declared
1280x813 Chromium 153/Linux setup. This is a scoped technical performance pass,
not a measured operator speedup, a device study or proof that the traversal
change alone caused the difference from the earlier cohort.

Actual light/dark captures now cover Form, Review, Files and Developer at
1440x900 and 375x740. Additional inspected captures cover the 1920x1080 shell,
Arabic RTL phone editor and 768x600 forced-colors/reduced-motion editor. Each
reported width was read from the live page after emulation. These captures show
one search owner, one editor, no document horizontal overflow, readable actions
and empty hidden draft previews in the inspected states. Files actions measured
44 CSS pixels high on the phone. They are Chromium emulation, not physical-device
or assistive-technology evidence. Artifacts are under
`.tmp/browser-ux-simplification/qualification-visual/`, named by surface, width
and theme.

The keyboard journey opened local search with Ctrl+F, closed it with Escape and
returned focus to the editor. Tab then reached Preview, Format and Apply in
order. Enter previewed and applied a synthetic public change; the next Tab
reached Cancel. Enter retired all editor/Preview nodes and preserved accepted
answers. It also exposed a real recovery defect: removing the focused Cancel
button left focus on `BODY`. The before image is
`qualification-visual/developer-keyboard-retired.png`; keyboard operation up to
retirement passed, but that focus result is not passing recovery evidence.

The new public cancellation regression compiled and failed at runtime before the
fix, 1 failed / 0 passed, exit 2, because there was no focusable recovery notice.
Explicit cancellation now requests focus on a named retirement group after its
render. Generation checks and subsequent-retirement invalidation keep revocation
and disposal from borrowing that user-only focus request. All Browser tests then
passed 335/335, exit 0. Production C# LSP is clean; the test LSP still misprojects
the Razor-generated `ConfigurationEditor` type, while the native compiler and
tests succeed. No language server was installed and no diagnostic was suppressed.
The combined AOT publication passed, exit 0. Its actual keyboard recheck observed
the subscribed focus event on the named retirement notice, zero editor/Preview
nodes and the reopen action as the next Tab stop. The inspected after image is
`qualification-visual/developer-keyboard-recovery-after.png`.

Mobile navigation also passed its actual focus distinction: Escape returned to
Menu, while choosing Advanced closed the rail and retained focus on the requested
section heading after mounting completed. The page retained one search owner and
no horizontal overflow. The inspected capture is
`qualification-visual/mobile-section-focus-final.png`.

At 640x400, a deliberate root-font override to 200% produced a measured 32 px
root / 30 px body font, retained one editor and no document horizontal overflow.
The inspected capture is `qualification-visual/developer-640-root-text-200-percent.png`.
This is a CSS text-scaling/reflow probe, not native browser zoom, OS text scaling
or physical-device evidence.

### Exact package verification and source-mount correction

The final serial package run completed with exit 0: Linux x64 CLI, Terminal and
Desktop publication, both AOT Browser variants, native editor freshness and the
complete actual-package Architecture suite, 77/77. Roots were
`.tmp/browser-ux-simplification/phase6-final-store-free` and
`phase6-final-with-store`. Their common native runtime was
`dotnet.native.kwoepqrdnu.wasm`, SHA-256
`94c6dee29885887b62fdb033ca04b9470dc85ac1dd9bde45061691455e5e93ff`.
The unchanged editor/manifest hashes remain those recorded above; the bridge
hash was `1f094aff07d7f8e26c0c20beafe76566e50043b510e86d976e1a6b82d5b40a39`.

Locked solution restore and Release build passed. Initial solution formatting
reported owned multi-entry initializer layout and import ordering; only those
mechanical issues were corrected. The repeated format check passed, exit 0.
Client documentation checked 89 files / 693 local links / zero errors.
An explicit execution-record check initially exposed five missing natural
metadata fields in this report; the header and stale introduction were repaired.
Its isolated recheck passed six files / 35 local links / zero errors, exit 0.
The earlier combined documentation/Git command hit its deadline after printing
the clean document result. A separate `GIT_MASTER=1 git --no-pager diff --check`
then passed with exit 0; the combined deadline is not presented as a successful
command.

Publication changed native identity from the earlier measured
`dotnet.native.2o7hq756oa.wasm`, so the full 960-sample cohort was repeated against
the exact final artifact on fresh origin 5184. Feedback and final-content p95
gates passed, but Developer entry at 1x, zero-based sample 20, retained a 53 ms
long task: 15.2 ms feedback / 72.4 ms final content. This fails the separate
warmed 1x long-task gate. All observations remain in
`.tmp/browser-ux-simplification/final-package-interaction-performance.json`.
The earlier candidate pass does not qualify this artifact.

A matching-symbol diagnostic publication preserved that exact native WASM.
Its clock-aligned 160-transition profile reproduced a cold 57 ms task, not the
same warmed sample. That task combined about 34.859 ms of managed execution
with 17.974 ms of editor execution. `ReadDocumentSource`/`ReadSource` accounted
for 23.243 ms inclusive within the managed portion; `OnAfterRenderAsync` joined
source admission and DOM construction in one task. No GC cause is inferred.
The profile and observations are retained as
`developer-entry-before-frame.cpuprofile` and
`developer-entry-before-frame-observations.json` under the task scratch root.

The narrow Browser correction reuses `configuration-rendering.js.nextFrame`
between admitted source acquisition and editor construction. It rechecks the
existing lease afterward. The acquired byte array has one owned alias so
retirement can clear it immediately while the frame is held; normal completion
also clears it. No Core validation, accepted-answer state, budget, dependency or
generic scheduler changes. Four new frame-boundary regressions first compiled
and failed at runtime because the required frame request never arrived, exit 2.
After the correction, four stale-authority/cancel cases passed; the normal case
exposed an existing empty `aria-busy` value. That attribute now emits explicit
lowercase true/false instead of Boolean attribute syntax. The focused normal
recheck passed 1/1; the settled Browser owner passed 340/340. Formatting of the
changed C# files, both refreshed AOT Browser publications and actual-package
Architecture 77/77 then passed in one serial command, exit 0
(`bash_286` / `mon_REMH6YSAYD60QRZB`). The unchanged native CLI, Terminal and
Desktop packages retain their earlier passes.

Both refreshed Browser roots now contain `dotnet.native.h05xny9gky.wasm`,
SHA-256 `5f9949d8d09fd2abfa42e009867d511e9475e42f9fa87b18f69eef74c0255835`.
Actual origin 5185 inspection confirmed one valid editor, settled
`aria-busy="false"` and no horizontal overflow. A separate instrumented probe
held only the owned rendering frame: no editor existed, `aria-busy="true"` and
Cancel remained enabled. Actual consent revocation followed by delivery of the
held callback left zero editors/previews, consent disabled and no window errors
or unhandled rejections. This is a controlled boundary observation, not physical
erasure proof. Records are in `source-mount-runtime-qualification.json`, and
the inspected images are `developer-after-mount-frame.png` and
`developer-held-mount-revoked.png` under `qualification-visual/`.

The complete unchanged normal timing protocol was then repeated on that exact
runtime: five warmups and 40 measured samples per direction/rate, retaining all
960 measurements, 120 warmups and 24 first-use observations. Every feedback,
journey-specific content-p95 and warmed 1x long-task gate passed. The following
table gives the worse direction, not a pooled percentile; times are milliseconds.

| Interaction | Feedback max 1x / 4x | Content p95 1x / 4x | Warmed long-task max 1x / 4x |
|---|---:|---:|---:|
| Sections | 20.7 / 66.7 | 99.5 / 186.3 | none / 66 |
| Configure / Review | 16.3 / 59.0 | 32.8 / 41.6 | none / 62 |
| Configure / Files | 18.5 / 43.7 | 32.7 / 42.0 | none / none |
| Form / Developer | 15.8 / 56.4 | 65.3 / 149.2 | none / 78 |
| Ordinary scalar | 40.1 / 131.0 | 32.2 / 132.3 | none / 131 |
| Short-choice activation | 41.7 / 149.6 | 32.4 / 149.8 | none / 158 |

First-use Developer entry was 15.5 / 26.0 ms feedback and 82.0 / 122.6 ms final
content at 1x / 4x. All observations, including slow 4x tasks and maxima, remain
in `frame-fenced-final-package-interaction-performance.json`. The prior
53 ms failure and diagnostic profile remain separate; they were not discarded
or relabeled as passes.

The single gate reviewer returned REJECT for the full goal: this timing failure
and the disclosed operator/platform gaps. Its inspected source flows revealed
no additional correctness blocker. The report is
`.omo/evidence/browser-ux-simplification-gate-review.md`; it predates the final
source-mount correction, whose verification remains parent-owned.

### Bounded Preview and multiline insertion diagnosis

A valid 262225-byte canonical document retained first-use Preview feedback of
51.1 ms at 1x, including a 52 ms task. The owning caller now awaits the existing
rendering frame before acquiring editor bytes, then passes the document
session's existing `nextTask` cooperative publication yield. Busy state paints
before parsing; after parsing clears owned source bytes, queued cancellation or
edits can invalidate the candidate before publication. Cancellation during the
initial frame is handled as cancellation, not a window error.

The two controlled-boundary regressions failed at runtime before this correction,
then passed 2/2. Full Browser passed 342/342; scoped formatting, both AOT variants
and actual-package Architecture 77/77 passed in the serial
`bash_290` / `mon_39HX2M767SSXX1GH` command, exit 0. The final runtime is
`dotnet.native.zkts2yjmx3.wasm`, SHA-256
`c33e56098a3481a014a9ae3ac1abc94f33de55e3e9e6423473b1d33ae2475750`
in both variants. Its actual-browser requalification is recorded separately.

Source setup also exposed a different problem: trusted CDP `Input.insertText`
with 256 KiB and thousands of newlines produced 107443.6 / 490729.1 ms feedback
at 1x / 4x. These observations remain in
`large-work-inserttext-before-classification.json`. The run was reported killed
at 300 seconds, but outstanding protocol work continued and recorded the 4x
observation afterward; it is not reported as a successful command.

A reduced 17002-byte diagnostic required 2062 layouts, 266.8 ms of layout and
50.4 ms of script in the editor. The same native insertion in bare editable div
and textarea controls required 1539 / 1026 layouts, about 231 / 238 ms feedback
and less than 0.2 ms script each. The CPU profile was dominated by `(program)`,
not Core parsing. This identifies the browser's multiline native insertion path,
not evidence that the admitted-document parser took hundreds of seconds.
`Input.insertText` is not a clipboard-paste measurement.

A clearly labeled synthetic DOM paste prepared the 256 KiB model in 41.5 ms
with four layouts. It is used only as source setup for subsequent trusted
Preview/Apply actions, never as physical or trusted paste qualification. No
production workaround or dependency change was made for the inappropriate
automation route. The CPU profile and bare-control/setup observations remain in
`large-inserttext-diagnostic.cpuprofile` and
`large-inserttext-native-controls.json`. Earlier nonexistent-marker and
off-screen-control probe failures are retained as driver failures.

### Bounded Apply feedback correction

The complete `zkts2yjmx3` large-work cohort retained 40 measured Preview/Apply
actions per direction/rate after five warmups and separate first-use costs.
The 262263-byte source preserves the existing explicit capability answer while
changing one endpoint. Preview feedback maxima were 15.1 / 13.7 ms at 1x / 4x;
completion p95 was 129.5 / 218.2 ms, with tasks up to 90 / 139 ms. These are
bounded-work completion observations, not a claim that all parsing finishes in
one paint budget.

Apply still failed feedback at 66.2 / 213.5 ms, with completion p95
49.8 / 188.4 ms and tasks up to 68 / 214 ms. Every observation remains in
`final-preview-large-before-apply-feedback.json`; no slow sample was removed.

The Apply caller now paints busy state before freezing/committing and rechecks
both lease and candidate identity afterward. Cancellation or changed text wins
before commit. Its finally path is also fenced by text version and cancellation,
so a late Apply cannot clear a newer Preview's busy state. Three controlled
regressions failed before this correction; native/package and runtime results
for the correction are recorded at their actual completion.

### Apply-corrected package and remaining normal timing

The three Apply-frame regressions passed 3/3; full Browser passed 345/345.
Scoped formatting, both AOT variants and actual-package Architecture 77/77
completed exit 0 under `bash_294` / `mon_JCN4AX3X8NQR00HX`. Both variants contain
`dotnet.native.hsyroeebgh.wasm`, SHA-256
`1f67dcb964c0b3befb20121900b9aeb0c9028d198f985e4ced252e75b93a948f`.

Its first full 960-record normal cohort failed several gates; all records remain
in `final-apply-normal-initial.json`. A contemporaneous host observation found
VBCSCompiler at 628% lifetime CPU, load 11.19/13.89/10.80 and 53 GiB swap used.
That observation alone does not establish the failures' cause.

After an observable compiler-quiet condition, only the five failed cohorts were
rechecked with the same warmups, sample counts and limits. Three passed, but
Developer entry retained a 58 ms task and scalar feedback retained 51.4 ms.
`final-apply-normal-compiler-quiet-recheck.json` preserves the complete recheck.
At that checkpoint, no normal qualification pass was established for the final
artifact. The scheduling diagnosis and complete requalification below supersede
that timing disposition without removing these failures.

Diagnostic-only profiling then observed no long task in 80 Developer round trips
but reproduced scalar feedback 68.4 ms with a 69 ms task in 120 edits. These
observations and profiles remain in `remaining-normal-diagnostics.json`,
`remaining-mount.cpuprofile` and `remaining-scalar.cpuprofile`. Generated symbol
files may be stale when ordinary publication disables symbol emission.
`bash_300` / `mon_PS9GTJYM5P3F82T0` explicitly generated the diagnostic package,
exit 0; its native SHA-256 exactly matches the final artifact. The matched
profile assigns a 21.134 ms sample interval to a Value getter whose owning
source simply reads a field. This is not evidence of 21 ms of getter computation,
nor proof of GC, paging or scheduling as the cause. Facts construction accounts
for about 4.2 ms in the affected interval. A source-established repeated section
scan remains a possible small optimization, not an established cause of this
stall; no speculative production change was made.

The final large-work cohort does pass its immediate-feedback gates: Preview
maxima 15.4 / 11.4 ms and Apply maxima 37.8 / 13.5 ms at 1x / 4x.
Completion p95 is 131.9 / 217.8 ms for Preview and 132.9 / 156.1 ms for Apply;
retained tasks reach 91 / 137 ms and 100 / 152 ms respectively. This does not
claim synchronous work is hard-interruptible. All 160 measured actions, warmups,
first observations and synthetic setup records are in
`final-apply-large-performance.json`.

A separate live post-parse hold observed busy state, enabled Cancel and no
published preview. Trusted cancellation retired the editor; delivery of the
held callback and Reopen preserved the accepted endpoint. This functional
observation is not a complete cancellation timing cohort; its record is
`final-apply-functional-cancellation-under-load.json`.

The subsequent complete cancellation/recovery cohort retained 40 measured
actions per direction/rate, five warmups and first observations. All accepted
values survived. Cancel feedback passed at 15.7 / 27.9 ms; completion p95 was
32.1 / 29.5 ms, with no measured Cancel long task. Reopen passed 4x feedback
at 109.4 ms but retained a 1x feedback failure of 52.3 ms and a 51 ms task.
Its completion p95 was 49.2 / 139.6 ms. The entire dataset remains in
`final-apply-cancellation-performance.json`. This is controlled post-parse
publication cancellation, not a claim of hard interruption during synchronous
Core work. At that checkpoint, normal entry/scalar and Reopen timing remained
required qualification work in task 6.4; the subsequent scheduling recheck below
addresses those timing failures.

### Final exact-package normal recheck before the Apply correction

The `zkts2yjmx3` final Preview build passes the complete normal cohort:
960 measured records, 120 warmups and 24 first-use observations, with no
nonfinite, negative or misordered clocks. Maximum measured feedback is
43.7 / 160.3 ms at 1x / 4x. No warmed 1x task exceeds 50 ms, and every original
journey-specific content-p95 gate passes.

| Interaction | Feedback max 1x / 4x | Content p95 1x / 4x | Warmed long-task max 1x / 4x |
|---|---:|---:|---:|
| Sections | 18.3 / 57.4 | 99.5 / 197.0 | none / 60 |
| Configure / Review | 16.3 / 63.5 | 32.7 / 53.4 | none / 65 |
| Configure / Files | 16.4 / 53.3 | 32.8 / 51.2 | none / 56 |
| Form / Developer | 15.7 / 57.1 | 65.4 / 173.0 | none / 106 |
| Ordinary scalar | 40.8 / 131.6 | 32.8 / 134.0 | none / 132 |
| Short-choice activation | 43.7 / 160.3 | 40.2 / 133.1 | none / 163 |

Raw data is `final-preview-package-normal-performance.json`. A Files predicate
initially required a Review button that appears only for blocked readiness;
the actual deferred Files panel had already settled. That instrumentation
failure is retained in `final-preview-normal-instrumentation-attempt.json`.
All 240 completed measurements were preserved. A fresh same-build workspace
supplied the first-use Files observation, and only the remaining 720 measured
actions were run. No measured record was deleted or rerun to remove an outlier.

### Final renderer scheduling diagnosis and exact-package requalification

The scalar trace in `remaining-scalar-thread-trace.json` reproduced a 61.283 ms
browser task with 36.842 ms of thread time. That wall/thread difference does not
identify a managed method as the source of the entire delay. Inspection then
found the task-owned renderer, PID 553608, using `SCHED_IDLE`. This was a concrete
qualification-environment defect, not evidence that every prior slow sample had
one cause.

Only that owned renderer's scheduling was changed, first on its main thread and
then all its threads, using `chrt --other --pid 0 553608` and
`chrt --all-tasks --other --pid 0 553608`. Subsequent `chrt -p 553608` reported
`SCHED_OTHER`, priority 0; `ps -p 553608 -o pid,cls,rtprio,comm` reported class
`TS`. No user browser, host-wide policy, product source or acceptance threshold
was changed. No speculative section-scan optimization was applied.

The unchanged final `hsyroeebgh` artifact was then measured with the same trusted
event-capture/animation-frame clocks and final-DOM predicates: 40 samples per
direction/rate, five warmups and a separate first observation. All 960 measured
records, 120 warmups and 24 first observations are retained in
`final-normal-scheduling-performance.json`. All 1,104 records have finite,
nonnegative, ordered clocks and trusted input events. These first observations
are under corrected scheduling, not replacement cold-start observations.

Every feedback sample is strictly below 50 / 200 ms at 1x / 4x, every directional
content p95 is below the retained stricter 100 / 400 ms limit, and no warmed 1x
task exceeds 50 ms. The table gives the worse direction, not a pooled percentile;
times are milliseconds.

| Interaction | Feedback max 1x / 4x | Content p95 1x / 4x | Warmed long-task max 1x / 4x |
|---|---:|---:|---:|
| Sections | 16.2 / 69.5 | 99.4 / 196.7 | none / 68 |
| Configure / Review | 16.2 / 51.5 | 32.8 / 48.3 | none / 54 |
| Configure / Files | 16.4 / 47.8 | 32.8 / 36.6 | none / none |
| Form / Developer | 15.7 / 55.4 | 65.6 / 162.2 | none / 89 |
| Ordinary scalar | 33.5 / 142.5 | 32.3 / 130.6 | none / 142 |
| Short-choice activation | 38.9 / 154.3 | 37.2 / 152.0 | none / 158 |

Only the previously failing 1x Cancel/Reopen pair required a further recheck.
Its 40 measured cycles, five warmups and first observation are preserved in
`final-normal-scheduling-cancellation.json`. Cancel feedback maximum / content
p95 is 22.1 / 32.0 ms; Reopen is 33.3 / 54.2 ms. Neither has a measured long task.
All trusted actions retain the accepted endpoint after the held post-parse
callback is released. The original complete 4x pair already passed and remains
in `final-apply-cancellation-performance.json`; it was not repeated. Large-source
setup remains synthetic paste, not OS clipboard qualification, and the held
publication boundary is not hard interruption of synchronous Core parsing.

This closes the scoped final normal/recovery timing failures for the recorded
workstation environment. It does not establish hardware input latency, physical
device performance, assistive-technology usability or the comparative benefit
required from representative operators.

### Native browser zoom on the final package

A separate headed Chrome 153/Linux session loaded the same final
`dotnet.native.hsyroeebgh.wasm` on a task-owned 1440x1000 Xvfb display. Native
window shortcuts, not CSS/device emulation, set 200% and 400% browser zoom.
Device-pixel ratio changed from 1 to 2 and 4 while visual-viewport scale remained
1. The resulting CSS viewports were 719x456 and 359x228; document widths were
714 and 357 respectively, without horizontal page overflow.

At 200%, the public answer and consent survived reflow; Review correctly showed
deferred readiness with no inactive Form inputs. At 400%, the single Developer
editor retained the public answer. A one-change Preview/Apply succeeded, and
returning to Form showed the new accepted endpoint with zero editor instances.
Menu remained reachable; a native Escape key closed it and returned focus to
its toggle. Inspected captures show readable wrapped source, applied status and
navigation without overlapping controls:

- `qualification-visual/native-zoom-200-form.png`
- `qualification-visual/native-zoom-400-developer.png`
- `qualification-visual/native-zoom-400-applied.png`
- `qualification-visual/native-zoom-400-menu.png`

`native-browser-zoom-qualification.json` binds the observed runtime and records
the attempts: an initial pipe session vanished, and a persistent launch chose
Wayland despite `DISPLAY`. That owned instance was stopped before native input;
the successful isolated launch explicitly selected X11. An Apply probe initially
expected `applied` instead of the actual `developer-applied` status; the predicate
was corrected without repeating the successful Apply. The qualification browser
and X display were stopped afterward.

This is one automated native-zoom journey, not a representative operator study,
physical mobile test, OS IME test or screen-reader session.

### Acceptance coverage and remaining qualification

This map distinguishes regression evidence, scoped published-browser observations
and the human/platform work still required. The named test methods/classes are
executed owner evidence, not a claim that a suite simulates assistive technology.
Earlier source-only consultation gaps for fresh output, Manifest switching,
unsupported mapping and retirement are addressed by the additional evidence
shown here; they are not silently treated as passed by unrelated tests.

| Scenario | Regression owner | Published evidence and limit |
| --- | --- | --- |
| AC-01: fresh Environment | Event workflow / definition-profile tests | Fresh integrated task: Environment, Basics and API input without an output action. |
| AC-02: Manifest switch | Configuration navigation / export lifecycle tests | Native output selection retained the public answer, exposed four requirements and retired the existing draft. |
| AC-03: direct scalar | `BrowserDirectEditingTests` | Ten accepted public scalar edits, zero expansion actions. |
| AC-04: absent / false / true | `DirectRadioChoicesKeepUntouchedFalseAndExplicitEmptyDistinct` | Native short-choice actions retain values; physical keyboard/AT cohort remains separate. |
| AC-05: short enum visibility | `OrdinaryAdvancedInputsAndShortChoicesMountWithoutSupplyingAnswers` | Visible choices in inspected Form captures; no menu-opening action for ten short choices. |
| AC-06: default origin | `ReviewExplainsOriginsWithoutExposingAnswersOrChangingRevision`, `DefinitionAnswerPresenceTests` | Review captures distinguish authored defaults from accepted supplied values. |
| AC-07: pristine mount | `BrowserDirectEditingTests`, `DefinitionContainerPresenceTests` | Fresh canonical document has no supplied values or omissions. |
| AC-08: body rail | `BrowserShellTests` | Inspected 320/375/768/1280/1920-width captures show reflow without horizontal overflow; the desktop rail sits beside content and ends above the footer. Separate native 200%/400% Chrome checks also retain reflow and navigation. |
| AC-09: distinct panels | `PanelRoundTripPreservesAcceptedAndDraftState` | Review/Files probes observe zero inactive Form controls and identifiable selected workflow. |
| AC-10: advanced correction | `SearchAndReviewCanReachAnUnmountedFieldWithoutCompletingTheRestEarly` | Exact-key search subscribed to actual focus on the Advanced input; one search owner. |
| AC-11: inactive correction | `InactiveResultRoutesToControllerWithoutActivatingFeatureOrConsent` | Regression proves routing without enabling a feature or granting consent. |
| AC-12: retained section work | `SectionUnmountPreservesInvalidDraftAndCorrectionsRemainSticky` | Ten short choices remain after section remounts; consent/identity/draft fences have native regressions. |
| AC-13: ready summary | `ReadyReviewOffersPreparationWithoutChangingAnswers` | Actual Review is populated, not an empty heading box; ready and deferred states are separately tested. |
| AC-14: ordinary blockers | `ReviewDistinguishesOrdinaryBlockersFromSecretDeferredReadiness` | Manifest task exposes ordinary requirements; correction/return invariants are tested. |
| AC-15: deferred secrets | Same readiness regression | One draft, eight unresolved paths, explicit incomplete status; no complete-readiness claim. |
| AC-16: stale output | `BrowserExportLifecycleTests`, `RevocationRetiresSecretAndOutputBeforeHeldMountOrOldInputCanResume` | Actual edit/revoke and output-switch journeys retire old visible bytes; held exports are generation-fenced in tests. |
| AC-17: clipboard denial | `ClipboardFailureProvidesValueSafeDownloadAlternative` | Native regression exercises value-safe failure and the real download alternative; no claim of every OS clipboard implementation. |
| AC-18: deliberate Clear | `ClearConfirmsOnlyWhenItWouldDiscardWorkAndCancellationPreservesIt` | Actual native confirmation decline retains work; acceptance clears it in the offline journey. |
| AC-19: editor boundary | `BrowserDeveloperEditingTests` | Actual representation, public scope, coverage and pending status are visible in light/dark and Arabic captures. |
| AC-20: invalid source | `RejectedOrInvalidDocumentsRemainPrivateAndUnchangedByFormat` | Actual malformed source remains editable, produces value-free diagnostics and disables Apply. |
| AC-21: typed Apply | `ApplySwapsOncePreservesProtectedAnswersAndReusesThePrivateEditor`, Core document tests | Ten Environment edits apply; canonical state retains ten settings plus one explicit capability, with protected setup content excluded. |
| AC-22: unsupported mapping | `ProtectedAndAmbiguousAssignmentsRemainExplicitWithoutSerializingAnswers`, `CaseCollisionsAndArrayIndexedSourcesDoNotBecomeEditable` | Core negatives establish refusal/exclusion; whole-import strictness is not broadened by editable scope. |
| AC-23: deletion semantics | `EnvironmentMissingKeysHaveExactMergeOrOmissionMeaning` | Merge/replace controls and Preview remain explicit; independent missing/empty/boolean semantics are tested. |
| AC-24: stale diagnostics | `NewTextInvalidatesTheOldParseWithoutLosingTheNewDocument`, retirement race matrix | Actual replacement/revoke removes diagnostics; controlled late reads cannot resurrect candidates in tests. |
| AC-25: protected values | `ApplySwapsOncePreservesProtectedAnswersAndReusesThePrivateEditor` | Actual canonical/environment source excludes protected setup content, rather than inserting a literal mask. |
| AC-26: private retirement | `RetirementFencesLateReadAndClearsTransferredBytes`, frozen-Apply revoke test | Capture-phase revoke removes editor/search synchronously; public bridge abandonment probe rejects reads and clears owned transfer bytes. No physical-erasure claim. |
| AC-27: keyboard / AT | Developer transition and cancellation regressions | Actual search/Escape/Tab/Preview/Apply/cancel/recovery path passes after focus correction. Screen-reader operators remain unavailable/unverified. |
| AC-28: visual / input methods | Browser culture/state regressions | Inspected phone/desktop, light/dark, RTL and forced-color captures retain state and legibility; native 200%/400% Chrome reflow and interaction pass within the recorded scope. Physical devices and OS IME qualification remain open. |
| AC-29: offline custody | Offline workspace / package / lifecycle suites | Actual offline edit/reload, local asset use, limited request observers, cache/storage scan and denied-consent mount checks pass within their stated observation scope. |
| AC-30: responsive / stale work | Developer concurrency and cancellation suites | Final `hsyroeebgh` normal cohort passes all 960 measured samples under recorded normal scheduling; targeted 1x recovery also passes. Earlier failures, large-work completion and cold observations are retained separately. Physical-device responsiveness remains unqualified. |

### Seven-pair implementation disposition

The revision-3 I-VSD assessment and its historical receipts are preserved. This
execution summary supplies evidence without rewriting the original seven pairs
or treating their planning alignment as product qualification.

| Pair and concern | Technical evidence now available | Remaining qualification |
| --- | --- | --- |
| F001 / M001: reduced operator work | Nine integrated automated journeys, zero expansion/menu steps in the specified tasks and the corrected-clock interaction cohort. | No representative operator comparison, paired task times or demonstrated aggregate advantage. |
| F002 / M002: deliberate defaults and choices | Admitted authored output metadata, exact presence/provenance regressions, fresh Environment and state-preserving Manifest switch. | Final overall review; no live-target acceptance inference. |
| F003 / M003: protected working text | Consent-before-acquisition, public-only source, synchronous revoke/abandonment retirement, cleared transfers and fenced late exports. | Final overall review; runtime physical erasure is neither promised nor measured. |
| F004 / M004: no silent data loss | Whole-candidate admission, revision/version fences, ten-value typed Apply, explicit omission/merge/replace and malformed recovery. | Final overall review; host semantic validation stays with the target. |
| F005 / M005: equivalent access | Inspected light/dark/RTL/forced-color layouts, keyboard Preview/Apply/cancel recovery, mobile menu focus and scoped native 200%/400% Chrome interaction. | Physical devices, OS IME and assistive-technology operators. |
| F006 / M006: truthful readiness | Populated Review, explicit deferred drafts, hidden output, deliberate export and actual Clear cancel/confirm behavior. | Representative operators explaining draft/complete and preparation/apply distinctions. |
| F007 / M007: offline promises | Reviewed 17-component local closure, reproducible assets, actual offline/cache/update/custody probes, final package Architecture 77/77 and preserved technical interaction budgets. | Final overall review with the remaining human/platform evidence; no publication authority is implied. |

Zero representative operators, physical-device sessions or assistive-technology
study sessions have run. Both resource questions timed out; no availability or
results were supplied. These required
gates remain in task 6.4 of this same plan, not a backlog or a completed checkbox.
The steward must supply sessions or existing verifiable results before the
experience can be declared fully qualified.

The remaining study requires 8-12 representative novice/technical operators,
including assistive-technology use, across the nine counterbalanced tasks and
both comparators. The unchanged gates are at least 90% unaided core-task
completion, correct readiness/authority explanations from every participant,
no critical loss/privacy/correctness failure, no core-task median worse than the
faster comparator, and at least 20% lower aggregate median task time. Native
200%/400% Chrome zoom has the scoped evidence above; physical mobile/input and OS
IME observations remain required.

Developer editing must qualify its integrated admission, scope, lifecycle,
custody and offline contracts. The final execution record must bind the settled product and package,
exercise all nine integrated tasks and report actual participant/device/AT
counts, including any unavailable evidence.

### Participant session worksheet - prepared, not executed

This worksheet operationalizes plan section 15. It contains no participant
results and does not close task 6.4. Use anonymous participant IDs and synthetic
configuration only. Keep the raw observations task-local; report counts,
failures and value-safe summaries here.

#### Freeze the three conditions before a session

- **A - redesigned product:** use the final package identified above. Record its
  package root, native SHA-256, admitted definition family/revision/digest and
  browser version in the session record.
- **B - previous product:** identify and record the actual pre-redesign package
  and its provenance. Earlier phase snapshot names alone do not establish that
  identity; do not substitute an intermediate redesigned package.
- **C - plain local editor/key-value workflow:** record the editor/version,
  document format and complete procedure for reaching the same target outcome.
  Declare any external validation or comparison steps before timing.

Give all conditions the same machine, documentation access, synthetic starting
state and target outcomes. Freeze the exact ten scalar paths, ten choice paths,
obscure setting, import file, bulk-edit document and malformed variant before
the first session. Record the public fixture identities/hashes, not private
deployment content. Reset each task to its declared starting state so earlier
tasks do not silently supply later answers.

Assign successive participants the condition orders `ABC`, `ACB`, `BAC`, `BCA`,
`CAB`, `CBA`, then repeat. Rotate the nine task orders across participants using
the independently seeded tasks; preserve any explicitly declared dependency.
Retain the assigned and actual orders, including interruptions.

#### Nine outcome cards

Use outcome-based instructions rather than naming the redesigned control to
click. The independent observer checks the result against the frozen fixture.

| Task | Target outcome and correctness check |
| --- | --- |
| Fresh Environment | Open a fresh Event workspace and configure the declared public Environment values; distinguish authored defaults from supplied answers. |
| Switch Manifest | Select the Manifest task while retaining compatible supplied values; no previously prepared output remains usable as current output. |
| Ten scalars | Set the ten declared optional scalar values exactly; check presence as well as text, without supplying unrelated answers. |
| Ten short choices | Set the ten declared boolean/short-enum choices, retaining the required distinctions between absence, explicit false and explicit empty values. |
| Obscure correction | Find the declared non-obvious setting and correct it without changing unrelated settings or enabling an unrelated feature. |
| Import/compare | Inspect the supplied synthetic file after deliberate consent; identify differences and retain accepted answers until an explicit adoption action. |
| Deferred draft | Prepare the declared secret-deferred draft, identify unresolved requirements and explain why it is not complete or applied to a target. |
| Bulk edit and recovery | Apply the declared multi-setting edit, then repair malformed text; invalid text must not partially change accepted state or discard working text. |
| Revoke during work | Withdraw consent during the declared pending operation; protected answers, prepared output and private editor work retire, and late completion cannot restore them. |

For a condition that has no corresponding UI mechanism, declare the equivalent
semantic procedure before timing. If no equivalent procedure exists, record the
capability/comparability gap rather than inventing a pass, assigning zero time
or removing the task.

#### Record observations without converting failures into successes

Each attempted task record contains: participant ID, experience group,
condition/order, task/fixture identity, device/OS/browser, locale, input method,
assistive technology/version where used, start/end timestamps, elapsed time,
task actions, errors, corrections, hesitation notes, assistance, independent
correctness outcome and evidence reference. Start timing after the participant
has read the outcome card; end when they declare completion, then independently
check correctness. An assisted completion remains assisted, not unaided.

Record physical mobile model/orientation and actual touch/keyboard behavior.
For OS IME, observe composition, candidate selection, committed text and focus
in Form and Developer; synthetic DOM composition is not that evidence.
Screen-reader evidence names the actual reader/browser and operator journey,
including announcements, editor entry/exit, errors, status and recovery.
Existing emulated captures, accessibility trees and native zoom observations
retain their narrower scope.

After the journey, ask each participant to explain what a deferred draft still
needs and whether preparation has applied or authorized anything on the target.
Record each explanation's correctness and any correction needed.

Report actual participant counts, all attempted tasks, unaided completions,
assisted completions, failures and both comprehension outcomes. Retain every
slow observation. Timing comparisons use completed matched tasks only, with
eligible pair counts shown; excluded timing pairs remain in completion/failure
counts. Declare the aggregate calculation before the first timed session and
include it with the results, rather than choosing a favorable calculation
afterward.

Evaluate the unchanged plan gates against those records: 8-12 representative
operators including assistive-technology use, at least 90% unaided core-task
completion, correct explanations from every participant, no critical
loss/privacy/correctness failure, no core-task median worse than the faster
comparator and at least 20% lower aggregate median task time. Missing comparable
data cannot satisfy a gate. A failed interaction returns to its existing owner
for correction and affected requalification; it does not lower the gate.
