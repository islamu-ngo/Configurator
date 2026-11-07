# Operations

> **Audience:** Contributors | AI agents
> **Status:** Implemented local engineering procedures; publication not activated
> **Owner:** Packaging/Release
> **Last Verified:** 2026-10-28
> **Source Anchors:** `AGENTS.md`, `PROJECTS.md`, `global.json`, `eng/agent-workflow/`, `eng/release/`

This selective transfer uses the AI-agent operational section from Event
revision `c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38`, with local verification
instructions anchored to Configurator owners. Event's backend operations,
databases, API/BFF, Aspire startup and production trust are not imported.
Configurator prepares artifacts offline; it does not apply target configuration
or acquire host administration authority.

## Current procedure entry

Start with [Status and evidence](STATUS.md) to select the claim being checked,
then use the current owner below. [Knowledge map](KNOWLEDGE_MAP.md) routes
document kinds and lifecycle; the [ADR index](adr/README.md) separates design
acceptance from delivery. These indexes add no runtime or publication authority.

| Task | Current procedure or contract owner |
| --- | --- |
| Select projects and verification commands | [Project map](../../PROJECTS.md), [Testing](TESTING.md) and [Verification Policy](#verification-policy) |
| Produce native or static packages | [Packaging](PACKAGING.md) and [public packaging guide](../public/packaging.md) |
| Inspect a published Browser | [Browser custody and offline verification](#browser-custody-and-offline-verification), using the selected package's identity and current packaging instructions |
| Handle interrupted CLI publication | [CLI contract](CLI_CONTRACT.md) and [cancellation procedure](#cli-cancellation-and-interrupted-publication) |
| Save restricted native output | [Protected artifacts](PROTECTED_ARTIFACTS.md) and [native output guide](../public/native-output.md) |
| Assess or fill from ordinary configuration | [Import/comparison contract](IMPORT_COMPARISON.md) and [operator workflow](../public/import-and-compare.md); implementation in progress, client/package qualification pending |
| Prepare release evidence | [Release runbook](RELEASE_RUNBOOK.md); local validation is not signed-publication activation |
| Validate documentation changes | [Native knowledge commands](../../eng/knowledge/README.md#documentation-ci) and [documentation workflow](../../.github/workflows/docs-quality.yml); current checks and optional manual historical audit have separate outcomes, hosted execution requires a later push |

Machine contract `0.0.1` is the current development baseline. The earlier
[secret candidate qualification](#schema-driven-secrets-qualification-2026-11-02)
records the pre-reset payload; [current baseline execution](#fixed-development-baseline-001-2026-11-02)
is recorded separately.
Earlier machine delivery evidence below remains evidence for its recorded
candidate and does not qualify the current payload.

## Desktop redesign execution (2026-11-07)

The approved `avalonia-desktop-ui-ux-redesign` workstream changes the native
composition over existing Presentation/Core answers and Artifacts publication.
Open/Configure/Review, semantic appearance, bounded collection projections,
metadata discovery and unified selected-output preparation remain view and
adapter responsibilities. Save stays a separate create-only operation.

Executed with SDK `10.0.302`, `TMPDIR="$HOME/.cache/agent-tmp"`:

```bash
dotnet build tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --verbosity quiet --tl:off -m:1 -nr:false
dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --no-build -- --minimum-expected-tests 395
```

Both exit 0; the final owner passes 395 tests, zero failed/skipped. This includes native headless
keyboard/automation, raw-draft/control identity, metadata privacy and held
publication/import/close invariants. A prior 391/392 run exposed publication
notification access to the process-global dispatcher. Capturing the operation
owner context corrected that cause; physical receipt settlement remains
independent of visual delivery.
The added root-variant regression requires validation to reveal and focus the
invalid child, not its branch selector. A staged-close test initially observed
save-task return before asynchronous window closure; it now awaits the actual
Closed event and additionally asserts workspace disposal. Its focused case and
the final full owner pass (395/395, exit 0). The keyboard cases explicitly
verify visible forward Tab and reverse traversal in both en/ar at enlarged text.
Rendered intake regression first failed for Arabic, then passed after null
workspace locale correctly used host UI culture and direction. Loaded workspaces
still own their explicit locale; the application does not change process culture.

The local publication target also exited 0, including current ratchets 3/3:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:Configuration=Release -p:ConfiguratorSurface=desktop -p:ConfiguratorRid=linux-x64 -p:ConfiguratorOutputRoot="$PWD/.tmp/avalonia-desktop-ui-ux-redesign/publish"
```

The actual single-file executable SHA-256 is
`3b0cf1f08cea6198cb58d59d05ed35870e81dfcc34855410238d7b8688f34e40`.
It launched on a dedicated Xvfb display with Arabic host UI culture before
admission, then in English with public
`examples/minimal-self-hosted/configurator.yaml`. Inspected current captures
under `.tmp/desktop-ux-published-r3/` show Arabic RTL intake and the loaded
English Configure workspace. Earlier `.tmp/desktop-ux-published-r2/` shortcut
captures belong to the earlier payload, not this identity. Injected X11 events
are not physical-keyboard or IME certification. Synthetic applications
and displays were explicitly terminated after inspection. Xvfb reported
nonfatal keymap/AMDGPU warnings; no GPU or performance qualification is claimed.

Final native startup cohorts each exit 0 with exact required/completed name-set
equality, 30/30, under `.tmp/desktop-ux-final-r2/attempt4/{en,ar}`.
The actual run durations were 30,898.006 ms and 31,988.1732 ms; these are suite
durations, not responsiveness or performance qualification.

```bash
CONFIGURATOR_NATIVE_UI_CULTURE=en xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 30 --capture-directory "$PWD/.tmp/desktop-ux-final-r2/attempt4/en"
CONFIGURATOR_NATIVE_UI_CULTURE=ar xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 30 --capture-directory "$PWD/.tmp/desktop-ux-final-r2/attempt4/ar"
```

The source inventory SHA-256 is
`c04ccdb1110db1e4dbacdc286107bf2175036365519deb7c5c9f40ca888ba086`.
Capture sidecars retain actual viewport, locale, appearance, body/heading/title
font sizes, text scale, render scale and loaded assembly SHA-256/MVID identities.
The Desktop assembly in native qualification has SHA-256
`56a2edc9e5a33cab2ff77f7e0fdbd609192e454d410ef5bbef10f1fcefd36f9a`.
This local observation inventory is not release provenance or a signer receipt.
Earlier failed diagnostic captures remain under `attempt1`/`attempt2`.
Independent English review passed 54 Open/Configure frames. Arabic review first
failed 90 of 162 frames; its 90 newly affected attempt3 frames then passed,
retaining 72 unchanged-view passes. Parent inspection covered appearance,
popup roots, correction, real 200% text, collection/search and review/save frames
across all three sizes and palettes. All 36 final middle-scroll import-purpose
frames were inspected, closing the previously unseen minimum-width label case.
No local visual finding remains in those named cohorts. No actual Windows/macOS,
real-session Linux screen-reader speech, IME/device or participant qualification
is inferred from headless checks. All remain explicit maintainer-owned gates
inside the workstream. No release signing, installer or external publication
was executed.

Remaining qualification stays inside the Desktop redesign task:

| Cohort | Current limit | Responsible role |
| --- | --- | --- |
| Real-session Linux accessibility | No screen-reader speech, user/device or participant observation; Xvfb automation properties do not prove it | Desktop maintainer and accessibility evaluator |
| Windows x64 | No actual host supplied for input, IME, contrast and assistive-technology observations | Desktop maintainer |
| macOS arm64 | No actual host supplied for Command mapping, IME or assistive-technology observations | Desktop maintainer |
| OS contrast and hardware DPI | Capture contrast is injected; 200% text roles are separate from actual host DPI | Desktop maintainer and accessibility evaluator |
| Physical keyboard and native pickers | Injected events and synthetic streams do not certify physical controls or platform dialogs | Desktop maintainer |
| Linux arm64 | Conditional on advertised Desktop support; the current release target remains Linux x64 only | Packaging maintainer |

These pending cohorts do not activate additional release targets or protected
save hosts. Local I-VSD alignment and successful preparation grant no target
acceptance, signing or publication authority.

## Desktop build-statistics verification

The approved Desktop dependency policy excludes build-statistics assets through
the three first-party project references and central package version described in
[Packaging](PACKAGING.md#desktop-build-statistics-policy). Preserve the failed
capture identifying `AvaloniaStatsTask` as the counterexample. Capture rejection
blocks qualification; it does not prevent an unknown task from executing.

After native lock regeneration, verify effective imports and actual Desktop,
Desktop.Tests and Desktop.NativeHost graphs: no statistics-task invocation, required XAML/resource
tasks retained, complete native capture bindings, affected Release/full-owner
checks and actual publication/dependency notices. Record exact commands,
results and identities in the active workstream before promoting this policy
to execution evidence. Central props changes require refreshing affected
consumed-input capture/check receipts. Earlier receipts remain historical
evidence for their original inputs; they do not qualify changed build inputs.

## Validate contributor intents

Run `dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- validate-intents .agents/contract/intents.yaml`
from the repository root. The command admits one UTF-8 YAML document, bounded
to 4 MiB, 100,000 parser events and 64 collection levels. Anchors, aliases,
explicit tags and duplicate mapping keys reject before semantic evaluation.

The owning `.agents/contract/schema.json` supplies required/known fields,
types, enums, patterns and minimum sizes. The native validator implements that
closed vocabulary without a schema-library dependency. It also rejects duplicate
intent IDs, unsafe relative paths and unresolved document, rule, skill, agent,
test-project and related-intent references. Scope and routing globs describe
possible future edits and need not match existing files. Metadata commands are
data and are never executed. The schema is located by walking the supplied
registry's ancestors; a registry outside a repository contract fails closed.

Success emits `intents_valid` and exits 0. Rejection exits 2 with one fixed
diagnostic: `invalid_intents_yaml`, `invalid_intents_contract` or
`invalid_intents_reference`. Diagnostics omit input text, values, paths and
exception details. Verify changes with the AgentWorkflow build/test commands in
[PROJECTS.md](../../PROJECTS.md), then validate the actual current registry.

## Operational Runbooks

- [Packaging technical anchor](PACKAGING.md) and [adopter guide](../public/packaging.md)
  own native/static output commands and platform limits.
- [Protected artifacts](PROTECTED_ARTIFACTS.md) and [native output guide](../public/native-output.md)
  own create-only save, privacy and failure behavior.
- [Launch contract](LAUNCH_CONTRACT.md) owns the local handoff boundary.
- [Import/comparison execution record](reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md)
  binds the local comparison mechanisms, exact candidate identities, measured
  results, historical failed cohorts and explicit qualification limits.
- [Release runbook](RELEASE_RUNBOOK.md) owns generated notes and local evidence.

Do not use service-recovery or direct-database instructions from Event as
Configurator operating procedures.

## Validate synthetic interaction records

Use `dotnet run --file eng/configurator/MeasureInteractiveWork.cs -- --self-test`
to check the measurement parser/aggregator, or pass
`--input FILE --fixture SHA256 --payload SHA256 --host SHA256` to validate an
explicit synthetic record set. The [Browser ownership contract](BROWSER_UI.md#synthetic-interaction-evidence)
defines the bounded format, required journeys, sample cardinality, thresholds
and exit codes. Keep the returned raw samples and every outlier with their
exact fixture/payload/host identities. This command does not run a Browser,
authenticate the producer or supply actual-host, participant or AT evidence.

The [public fixture generator](BROWSER_UI.md#public-collection-fixtures) supplies
five nested/collection/correction/multi-output inputs separately from that
result validator. On 2026-11-05 its Release self-check and create-only generation
both exited 0. The generated completion manifest under
`.tmp/eaq-interactive-fixtures-20261105/` records all five definitions and their
independent expected-output identities. Baseline map/list bounds were corrected
from fixed 256 to 255–257 before the accepted self-check so add/remove/reverse
can actually run. No Browser performance result follows from those commands.

## Retain safe evidence and qualify explicit receipts

The [safe archive procedure](TESTING.md#local-safe-evidence-archive) creates
hash-addressed report/index sets and checks a copied archive without depending
on the original CI state. Local creation, retrieval and tamper rejection are
verified mechanisms; seven-day uploads remain transient transport. The release
steward still supplies the actual custody destination, custodian, retention
policy and retrieval/tamper drill before the corresponding release claim.

The [support receipt procedure](PACKAGING.md#detached-support-qualification-receipts)
validates explicit source/build/payload identities and outcome relationships.
Accepted failed and unexecuted rows remain failed and unexecuted. Synthetic
validator tests do not establish actual-host qualification or release authority.

The [detached provenance procedure](PACKAGING.md#detached-package-provenance)
checks explicit payload inventories, retained native capture sessions and
typed evidence against independently retained identities. Its synthetic
consumer checks are separate from the capture owner's actual consumption proof.
Keep candidate bytes unchanged while producing later detached receipts and
support matrices; changing relevant inputs or payloads invalidates affected
qualification.

The static Host admits the SDK PWA asset inventory and fixed packaging artifacts,
with explicit MIME mappings and framing denied. Run `BrowserHostPolicyTests`
against `CONFIGURATOR_HOST_URL` with an isolated publication containing the
documented synthetic attack files. The workflow owns its local process,
bounded readiness and cleanup. Local rootless read-only containers passed
22 HTTP cases for each existing Store/no-Store tree; this does not establish
a fresh Browser AOT payload, complete image rebuild, real PWA journey or
remote CI execution.

## Native directory composition ownership

Core accepts ordered path-free JSON/YAML sources and charges their aggregate
input, node, scalar, parser-event, merge and serialization ceilings once per
compilation. Standalone definition admission/import receive fresh parser budgets.
Artifacts owns Linux directory acquisition and fixed-layout `statx` identity.
It captures input, privately serializes through Core, invokes one barrier only
for a valid candidate, then checks cancellation and recaptured identity before
returning output. Earlier serialization/limit failures invoke no barrier; all
failed results expose empty output.

The current Linux x64 native cohort covers real directory mutations and refusals;
arm64 checks establish literal flags/layout only. Actual other-host qualification
and final published Browser/package exclusion remain separate requirements.
See the [native acquisition ADR](adr/ADR-native-directory-acquisition.md) and
[protected contract](PROTECTED_ARTIFACTS.md).

## Historical evidence boundary

The dated qualifications and candidate records below retain their original
headings, commands, failures, sample sets, digests and evidence locations in
place. Some later sections interleave procedures with observations: use the
current procedure entry above for routing, and read each observation against
its own recorded source, package, host, date and scenario. A later pass does
not erase an earlier failure or transfer qualification to a different payload.
Historical commands document execution; they are not automatically the current
procedure. No evidence records have been moved into the new indexes.

Local ignored evidence paths, temporary captures and session handles are
retained provenance references, not promises that their underlying files remain
available in a checkout. A recorded digest identifies bytes; it does not
establish present availability, independent reproduction or target acceptance.
See [source/package identity and evidence availability](STATUS.md#sourcepackage-identity-and-evidence-availability)
before reusing a historical result.

## Definition v2 qualification

The 2026-10-28 local qualification used SDK 10.0.302 and Linux x64. The durable
public input is
`tests/Configurator.Architecture.Tests/Fixtures/V2Qualification/configurator.json`.
Entered answers, downloaded artifacts and screenshots were synthetic and
temporary; no private answer fixture is shipped.

All three native surfaces and both store-free/store-enabled AOT Browser variants
were physically published. The complete Architecture owner passed 73 tests;
the strict CLI package validator retained four accepted and 63 rejected receipt
cases. Store conformance passed 48 cases, and native generation/freshness admitted
two fresh v2 revision-1 families. Their retired v1 identities were not repinned
or promoted as a new trusted baseline.

Actual X11 runs passed all eighteen retained guards plus D19 in English and
Arabic. D19 now covers declared fixed members alongside typed map entries,
required correction, branch replacement, nullable intent, locale-only byte
preservation and real protected output. Light/dark desktop and minimum-size
captures were inspected. The Desktop owner passed 229 tests, with the affected
typed class passing six.

The actual published Terminal was exercised at an 82-by-28 outer terminal frame:
keyboard choice/navigation, masked literal-key entry, invalid-draft correction,
map/provider editing, reset/suggestion/null, native import and protected save.
Its output matched actual Browser and CLI bytes, with native mode `0600`.
Publication consumed private state, and the successful process exited 0.
Follow-up rendering checks preserve readable Latin fallback under right-aligned
RTL metadata; complete bidirectional shaping and assistive technology remain
separate claims. The Terminal owner passed 106 tests.

The published Browser was inspected at 1440-by-900 and 390-by-844, including
dark/RTL layout, focus, multiline inert help, typed values, explicit secret
consent, truthful incomplete drafts and deliberate reveal/copy/download.
Locale changes preserved actual artifact bytes. A held clipboard completion
after revocation did not restore denied output or stale copied status. An
already-authorized external clipboard transfer is not revocable afterward.
The probe found no answer canaries in network/worker/storage channels or in
166 cached responses totaling 74,465,760 bytes. A real new worker waited without
replacing the live workspace. Cached store discovery and pinned snapshot
admission also worked with the local asset host stopped.

For repeatable static-package inspection, run the loopback-only native helper:

```bash
dotnet run --file eng/configurator/ServePublishedBrowser.cs -- \
  "$PWD/artifacts/configurator/browser/wwwroot" 7084
```

It serves public assets and SPA navigation routes, not answer endpoints or a
production host. Headless tests, actual rendering, package checks and target
acceptance are distinct evidence. These results do not qualify another host,
manual native picker, screen reader, legal review, signer or target application.

### Final definition delivery and responsibility

Distribution commit `55c951d` contains the qualified runtime/package repairs;
`764cb59` promotes the current reference/cookbook. The final solution Release
build, solution format and post-distribution native release range all exited 0.
Core's final owner gate passed 217 tests. Earlier phase-specific CLI,
Presentation, Browser and artifact evidence remains in its owning phase record;
the final build is not a claim that every suite was rerun after prose changes.

The portable adoption exercise used the installed Linux x64 executable to fork
the public qualification source to a fresh family, check/lint it, discover the
built-in authoring schema and build revision 1. Receipts used command v3 and
authoring/runtime 2/2. Lint correctly reported unspecified restart/update advice.
Synthetic prefill, validation and create succeeded; native output was `0600`
and byte-equal to the actual Browser/CLI/Terminal artifact with SHA-256
`71d540aa2e56214f2f78f7a6d76edbff6f1588a9c2dee550a669cb9f77378fbe`.
Incomplete synthetic input returned exit 3 with two value-free required
corrections. Complete preparation still reported target acceptance unassessed
and host-validation readiness incomplete; these are deliberately separate axes.
No private source discovery or target process was used.

A public-schema exercise additionally admitted an explicitly acknowledged bounded
integer/nullable-string schema and reported twelve transformation diagnostics,
including discarded default and bounded numeric conversion. An otherwise
equivalent schema using unsupported `pattern` returned exit 65 with
`schema-keyword-unsupported`, even with acknowledgment, and created no output.
Portable/router native validation and all five validator self-tests passed.
The four guidance documents passed local-link/fence checks. Adding their missing
H1 title required a final package refresh; the intervening shipped-copy test
correctly detected stale bytes rather than being weakened.

| Scenario | Delivered owner / evidence |
| --- | --- |
| S01 | Core v2 admission and generated runtime schema; legacy/unknown contracts reject |
| S02 | Core exact paths/canonicalization and typed adapter qualification |
| S03 | Core syntax/expansion/evaluation budgets and conformance owner |
| S04 | Core effective resolver; shared initialization regression owner |
| S05 | Core snapshot and Presentation editing; actual null/omit/reset controls |
| S06 | Core exact numeric, Unicode and presence conformance |
| S07 | Recursive node admission, typed maps/variants; native D19 and actual Browser |
| S08 | Core scoped predicates and row conformance; shared adapter projection |
| S09 | Core local relationship conformance, separate from activation ordering |
| S10 | Core assertion-context and explicit lexical-format vectors |
| S11 | Core inherited privacy and public-literal admission; store recursive privacy cases |
| S12 | Core safe coordinates and current client correction; no dynamic-key serialization |
| S13 | Authoring source maps and CLI structured bounded diagnostics |
| S14 | Core serializer-compatible mapping admission |
| S15 | Core scoped bounded projection/repeat composition |
| S16 | Core atomic bounded inverse; actual synthetic native prefill |
| S17 | Core draft dependency/completion policy and actual Browser incomplete draft |
| S18 | Core/Presentation digest identity and pinned history; fresh v2 generated families |
| S19 | Derived capability handshake and four client admission owners |
| S20 | Presentation reachable required/error fallback and client navigation owners |
| S21 | Typed choice/suggestion editing; actual duplicate-label and null selection |
| S22 | Inert help/localization; actual locale-preserved output and RTL rendering |
| S23 | Presentation operation generations; actual Browser consent/held-transfer fence |
| S24 | Core importer structured retained/converted/advisory/discarded/unsupported ledger |
| S25 | CLI v3 strict consumers and schema/lint; real standalone discovery/check/build |
| S26 | Advisory update/restart metadata; target acceptance remains unassessed |
| S27 | Four native/static publications, schemas/examples/ratchets and Architecture 73 |
| S28 | Promoted public/technical reference; native metadata/link/fence checker |
| S29 | Portable native validators and actual fresh-family synthetic command sequence |
| S30 | This separated conformance/client/package/target evidence record |
| S31 | Core closed vocabulary and inert bounded extensions; no execution/ref fetch |
| S32 | Exact declared application versions and active revision pinning, not ranges |

The [v2 ADR](adr/ADR-definition-standard-v2.md) retains decisions and rejected
alternatives. The [I-VSD execution checkpoint](../../islamic-value-sensitive-design/workstreams/i-vsd-definition-standard-redesign.md#executed-mitigation-review-2026-10-28)
retains all eight pairs and residual responsibility. General assertions,
declared version ranges and external target/adopter trials have separate
bounded backlogs. None is a hidden incomplete branch of this selected profile.

## Verification Policy

Shared builds pin SDK analyzer rules to .NET `10.0` in Recommended mode and
enable build style analysis. The existing file-scoped namespace convention is
enforced as a build warning and therefore fails under warnings-as-errors.
Other style suggestions remain editor guidance unless explicitly promoted.
This policy adds no analyzer package or warning suppression; verification must
compile the affected source to exercise the selected diagnostics.

Use SDK 10.0.302 and Microsoft.Testing.Platform. Set the current workstation
temporary root before native tool/test execution:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
```

Run the targeted Release build and affected classes at coherent phase exit as
described in [TESTING.md](TESTING.md#the-3-ring-progressive-verification-model).
Preserve passing evidence; rerun only failed/newly affected checks. Broader
affected-owner and architecture/package checks run once at workstream exit.

The current CI/package lane covers Linux x64 native clients and static Browser.
The Windows x64, macOS arm64 and Linux arm64 actual-host preparation,
restricted-save and package checks are **Planned**. Record those as unrun until
their host commands and results exist; cross-published output is not a substitute.

Documentation-only changes verify local targets, anchors, formatting and
whitespace. They do not run product builds or .NET test suites. Never broaden a
tooling-only change into product suites or an Event database matrix.

### Verification Evidence

For each completion, report exact commands and outcomes, including exit status
and executed test selections. Record omitted checks and why; distinguish package
creation, test execution and measured coverage. Do not say a build or test passed
when it was not executed. Explicit tests omitted from an ordinary run are not
evidence for their behavior.

Report unrelated failures without absorbing them into the task. Prove baseline
origin where available and quarantine pre-existing rot in task-owned context.
No warning suppression, hidden skip or test weakening is an acceptable repair.

## CLI cancellation and interrupted publication

The [CLI contract](CLI_CONTRACT.md#async-lifetime-and-owned-input) defines the
cooperative async lifetime; the [operator guide](../public/cli.md)
describes its process behavior. Ctrl+C and Unix SIGTERM request cancellation of
one invocation. Repeated signals do not create additional receipt attempts.
Owned input byte buffers are cleared and native preparations are disposed; this
does not promise managed JSON/string erasure or recovery from kill/crash.

Final receipt delivery has a separate bounded token so cancellation of work does
not automatically cancel the receipt. A delivery failure exits 74. Otherwise
cancellation exits 130 unless an earlier operation or cleanup failure already
determines completion. Neither exit code proves that nothing was published:
retain established writes, inspect each artifact's write status and cleanup, and
check destinations before deliberately retrying. Unknown means publication was
not established, not that the destination is absent. Ordered bundle and public
definition/ID-map writes are not transactions.

For runtime verification, record the CLI Release build and affected cancellation,
native process, application, definition, prepared-artifact, machine-boundary and
source-helper test selections separately from protected writer/bundle owner checks.
Controlled gates and bounded waits must exercise cancellation before reads/commit,
at successful commit, after a written bundle prefix and during cleanup; native
process checks cover Ctrl+C, SIGTERM, repeated signals and a broken final channel.
This is the required evidence scope, **not a claim those checks have run**.
Non-cancellable native I/O may delay shutdown; forced process loss cannot guarantee
a receipt. This increment does not qualify additional hosts, harden public-file
custody or deliver the separate parser/environment migration.

## Browser custody and offline verification

The [browser ADR](adr/ADR-browser-session-trust-and-export.md) records the design;
[launch](LAUNCH_CONTRACT.md), [presentation](PRESENTATION_STATE.md) and
[packaging](PACKAGING.md) identify production owners. Browser package creation
does not deploy hosting or prove platform behavior. Use a published static
package, not development hosting, for offline/cache/update evidence.

Before claiming actual-platform support, record the static host, browser/version,
package identity and exact scenarios. Prepare public app assets and explicitly
retain a public admitted definition before answers. Disconnect, reopen/reload,
edit typed settings and object rows, and inspect complete/draft copy/download
against independent vectors. With distinctive synthetic sentinels, inspect
network requests, storage and Cache API payloads to prove no answers/output
entered those sinks. Exercise consent revocation, clear/navigation, late work
and updates with another active tab. Inspect keyboard focus, mobile reflow,
zoom and value-safe failures. Never include private paths or real credentials
in evidence.

Application-local no-fetch assertions are distinct from browser-managed worker
update checks and installation of public app assets. These update requests carry
no answers while an older workspace remains pinned. The page's connection guard
does not control all worker/browser traffic. Physical disconnection is stronger.
Managed-memory clearing, clipboard and downloads are not forensic-erasure claims. Cross-published native
RIDs and package tests remain separate from actual-host protected-save evidence.

### Browser interaction qualification (2026-10-22)

The interaction workstream separates one form-owned metadata index, current
revision facts and current view/discovery selection. Browser mounts discovery
on request and pages shared search/effect selections after complete ranking.
The [form computation decision](adr/ADR-browser-form-computation.md) records the
ownership and notification contract.

The frozen indexed-edit candidate passes the reference-host latency gates;
see the final inventory and results below. Earlier entries are chronological
diagnostics, including superseded failures, not accessibility certification.
Use the static Release publication,
SDK 10.0.302 and `TMPDIR="$HOME/.cache/agent-tmp"`:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser -p:Configuration=Release -p:ConfiguratorOutputRoot="$PWD/artifacts/browser-performance/store" -p:DefinitionStoreSourceRoot="$PWD/store" -verbosity:quiet
ASPNETCORE_ENVIRONMENT=Development dotnet src/Configurator.Browser.Host/bin/Release/net10.0/Configurator.Browser.Host.dll --urls https://localhost:7444 --webroot "$PWD/artifacts/browser-performance/store/browser/wwwroot"
```

The untouched Store-enabled baseline is retained under
`artifacts/browser-performance/baseline`, from source
`733aba5fceb41ec1c0c6337525ccdee67a8093e0`. Its
`service-worker-assets.js` SHA-256 is
`84f5eb9374666906633f7215a114e47bdde563efdf230613a75b4ff260bfdfa5`.
It is served separately on HTTPS port 7445; the existing user listener is
untouched. Candidate inventory digests change with each verified repair and
must be captured again before final sampling.

The actual browser is cached Google Chrome for Testing 151.0.7922.34, launched
with `--headless --no-first-run --ignore-certificate-errors
--remote-debugging-port=9224 --user-data-dir=<isolated-synthetic-profile>`.
The certificate exception is confined to that profile/process; no trust store
is changed. Existing OmOWright agent tabs/CDP drive trusted input. DOM mutation
readiness followed by two animation frames records the final requested content,
not handler completion. PerformanceObserver records long tasks and Event Timing;
task attribution includes tasks intersecting input dispatch.

Record first-use separately, then five event-complete warmups and every one of
40 samples per direction/journey at 1x and 4x CPU. Use nearest-rank p50/p95/max;
do not delete outliers or substitute a spinner for final content. Bind every
result to package/definition digests, viewport and actual scenario state.
Private text, queries and artifact bytes do not belong in reports.

An early candidate still took about 1,017-1,107 ms for warmed Event disclosure.
The Browser adapter repeatedly rebuilt section field membership with linear
root lookup. After indexing those form-owned references and using keyed field
states, a diagnostic trusted-frame sample took 31.6 ms. Further indexed root
reads removed repeated origin scans. These are repair probes, not 40-sample
acceptance results. Final cold/warm, scale, custody, offline, focus, RTL,
light/dark and narrow-layout evidence remains required.

The first complete untouched Event-environment disclosure run retained 40
samples in each direction at 1x. Opening p50/p95/max was
2,597.1/8,137.2/16,173.3 ms; closing was
2,636.5/11,196.3/13,435.1 ms. All outliers remain in
`artifacts/browser-performance/evidence/baseline-event-disclosure-1x.json`.
Host load average reached 24.40; these are observed workstation results, not
isolated-device certification. An intermediate candidate met the 100 ms control
p95 budget (53.3/60.3 ms), but maximum observed feedback exceeded 50 ms.
That feedback gate remains open; per-field root scans were removed before
remeasuring, rather than relaxing the budget.

With the per-field references indexed, the served candidate
`388aa0bab614c4f63f31171123c89c63a4b6dd72c76e110520584e8ac766bfb5`
recorded disclosure opening p50/p95/max 26.3/32.9/33.0 ms and closing
23.8/32.2/32.4 ms, with maximum feedback 20.8/16.0 ms and no observed
long tasks. Review entry p95 was 50.8 ms, but maximum feedback was
57.9 ms. Discovery retained large outliers (opening max 732.5 ms,
closing p95 404.8 ms). These failures remain open, not discarded.

During this run, a direct project publication accidentally wrote the sibling
`store/wwwroot` directory rather than the host's `store/browser/wwwroot`.
The first evidence labels used that sibling inventory
`6d8beab269fc976806e0fde260b5ba04f7f0733c08244ae44fddbc7d47b30d07`;
it was not the served package. The host command and both on-disk inventories
establish the served identity above. Evidence corrections must retain this
provenance note. Use the release target shown above for package closure.

Indexing Review's metadata title lookup removed another root scan. The next
served inventory is
`65952fb34c65f6c231eb8fb699d4f4a0df51ae146738721a6317d3ceaa5419c0`.
Its 40-sample 1x runs passed the active budgets: disclosure opening/closing p95
31.6/31.9 ms, Review entry/return 32.6/32.9 ms, discovery opening/closing
31.9/31.8 ms. Maximum feedback across these runs was 23.4 ms and no warmed
long task was observed. At 4x, Review entry/return p95 was 106.0/59.2 ms
with maximum feedback 121.6/76.5 ms. These cover those named journeys only;
the remaining matrix, custody and accessibility gates remain open.

The public synthetic scale fixture was admitted through the actual local-file
browser flow, canonical digest
`d23cec8f1707dbc30451fb9c4c04f763aaaaa63e1ac31a370a5bc1cd901df486`.
It has 500 roots and 125 list rows; the mounted discovery reported 1,000
settings, with 50 results rendered on page 1 of 20. Admission and this
cardinality observation do not establish latency qualification.

An intermediate candidate inventory after native scalar/action controls, query-local
rendering and narrow Review wrapping is
`1bccea5acf0f2f88acf77ff213d1aaa5589a5986263c62e195a3edf9d9d50c8a`.
Its minimal and scale runs retain all 40 samples per direction:

| Named journey group | Minimal 1x / 4x worst directional p95 | Scale 1x / 4x worst directional p95 |
| --- | --- | --- |
| Disclosure, Review and discovery opening/return | 32.7 / 33.0 ms | 32.8 / 110.4 ms |
| Search and restoring the full selection | 25.9 / 27.4 ms | 28.9 / 144.0 ms |

These runs pass their budgets: maximum 1x feedback is below 50 ms, maximum 4x
feedback below 200 ms, with no warmed 1x long task over 50 ms. Earlier failed
inventories and every outlier remain retained. Event, remaining navigation and
the matched baseline matrix are still being completed; this is not a blanket
workstream pass.

Published custody evidence on the same native/query-local implementation,
before the CSS-only Review wrap, is retained in
`published-native-custody.json`, `published-live-worker-waiting.json` and
`published-after-update-custody.json`. Rapid typing survived blur; three hidden
complete artifacts survived view-only actions. Private answer text matched no
metadata. Deliberate reveal stayed local; revocation removed the secret,
preview and output, and reconsent restored none. Offline edits produced three
explicit drafts without reload. One observed page request contained no
sentinels; no storage writes or worker/MessagePort messages carried answers,
and IndexedDB was empty. Scans covered 78 active public cache responses and
156 across both waiting-update generations, with no sentinels.

The update waited without replacing its controller or reloading the live
workspace; all three drafts remained. After synthetic workspace cleanup and
owned-tab closure, a new workspace booted with no waiting worker and one public
cache generation. Actual clipboard/download transfer, screen-reader speech,
physical mobile and browser UI zoom are not certified. Keyboard focus,
accessible search naming/status, light 390-pixel and dark RTL 320-pixel layouts
were inspected; CSS 200% Review reflow is explicitly an emulation.

The final candidate additionally indexes root input/schema mounting, reuses
canonical root IDs in budget/snapshot traversal, and defers value-free entry
materialization until a consumer requests it. Static Lazy factories capture
immutable metadata and value-free states, not the live form or private answer
map. Final Store inventory is
`b0763bf07a89688d03c87b10523ec419a355a75e78121f46a9901ae91d767eee`.
Activation clearing/setting passed at 1x with p95 32.9/42.1 ms, maximum
feedback 34.8/48.4 ms and no warmed long task; at 4x p95 was 130.6/180.7 ms,
maximum feedback 151.9/199.6 ms. All 40 samples per direction remain in
`candidate-event-canonical-root-ids-activation-{1,4}x.json`.

Final-package Event disclosure, Review, discovery and search also passed both
CPU rates. Worst directional p95 was 32.6 ms for controls and 29.0 ms for
search at 1x, 60.5/66.5 ms at 4x. Minimal disclosure, Review, discovery,
search, section, experience and facet selection passed both rates; all 40
samples per direction and the 4x search outlier remain in the
`candidate-*-final-b076-*.json` evidence. Scale and the remaining comparison/
lifecycle matrix are still in progress; these are bounded results.
The subsequent scale run passed controls, search and section navigation at
both rates but failed experience-switch acknowledgement: 59.4 ms at 1x and
221.8 ms at 4x. Final-content p95 remained 54.3/206.9 ms. These failures
remain in `candidate-scale-final-b076-experience-{1,4}x.json`; meeting p95
does not waive the feedback ceiling.

The reversed scale experience mounts list rows and nested editors. The field
template previously recalculated the same logical control identity for every
ID/help/error reference within one render. Refreshing that metadata identity
once per parameter application preserves row-token changes without caching
answers. Its Browser suite passed 182/182 and the release-owner publication
exited 0 with Store inventory
`78fd02a00d235e8306dffe3f77429bcd1d13f4d23db86779329581019c227c22`.
That candidate and the subsequent canonical-section-ID candidate still failed
the feedback ceiling. The value-free visibility fast paths alone also failed;
all runs remain recorded. Experience changes additionally retained an
unnecessary heading-focus request after replacing the section. Actual focus
was the section H3 rather than the persistent experience selector.

The candidate now retains selector focus on experience changes; explicit
workflow, section and correction navigation still focus their destinations.
Inventory
`07cdcd419b8c22b40e63b8c019ad5ca4b2a942652c77f9f6d82d1dd0566611d0`
passed both experience directions, retaining 40 measured samples each:
at 1x worst directional p95 48.7 ms, maximum feedback 49.4 ms and no
warmed long task; at 4x p95 166.1 ms and maximum feedback 195.5 ms.
The 200.5 ms final-content maximum remains recorded: the final-content gate
is p95 <=500 ms at 4x, separately from maximum feedback <=200 ms.
Focus remained on `configuration-experience`. The same inventory subsequently
passed scale facets, discovery paging, Review paging, disclosure, Review
entry/return, discovery opening/return and search at both rates. Full totals
remain 1,000 settings, including 375 required leaves; Review mounts 50 rows.
Literal page boundaries were `enabled` and `setting049`. Worst 1x control p95
was 38.5 ms with maximum feedback 39.1 ms and no warmed long task. At 4x the
largest facet p95 was 164.4 ms, largest search p95 145.3 ms, and maximum
feedback across these additional runs was 164.0 ms. Raw first-use, warmups,
all 40 samples and outliers are in `candidate-scale-selector-focus-*.json`.
Remaining final-package fidelity and comparison evidence is still required.

Exact-final minimal controls, search, section/experience, facets and ordinary
answer edits passed at both rates, retaining 40 samples per direction. At 1x
the largest control p95 was 33.0 ms; ordinary edits p95 was 30.2 ms with maximum
feedback 13.7 ms. All warmed 1x tasks remained below 50 ms. The retained 4x
discovery outlier was 135.0 ms final/119.2 ms feedback, within its separate
4x gates.

`published-final-selector-focus-custody.json` repeats custody on the exact final
inventory rather than inferring it from earlier packages. Rapid character input
survived blur. Three complete artifacts survived disclosure and a query matching
private answer text found no metadata. Deliberate reveal stayed local. Revocation
removed all secret controls, previews and artifacts; reconsent restored an empty
secret and no output. Ordinary input survived and three explicit drafts prepared
offline with unchanged document identity. The scan found no sentinel in 78 cache
responses, storage, IndexedDB or requests; observed storage writes and worker/
MessagePort messages were zero. One observed page request contained no sentinel.

`published-final-internal-discard-policy.json` verifies actual Cancel retained
the ordinary input and all three drafts; Confirm removed the workspace/output.
Internal history reopened clean defaults with consent off and no output, while
document time origin stayed unchanged throughout. This cancellation check used
an isolated policy-aware Chrome 153.0.8010.52 process, not the timing browser:
the durable CDP transport unconditionally accepts dialogs. Native pointer
activation and exact dialog/DOM signals avoid a false load wait after Cancel
or an SPA history action. No private values appear in its report.
`published-final-abandoned-history.json` uses the timing browser's native
navigation to the other loopback origin and back. Recovery started a fresh
realm, with no former ordinary input, secret consent or prepared output.

Final scale inspection reached page 20 of 20 with 50 mounted results, all 1,000
matches retained and Next disabled; the last root was `list124`. Keyboard
Enter operated both pager directions. A page-20 nested correction selected
`section19` and focused `list124.0.label`. Inspection exposed off-screen focus:
explicit field focus had prevented scrolling. Inventory
`5964a15218c58b4855523f01837f633610ea975ec986aa9a2ef00a2f06e00fe8`
permits native scrolling for requested field/empty-list focus. Its Browser
suite passed 182/182; the actual corrected input was fully visible
(top 397.3, bottom 447.3 in an 844-pixel viewport), with no horizontal overflow.
Dark RTL 320-pixel screenshots show the focused control and wrapped Review
actions; light 390-pixel screenshots show the last discovery page and pager.
The earlier off-screen screenshot remains evidence of the repaired defect.

Real list editing retained the surviving row token and text after deleting its
sibling and switching sections. On the focus-fixed package an invalid numeric
draft survived section remount, stayed invalid and blocked preparation with a
reachable correction. Searching the declared `list124` key returned three
matches, removed paging and retained search focus. These checks supplement,
not replace, the semantic regression suites or unexecuted speech/device tests.

Facet input uses verified native CDP Space dispatch: the installed automation
checkbox click produced an untrusted event, and its `press('Space')` produced
an empty key. Neither failed automation attempt counts as a product timing
sample. Native key dispatch produced trusted click/input/change; sampling
awaits the resulting complete selection count and frame.

The preceding native closure passed exact-owned no-write formatting, solution Release
build, 83 Terminal tests and four package tests. Presentation 152/152 and
Browser 182/182 remain passing affected-owner evidence. Plain inventory is
`4c915133a55c913989db3bc86158efb0a7c70d0e7315dca52818df51de770ea5`.
Subsequent metadata/focus changes passed Presentation 152/152 and Browser
182/182. Final focus-fixed closure again passed owned no-write formatting,
the solution Release build, Terminal 83/83 (2.447 s), plain publication and
package 4/4 (0.650 s). Plain inventory is
`1c040404c3a8df65ce07cb0b2a0afbdf444a947b33994ff250985e749a09b83f`;
Store inventory is `5964a15218c58b4855523f01837f633610ea975ec986aa9a2ef00a2f06e00fe8`.
Desktop remained 182/184 (3.511 s), only the independently reproduced failures.
Final Event controls/search passed both CPU rates, but activation did not:
activating 1x p95 50.3 ms and maximum feedback 53.2 ms; clearing feedback
45.8 ms. No warmed 1x long task was observed. All samples remain retained;
4x candidate activation was not executed after this failed lane. Qualification
therefore remains open despite passing native closure.

The preserved Event comparison is now complete at both CPU rates. All 40
samples in each direction, first-use and five warmups remain in the evidence.
Worst directional p95, in milliseconds:

| Event-environment journey | Untouched baseline 1x / 4x | Focus-fixed candidate 5964 1x / 4x |
| --- | --- | --- |
| Disclosure | 11196.3 / 12352.9 | 31.9 / 39.9 |
| Review entry/return | 1715.1 / 8156.0 | 32.6 / 55.2 |
| Discovery opening/return | 32.3 / 31.1 | 32.4 / 46.3 |
| Search/restoring full selection | 3168.1 / 12430.6 | 30.5 / 62.4 |
| Activation clearing/setting | 3231.5 / 12606.5 | 50.3 / not run after failed 1x feedback |

The first baseline 1x disclosure run includes the previously recorded high
host-load outliers; no sample was removed or normalized. Baseline native
discovery opens quickly because its expensive descendants were already mounted,
not because their construction was free. These comparisons establish the named
observations, not a universal speedup or a candidate pass while feedback fails.
Desktop again passed 182 of 184, failing only the two baseline assertions below.
The combined verification command exits 2 because of that final Desktop run;
it must not be reported as an entirely green command.

#### Frozen indexed-edit candidate (2026-10-23)

The final Store inventory is
`58a14e6497c566992e7ec6c862f2f2d47c356deb8cb0d3137675eb9f5699a864`.
It supersedes the intermediate failed candidate gates above, without deleting
their samples. Four validated edit/collection paths now use the admitted
field-position index instead of `Single` scans that decoded every root ID.
Review relevance prefixes are deferred from immutable value-free entry states;
field rendering reuses access/metadata reads only within the current render.
Core evaluation, scheduling and preparation are unchanged.

All 92 directional rows pass the unchanged budgets: 3,680 measured samples,
five warmups per direction and separate first-use observations. Maximum feedback
is 47.8 ms at 1x and 183.9 ms at 4x; no warmed 1x long task exceeds 50 ms.
Worst directional p95, in milliseconds:

| Journey | Final candidate 1x / 4x | Untouched scale baseline 1x / 4x |
| --- | --- | --- |
| Event activation | 38.8 / 144.0 | Not a scale journey |
| Event controls (worst) | 32.8 / 50.7 | Not a scale journey |
| Event search | 25.3 / 52.5 | Not a scale journey |
| Minimal controls (worst) | 32.7 / 32.9 | Not a scale journey |
| Minimal search | 25.8 / 28.9 | Not a scale journey |
| Minimal ordinary input | 30.5 / 28.7 | Not a scale journey |
| Scale disclosure | 32.4 / 78.9 | 6135.5 / 24560.5 |
| Scale Review | 31.8 / 65.2 | 4194.0 / 16801.6 |
| Scale discovery | 32.6 / 57.9 | 32.9 / 172.8 |
| Scale search | 31.5 / 119.9 | 6410.7 / 25637.1 |
| Scale section | 32.0 / 51.7 | 2093.2 / 8733.8 |
| Scale experience | 44.4 / 158.2 | 7882.3 / 31903.9 |
| Scale facet | 33.0 / 136.4 | 6205.1 / 24891.8 |
| Scale discovery page | 33.2 / 68.0 | No baseline pager |
| Scale Review page | 32.7 / 44.8 | No baseline pager |
| Scale correction/return | 32.2 / 64.4 | Not sampled |

Minimal baseline ordinary input is 29.4/39.6 ms; the candidate does not make
every small-form interaction faster. Scale retains all 1,000 matching nodes
and 375 required leaves while rendering 50 results. Baseline discovery already
mounted descendants, so its native disclosure timing excludes construction.
The original scale source/digest and baseline inventory remain unchanged.

Raw artifacts under `artifacts/browser-performance/evidence/` are
`candidate-{event,minimal,scale}-indexed-edit-final-{scenario}-{rate}x.json`,
`candidate-event-indexed-edit-lookup-activation-{rate}x.json`,
`baseline-scale-preserved-{scenario}-{rate}x.json`,
`baseline-minimal-preserved-navigation-{scenario}-{rate}x.json` and
`baseline-minimal-preserved-final-ordinary-input-{rate}x.json`.
`candidate-indexed-edit-final-budget-audit.json` records the row audit.
Pager samples for the preceding `07cd` inventory remain separately named
`candidate-scale-selector-focus-*`; final pager evidence has the final prefix.
Final scale transport/document observations are in
`candidate-scale-indexed-edit-final-transport.json`.

Sampling was serial, without concurrent builds. Seven completed task-owned
browser tabs were retired before the final matrix. Earlier host-load outliers
and failed candidate observations remain retained. Chrome emitted negative
frame-latency diagnostics between -0.035 and -0.093 ms during the final scale
run; no sample was discarded in response. These are measurements on the named
workstation/browser, not certification of other devices or assistive technology.
Final native/package closure (`mon_4JCRK421H257ZSV6` / `bash_90`) passed
owned no-write formatting, the solution Release build, Terminal83/83 (2.857 s),
plain publication and package4/4 (0.590 s). Plain inventory is
`67a93f64c3344bd446d590465bd02500389bfc357fbfff6862983f573dec1a02`.
Presentation152/152 (0.693 s) and Browser182/182 (2.323 s) passed for this source.
Desktop182/184 (2.936 s) again failed only the two independently reproduced
baseline assertions below; the combined closure command exits2, not zero.

`published-indexed-edit-final-custody.json` binds the frozen inventory to rapid
secret typing/blur, three complete masked artifacts, view-only preservation,
deliberate local reveal, revocation clearing previews/output, blank reconsent
and three offline drafts without reload. It scans78 public cache responses;
IndexedDB is empty, storage writes and worker/MessagePort messages are zero,
and the one observed request contains no synthetic answer sentinel. Final
cleanup disables consent and retires output. Actual clipboard/download transfer
was not performed. Earlier waiting-worker and cancellation/history evidence
remains separately inventoried above, rather than falsely relabeled as new runs.

`published-indexed-edit-final-layout.json` and the
`final-indexed-edit-{correction-dark-rtl-320,review-light-390,review-light-desktop}.png`
screenshots retain inspected final-package surfaces. The exact nested correction
`list124.0.label` is visible at top396.9/bottom446.9 in an844-pixel viewport,
with no horizontal overflow. Review retains1,000 results and mounts50.
Desktop Review focus settles on its heading. Physical-device, screen-reader
speech and actual browser UI zoom remain unverified; earlier CSS200% reflow
is not represented as browser zoom.
Final collection add/remove and section remount preserve the surviving row's
control identity and text (`published-indexed-edit-final-row-identity.json`).
Searching the synthetic ordinary answer returns zero metadata matches and
retains search focus; final sink inspection still contains no sentinels.

Native/engineering baseline findings are separate:

- Desktop: 182/184 passed; `AtomicImportRejectsConflictAndCommitsOneRevisionWithoutLeakingAnswers`
  and `OptionalEditorControlRestoresDefaultsWithoutHidingActiveCorrections`
  failed. Both exact assertions reproduced against an untouched `git archive
  HEAD` export (0/2 passed), without switching the shared checkout. They remain
  quarantined; no assertion was weakened.
- Terminal: all 83 passed after the seeded navigation fixture explicitly
  restored authored disclosure. Seeding required answers is edit intent under
  pure correction promotion; the fixture now resets the experience before
  asserting its authored initial state.
- The solution-wide no-write format check found existing CLI/Core whitespace
  and credential-test import ordering outside this change. Owned formatting
  corrections were applied with native patches, and the exact owned-file format
  check passed. No diagnostics were suppressed.

### Browser editor redesign qualification in progress (2026-10-23)

The current renderer is `9a6c3574d913f69aa1a84eb8d5740efd42dc5ca2`.
It refreshes revision-bound choice callbacks while skipping unchanged visual
output, uses a full-width MudInput with a connected scalar error, and mounts
omission menus on first use. Cold activation and menu close restore activator
focus; the native help trigger opens the existing workspace-owned inspector.
The Release Browser build succeeded and the full Browser owner passed 199/199.
After two test event calls were made explicitly awaited, the affected workflow
and help classes passed 11/11 in 1199 ms. Native commit hooks passed for the
renderer and its independent deterministic budget-test increment `b0c9d5b`.

Final interpreted Store/plain publication and the Architecture Release build
exited 0. Store package tests passed 4/4 (738 ms), and Browser package tests
passed 2/2 (594 ms). Store inventory SHA-256 is
`ff55ff80c3308fb9d0528b23bbad73d8a6b3acbb94c9294ac99471100b67dbeb`;
plain inventory is
`afd42bff2c290c4b05f0466a49c7a02becf289cda4d0aa04748d680f9cee22a4`.
These checks establish package closure, not latency acceptance.
The subsequent runtime check rejected this inventory: after loading the public
assets, Mono reported `NIY encountered` in `LocalizationOptions` and an
interpreter assertion. Only the 32 minimal timing rows completed; other lanes
timed out before sampling. Thus the package is not startup-qualified despite
passing all six package tests. Native cleaning and isolated interpreted
republication were required before further qualification; the original failed
package and `final-startup-failure.json` remain retained.

Ordinary native Clean followed by explicit interpreted publication produced
the same failing inventory and left AOT intermediates present. A separate
locked restore and publication using SDK `UseArtifactsOutput=true` with a
task-owned `ArtifactsPath` isolated all project intermediates. The first
isolated attempt lacked restored SDK imports and failed `ResolveWasmOutputs`;
separating restore from the publish evaluation corrected that command sequence.
The isolated Store inventory is
`90ffb31c48c7c28e84d15b8451801550bf6a8c53d3e4dc3f3887aadda5605b3a`.
Its localization payload differs from the rejected package, and actual Event
controls render without the interpreter assertion. A desktop screenshot was
inspected. This supports stale AOT-generated output as the startup cause;
no product change or dependency was needed. Both isolated package gates passed:
Store 4/4 (800 ms) and Browser package 2/2 (807 ms). Timing must use this isolated
package, not the earlier inventory or its incomplete matrix.

The isolated candidate now has all 128 complete directional rows: the original
92 plus 36 detail/density/help rows, each with separate first use, five warmups
and 40 trusted samples. Four incomplete Event attempts remain retained;
explicit page activation and settled operation setup allowed complete Event
lanes without a product change or stitching partial directional samples.
The final audit applies the original p95 limits to existing rows and S25's
100/400 ms limits to new transitions, with feedback strictly below 50/200 ms.
An inherited generic 500 ms metadata value in raw files does not override S25.

All final-content p95 checks pass, but only 110 of 128 rows pass every gate.
Eighteen fail feedback or warmed 1x long-task limits. Representative maxima:

| Transition | 1x feedback / longest task (ms) | 4x feedback (ms) |
| --- | --- | --- |
| Event activation, set | 130.6 / 117 | 696.0 |
| Event activation, clear | 215.4 / 157 | 375.5 |
| Scale disclosure, expand | 80.8 / 0 | 265.4 |
| Scale Inspect, enter | 91.1 / 71 | 440.4 |
| Scale correction, enter | 246.4 / 232 | 302.8 |
| Scale correction, return | 149.6 / 85 | 320.3 |
| Scale detail, expand | 56.4 / 0 | 269.5 |

Minimal search return, scale Review paging, Event Review return and Event
density return also have retained failing feedback outliers. The audit is
`isolated-final-matrix-audit.json`; raw samples are in
`isolated-matrix-*.json` and `isolated-Event-completion-*.json`. No sample was
removed or normalized. This is a failed qualification, not an accepted latency
result or proof that every failure is environmental.

Observer correction, 2026-10-24: `observer-drain-probe.json` exercised eight
trusted callbacks that intentionally occupied 80 ms. Teardown without
`PerformanceObserver.takeRecords()` missed all four tested tasks; draining
queued records detected all four. The measurement adapter now drains before
disconnecting, without changing timestamps, predicates, sample counts or
budgets. Earlier zero-long-task results are therefore not evidence that no
long task occurred. The raw rows and their feedback/p95 results remain retained;
final long-task qualification requires the corrected observer.

### Committed AOT editor candidate (2026-10-24)

The final product source is `2392c03968be641cb44bd9f32993c4e359d00e69`.
Its corrective increments are deterministic duplicate-row event completion
(`3efa33a`), immutable admitted field metadata (`2334137`), bounded connected
section mounting (`58c24df`) and Release AOT with pinned builder prerequisites
(`2392c03`). Core 145/145, Presentation 155/155 and Browser 208/208 passed.
The Release Browser build, 9 Action pins, 87 dependency-policy entries and
2/2 AgentWorkflow checks passed. Two ReleaseEngineering cases did not pass:
repository release-input policy and the unchanged loopback GitHub CLI header
case (`TaskCanceledException`); neither was suppressed.

Both final governed publications completed with exit 0 from this committed
source, without an AOT command-line override. The roots are
`artifacts/browser-ui-ux-redesign/final-store` and `final-plain`.
`BrowserStorePackageTests` passed 4/4 (617 ms); the standalone Browser package
checks passed 2/2 (443 ms). Store inventory SHA-256 is
`710a32028c9dfe0bf90e7d2a65e85aba1ee977e937eafad3b1f5f8ac6f0839ea`;
plain inventory is
`453831b6fa840fb1f2192eb6b1a18fb3f0d9fa9282795991d0fe44b873722b51`.
These package checks do not establish runtime qualification.

Final-package startup was observed at the isolated loopback port 5144 and its
desktop screenshot inspected. The first 76 complete timing directions
(minimal 44, Event 32) pass their original gates with the corrected observer.
Every direction retains separate first use, five warmups and 40 trusted samples.
Event activation feedback maxima are 25.5/22.7 ms at 1x and 112.0/108.3 ms at 4x.
Recorded 4x long tasks remain in the raw evidence; the no-long-task gate applies
to warmed 1x. No warmed 1x long task was recorded in these 76 directions.
Raw files: `final-aot-minimal-raw.json` and `final-aot-event-raw.json`.
The scale cohort subsequently completed. The final audit has 128 unique complete
directions: the original 92 and 36 detail/density/help directions. All 128 pass
their unchanged budgets, including the original search exception and strict
feedback limits. Maximum final-content p95 is 82.5 ms at 1x and 231.4 ms at 4x;
maximum feedback is 29.7 ms and 112 ms respectively. No warmed 1x long task was
recorded. The 4x long tasks remain recorded and are not discarded. Complete
content includes zero pending mount batches plus the existing frame boundary,
not merely a loading indicator. See `final-aot-scale-raw.json` and
`final-aot-matrix-audit.json`; all failed earlier candidates remain retained.

Actual final-package custody revalidation prepared three complete artifacts,
kept the synthetic secret masked and returned zero metadata-search matches for
its value. Denied clipboard permission reported `clipboard-unavailable`;
deliberate permitted copy and reveal contained the expected synthetic content.
A completed 72-byte download was inspected, then removed. Revocation removed
secret controls and prepared/revealed output; reconsent started empty.
Before entry, page observers covered Storage writes, Worker, MessagePort,
ServiceWorker, IndexedDB writes, fetch and XHR. A passive CDP request listener
observed only the public `browser-artifacts.js` GET. No sentinel appeared in the
83 cached public responses, one appearance-storage entry, zero IndexedDB
databases or observed message/request sinks. This is bounded observation of
those sinks, not universal coverage of browser APIs. Clipboard was cleared and
isolated permissions reset. See `final-aot-custody.json`.

The final desktop-light help dialog preserved scroll 408 and the input's exact
790x44 rectangle; Escape returned to its named invoker. At 390 px, dark,
forced-colors, RTL and asserted coarse-pointer mode, the help trigger was 44x44,
the dialog 326x780 with reachable Close, and no horizontal overflow. Search
preserved header height 196.1875, scroll 1130 and field top 400.484375.
At 320 px light/LTR, search preserved header height 196.1875, scroll 1281 and
field top 399.78125, returning focus to the input. Both phone screenshots and the
desktop screenshot were inspected. See `final-aot-surface.json`.
An isolated native X11 Chrome window also loaded this final inventory and used
actual browser 400% zoom, not emulated page scale: viewport 359x214, DPR 4,
visualViewport scale 1. The edited input remained unobscured at top 85.04296875;
the short-viewport header scrolled away. Keyboard search preserved scroll 1498.25
and the exact input rectangle, kept its query inside the viewport and returned
focus on Escape. Native zoom was reset afterward. The screenshot was inspected;
`final-aot-native-zoom.json` records the geometry. This does not establish
physical magnification hardware, screen-reader speech or independent text-only
zoom qualification.

With the final worker controlling the page and `configuration-rendering.js`
present in public cache, CDP network-offline emulation followed by a reload
still rendered the pinned minimal workspace and prepared three draft artifacts.
Network emulation was restored afterward. `navigator.onLine` remained true, so
this is the recorded CDP-disabled-network scenario, not an OS connectivity claim.
See `final-aot-offline.json`.

Timing used Chrome 151.0.7922.34 at 1440x900 on the recorded Ryzen 7 7800X3D
reference host, sequential workspaces at 1x/4x CPU rates, with no concurrent
native build. Public minimal/Event digests and the synthetic scale fixture's
SHA-256 are retained in `final-aot-environment.json`. The final plain package's
62 framework Brotli siblings total 7,999,831 bytes; this is not measured initial
network transfer. Published directories occupy 60,047,933 bytes plain and
60,712,305 bytes with Store, including uncompressed/sibling files and metadata.
The earlier matched interpreted/AOT payload comparison remains recorded above;
these governed final totals are not substituted for that matched experiment.

The attempted forward correction to the existing definitions fragment's
browser group was rejected by the native commit hook with
`staged_fragment_mutation`. That edit was restored and unstaged. Group validation
examines all fragments, so supersession cannot correct that group. Its
mixed-scope failure remains owned workstream release metadata debt alongside
the eight historical detail markers and unrelated guided-experiences group.
No hooks were bypassed and no history was rewritten.

Docker publication was not exercised: neither the configured Desktop socket nor
`/var/run/docker.sock` exists. Native governed publication is the executed route;
remote CI, physical devices and assistive speech remain unqualified.

### PR CI repair verification (2026-10-25)

The first PR run failed before product verification: native and public-data jobs
lacked the workload set pinned by `global.json`, and the standalone dependency
audit inspected Browser.Host without restoring its independent graph. The next
run passed Linux x64 CLI/package and public-data, exposing actual cross-host and
owner-test failures rather than bypassing them.

Linux Arm64 used x64 `open` flags. A literal ABI regression failed with expected
directory flags 573440 versus 720896 before `d0fe5d8` selected process-specific
flags. macOS authoring fixtures encountered system temporary-directory aliases;
the native job now uses the runner-owned temporary root without relaxing symlink
admission. Windows bundle fixtures now use native path composition. The real
post-installation CLI cases retain their Linux assertions and additionally
require unsupported hosts to refuse without installing output.

The subsequent Windows CLI suite passed all 138 tests, then native publication
rejected `browser-release-capabilities.json` as stale. Indented JSON used the
host newline convention. The ratchet writer and both definition-schema owners
now normalize to LF before byte comparison or hashing, matching the existing
command-schema contract. Generated outputs remain generator-owned.

After all five native package jobs passed, the standalone runner shut down
during ReleaseEngineering tests. Its unchanged-commit retry reached a separate
Browser collection-test race: a row expected to contain `blue` was still empty.
The collection workflow tests now await input, click and change dispatch before
re-querying the next control. Existing typed-value, row-identity, omission and
privacy assertions remain, with an additional rendered-row value assertion.
No timing delay or product callback-fence change is involved.

The next candidate passed all 208 Browser tests but repeated the runner shutdown
about four minutes into ReleaseEngineering tests. Investigation found that
`MainCommand` passed a negative process-group identifier to `kill` without an
end-of-options delimiter. The local host uses util-linux; procps documents an
argument-parsing hazard for that form. Cleanup now delimits the owned group with
`--`, retaining its TERM/KILL sequence and process ownership.

The existing hung-Git regressions use a strict external signal wrapper that
records arguments and refuses ambiguous operands before invoking the native
utility. The unmodified helper failed safely with four recorded arguments
instead of six. Existing timeout, process-exit and descendant-cleanup assertions
remain. The affected Release test-project build had zero warnings/errors, and
the complete 468-test suite passed with no failures or skips. The post-fix full
run is retained in `artifacts/ci-repair/release-signal-fix.log`.

Source register, accessed 2026-10-25:
[procps issue 354](https://gitlab.com/procps-ng/procps/-/issues/354) supplied only
the negative-operand ambiguity as a functional fact. A separate implementer
received a source-free handoff to delimit that operand and used only the
repository helper; it read no third-party source. Parent AFC/SSO review found
only the standard command-argument boundary added to existing structure. No
upstream implementation, prose, dependency or asset was transferred.

The Desktop failures were stale scenarios: imports may replace pristine defaults,
and corrected editors remain visible until deliberate collapse. Corrected setup
and additional assertions preserve both contracts. Release-input validation now
distinguishes reserved confidentiality markers from ordinary public descriptions
and admits digest-bound group-only corrections. All original fragments remain
unchanged; committed correction custody and exact-tree candidate/inventory
provenance are verified rather than exempted.

Local verification used SDK 10.0.302 and
`TMPDIR="$HOME/.cache/agent-tmp"`. Every row below passed with zero failures and
zero skips:

| Executed command | Result |
| --- | --- |
| `dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 145 --no-progress` | 145 passed |
| `dotnet test --project tests/Configurator.Artifacts.Tests/Configurator.Artifacts.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress` | 48 passed |
| `dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --no-build --verbosity quiet -- --minimum-expected-tests 208 --no-progress` | 208 passed |
| `dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 138 --no-progress` | 138 passed |
| `dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --no-build --verbosity quiet -- --minimum-expected-tests 185` | 185 passed |
| `dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 468 --no-progress` | 468 passed |
| `dotnet test --project eng/agent-workflow/tests/Configurator.AgentWorkflow.Tests/Configurator.AgentWorkflow.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress` | 2 passed |
| `dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress` | 58 passed |

Locked solution restore, Browser.Host Release build, every graph in the exact CI
dependency-audit enumeration, 9 Action pins and 46 definition-store self-checks
also passed. The native release owner assembled CLI, Terminal and Desktop Linux
x64 packages, then all three AOT Browser publication variants used by CI: separate
Store output, Store in the default destination and a subsequent plain publication
in that same destination. Architecture checks consumed those actual packages.
No-write formatting passed for every changed C# file.

After the Windows newline repair, the native definition-schema and ratchet
generators ran in both `--write` and `--check` modes. Both schema files and all
three ratchets remained byte-identical to the committed Linux outputs; the
freshness checks passed. The affected Core Release build had zero warnings and
errors, and the Core/CLI suites above passed again.

The real CLI executable returned doctor success, displayed help and rejected an
unknown command with exit 64 and a bounded machine receipt. Its transcript is
retained locally in `artifacts/ci-repair/manual-cli.log`. The final full release
run is retained separately in `artifacts/ci-repair/release-owner.log`, avoiding
the default report's replacement by a later targeted run. An independent gate
approved code readiness; it did not claim remote completion.

Final cross-host workflow execution was still pending when this record was
committed. The resulting commit's run results belong to PR #1's Checks tab,
not this local matrix. This CI repair does not renew browser timing, speech,
physical-device or native GUI qualification, activate signed publication, or
close the mandatory redesign follow-ups.

### Retained earlier interpreted-package evidence

The following observations belong to their recorded earlier inventories, not
to the committed AOT candidate above.

Actual final synthetic custody checks prepared three complete artifacts,
kept the credential masked, found zero metadata-search matches for its value,
reported denied copy, then exercised deliberate permitted copy, reveal and a
completed 96-byte download whose content was inspected. Revocation removed
artifacts and secret inputs; reconsent started empty. Storage and 82 public
cache entries contained no sentinel; IndexedDB had no databases. The listed
storage/worker/port/IDB/fetch/XHR observers were installed before entry.
A separate passive CDP network observation began before fresh reconsent and
entry, covered search/preparation/revocation, and observed zero requests.
Synthetic clipboard content was cleared and isolated permissions reset.
See `isolated-final-custody.json` and `isolated-final-network.json`; the latter
does not infer network-body coverage from JavaScript Request serialization.

The final desktop-light help check retained scroll 362 and the input's exact
790x44 rectangle. Enter opened the named dialog; Escape returned to its
invoker. In 390px dark-high-contrast RTL, actual coarse-pointer emulation
(`matchMedia` asserted) produced a 44x44 trigger, a settled 326x780 dialog,
reachable Close and no horizontal overflow. The first phone observation lacked
touch emulation and sampled the opening animation; it remains retained rather
than being presented as the coarse result. Both final screenshots were inspected.
Physical-device, assistive speech and stakeholder qualification remain absent.

A later nullable MudCheckBox experiment passed 200 Browser cases but was not
adopted. In balanced 1x activation measurements, its set/clear p95 was
64.4/64.3 ms versus the retained select's 59.2/54.6 ms. Feedback remained
72.7/52.9 ms versus 61.6/65.1 ms, and the checkbox set direction had a 69 ms
long task. Separate first-use, five warmups and all 40 trusted samples per
direction remain in `nullable-boolean-activation-comparison.json`.
Actual accessibility inspection also exposed a null-state mismatch: the
native input reported false in Chromium's accessibility tree despite visual
Not supplied and `aria-checked=mixed`; setting native `indeterminate` fixed
that diagnostic state, but did not justify adopting the slower renderer.
All five experiment source/test diffs were restored to the committed baseline.
The failed experiment does not establish a defect in the retained select.
Restoration was verified by an empty diff on all five experiment files.
The restored suite initially passed 198/199: the manifest workflow queried
tenant row 2 before its asynchronous add completed. Test-only commit `9c88768`
awaits the existing input, row, preparation and export tasks without changing
the zero-to-three-tenant expected JSON or assertions. The final full Browser
run passed 199/199 in 4008 ms, exit 0. Product source and the retained
startup-qualified package remain unchanged by that test correction.

An isolated AOT experiment used the already-installed SDK workload without
changing project or release defaults. It reduced matched mounting costs but
still failed feedback budgets: at 1x its Inspect feedback maximum was 63 ms
against 50 ms; at 4x disclosure and Inspect reached 223.5 and 261.3 ms against
200 ms. The matched interpreted values were 86.1 ms at 1x and 355.2/428.1 ms
at 4x. All 40 trusted samples per direction, five warmups, first use and outliers
are retained in `aot-mount-comparison.json` and `aot-mount-4x.json`.
AOT increased the 90 Brotli assets from 3,910,306 to 7,936,430 bytes and would
add a release prerequisite, so it was not adopted. Neither experiment qualifies
latency. The complete interpreted matrix above is the current candidate
qualification; the historical observations below are not its acceptance.

The primary editor slice implements independent detail, session density,
metadata search, compact fields and contextual help. This is not whole-program
UI/UX completion; [mandatory follow-ups](../../dev/backlog/browser-ui-ux-redesign-followups.md)
remain separate. Qualification is still open.

#### Retained earlier candidate observations

At source `b6838912fede988e48fea9073956381a750bb410`, native Store and
Store-free publication completed, the Architecture Release build succeeded,
all four BrowserStorePackageTests passed (572ms), and both Browser package
tests passed (465 ms). SDK 10.0.302 and the workstation temporary-directory
override were used. Store asset inventory SHA-256 was
`4c554d4253fd38901cc9e91fb3a4e3bb2b9ba0d1ed170a40ad4d66020b88193e`;
Store-free inventory was
`df59cb6bfc3163e858f185bb6c86505d687db601cc521007bc604cf89220c2c0`.
Browser semantics have passing evidence for 195 cases, including replacement
with unchanged consent; this does not prove pointer or layout behavior.

Actual Chromium 151 interaction established 44 px desktop and phone choice
targets, connected combobox names, modal-aware Ctrl-K, and unchanged document
scroll 500/edited-field top 335.640625 when opening deep-search. Actual native
400% browser zoom then exposed an obstruction: the 196.199 CSS-pixel sticky
header covered the focused field in a 214 px-high viewport. The short-viewport
correction is committed as `6d964af`. Its final Store inventory is
`37352a59369eaab4457a2def4789f642f7426b7c9ecd1968a7f318b8894c4c8a`;
Store-free inventory is
`7c9966541b2b66c1d895eddfae6a5dd20af164aa75cd1ab6a0f7b5bc2eb93189`.
Release build/publications exited 0; all 8 affected shell cases passed (1125 ms),
Store package cases4/4 passed (729ms), and Browser package cases2/2 passed
(577ms). The first final Browser package invocation omitted the Store-root
environment variable and failed its Store variant on missing index.html;
rerunning with both explicit roots passed. No product repair or test weakening
was needed.

In that package, actual native 400% zoom produced 359x214 CSS pixels at
devicePixelRatio 4 on the owned 1440 px X11 screen. The field remained uncovered,
Ctrl-K kept scroll 1498.25 while fixing the entry at y=8 and results at y=72, and
Escape returned to that field without scrolling. A separate 1440x900 desktop
check retained sticky navigation, scroll 500 and field top 335.640625; 390 px phone
emulation retained a 44 px search trigger and no horizontal overflow. Native
zoom evidence is in `native-zoom-final/`; emulated widths are not native-device
evidence.

Further final-package observations used only public fixtures and synthetic
answers: 320 px RTL light high contrast and 390 px dark high contrast retained
full numeric errors without horizontal overflow. Browser default-font sizing
changed root text from 16 px to 32 px without overflow at 1440x900. Malformed numeric
draft text and connected error IDs survived detail/density/help; populated
collection rows retained exact input IDs and values. Help exposed neither
draft nor row value, trapped five successive Tab presses and returned focus.
Actual browser composition marked Ctrl-K `isComposing=true` and kept focus in
the draft; no-match search retained focus and offered working Clear search.
Offline edit, density, help and draft preparation retained one navigation
entry, with each output labelled "Draft — secrets still required."

Deliberate synthetic export exercised denied clipboard permission, successful
copy after granting permission, an actual completed 92-byte download and reveal.
Revocation removed output and secret controls; reconsent started empty. A scan
found zero sentinel hits in storage, 82 public cache entries, IndexedDB,
captured requests or observed worker/port messages. The synthetic clipboard
was cleared and isolated permissions reset. See local
`final-custody-observations.json` for observation ordering and limits.

The single primary gate review then found two search defects in `6d964af`:
phone opening grew the header, and visible-header Escape lost its keyboard
invoker. Actual 390 px reproduction measured header 196.1875->248.1875 and
scroll 1142->1194 (scroll anchoring retained field top 400.484375). Corrective
commit `5765d07` overlays the retained phone trigger space and prefers a
connected weak keyboard-invoker reference; outside dismissal clears it.
Final Store inventory is
`53f92b31c0f9a57a1d1674345ec1665d2b964d589c690a16147ac39069557e3a`;
Store-free inventory is
`3fddbe2bd5dfc11b470118f450f26fe628b8ccdc4e6f0160de5616b6bf459a86`.
Release publication exited 0, affected shell cases 8/8 passed (919 ms), Store
package cases4/4 passed (707ms), and Browser package cases2/2 passed (537ms).
The parent verified the review's fixes without a second reviewer or panel.

On the corrected package, phone 390 px retained header 196.1875, scroll 1142 and
field top 400.484375 across search; 320 px retained header 196.1875, scroll 1269
and top 399.78125. Desktop 1440x900 retained scroll 408/top 427.640625. Every
Escape returned to the keyboard invoker; outside dismissal retained the clicked
Menu focus, and pointer reopening fell back to the query. Actual native 400%
retained the earlier uncovered field/search geometry and scroll 1498.25.
Exact observations are retained in `search-gate-final-observations.json`.
A further synthetic custody run installed every listed page storage/worker/
service-worker/port/IndexedDB observer before consent or entry. After search,
revocation and reconsent, the secret was empty, output absent, and no sentinel
was found in 82 public cache entries, storage, IndexedDB or the one captured
request. Clipboard/download implementation did not change; those earlier
executions remain evidence for their unchanged paths, not new executions.

The gate's S34 test-evidence note was addressed by strengthening the existing
export lifecycle test: it captures the actual editor-mounted inherited field,
proves unmount and a different remounted instance, then holds export module
initialization across revocation, replacement and retired callback release.
All 5 class cases passed (1179 ms). This strengthens the composed schedule; it
does not imply that a secret resurrection bug was observed or repaired.

Those earlier timing attempts retain every sample, with first-use separate and
five warmups plus 40 samples per direction. At that checkpoint, the first 52 of 92
original rows included failing feedback and activation measurements; scale and
new presentation transitions were incomplete. The complete128-row result is
recorded above. A metadata-render suppression experiment worsened measured
activation and was removed. Under the same heavily loaded host (observed
load 50.61 on 16 logical cores), the preserved pre-redesign package also failed
matched activation: p95 352.8/319.3ms and maximum feedback350.6/515.6ms.
These observations neither qualify latency nor establish redesign causation.
Budgets and outliers are unchanged; this comparison does not resolve the 18
current failed gates.
Local ignored evidence is under `.omo/evidence/browser-ui-ux-redesign/`,
including `minimal-event-matrix-attempt-1.json`,
`metadata-chrome-activation-1x.json`,
`prior-package-matched-activation-1x.json` and
`native-zoom-400-failure.json`. Physical-device, speech and stakeholder
acceptance remain unqualified.

### Browser UI refinement evidence

The screenshot-driven interface refinement on 2026-10-10 corrected the vendor
cascade, added explicit shared native-control styling, refined shell/footer
composition, separated definition entry sources and grouped workspace controls.
The [interface anchor](BROWSER_UI.md#interface-refinement) records ownership,
independent design and official standards/API sources.

| Check | Executed result | Local evidence |
| --- | --- | --- |
| Shell baseline before edits | 5 passed, zero failures/skips; 1.339 s | Session command output |
| Final Browser owner semantics | 125 passed, zero failures/skips; 6.772 s | `.tmp/browser-ui-refinement-tests.log` |
| Browser Release build | Exit 0; zero warnings/errors; 2.91 s | `.tmp/browser-ui-refinement-build.log` |
| Changed C# format verification | Exit 0 | `.tmp/browser-ui-refinement-format.log` |
| Fresh Store-enabled and Store-free publication | Both exit 0 | `.tmp/browser-ui-refinement-publish.log`, `.tmp/browser-ui-refinement-without-store-publish.log` |
| Published asset/snapshot inventory | 4 passed, zero failures/skips | `.tmp/browser-ui-refinement-packages.log` |
| Local preview routing/assets | Nine HTTP 200 checks; missing framework asset HTTP 404 | Session `curl` checks, isolated port 5084 |

Executed commands used SDK 10.0.302 and
`TMPDIR="$HOME/.cache/agent-tmp"`:

```bash
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --treenode-filter "/*/*/BrowserShellTests/*" --minimum-expected-tests 5 --no-progress
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 125 --no-progress
dotnet build src/Configurator.Browser/Configurator.Browser.csproj --configuration Release --verbosity quiet
dotnet format Configurator.slnx --verify-no-changes --no-restore --verbosity quiet --include src/Configurator.Browser/Services/AppearanceThemeService.cs tests/Configurator.Browser.Tests/BrowserSourceEntryTests.cs tests/Configurator.Browser.Tests/BrowserStoreIndependenceTests.cs
dotnet msbuild eng/configurator/Configurator.Release.proj -target:PublishConfiguratorBrowser -property:Configuration=Release -property:ConfiguratorOutputRoot="$PWD/.tmp/browser-ui-refinement" -property:DefinitionStoreSourceRoot="$PWD/store" -verbosity:quiet
dotnet msbuild eng/configurator/Configurator.Release.proj -target:PublishConfiguratorBrowser -property:Configuration=Release -property:ConfiguratorOutputRoot="$PWD/.tmp/browser-ui-refinement-without-store" -verbosity:quiet
```

Package tests set `CONFIGURATOR_PACKAGE_ROOT` to
`.tmp/browser-ui-refinement-without-store` and
`CONFIGURATOR_STORE_PACKAGE_ROOT` to `.tmp/browser-ui-refinement`, then executed:

```bash
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --treenode-filter "/*/*/BrowserStorePackageTests/*" --minimum-expected-tests 4 --no-progress
```

The isolated Apache preview uses port 5084 and leaves the existing port 5081
preview intact. Syntax checking initially preceded publication and rejected the
missing document root; after publication it passed and the server started.
No browser was exposed by the automation inventory, and opening the in-app
browser returned unavailable. Visual, keyboard, screen-reader, zoom, RTL and
theme contrast verification for these refinements remain unexecuted. Earlier
browser screenshots and manual results below describe earlier assets and do
not qualify this refinement. No product references, dependencies, custody
policy or native owners changed, so unrelated owner suites were not rerun.

### Browser interface and optional store evidence

The authorized `browser-ui-and-definition-store` implementation was verified
directly in the checkout on 2026-10-10 with SDK 10.0.302. No history or remote
publication was recorded. The existing evidence below remains historical;
these results describe the new interface and optional-store integration.

| Gate | Executed result | Local evidence |
| --- | --- | --- |
| Locked solution restore and Release build | Exit 0; zero warnings/errors; build 10.74 s | `.tmp/browser-ui-and-definition-store/final-restore.log`, `final-build.log` |
| Full solution format verification | Exit 0 after targeted owned formatting; final router slice also exit 0 | `.tmp/browser-ui-and-definition-store/final-format-green.log`, `final-router-format.log` |
| Browser owner suite after visible-focus correction | 124 passed, zero failures/skips; 6.176 s | `.tmp/browser-ui-and-definition-store/final-browser-visible-green.log` |
| Presentation owner suite | 36 passed, zero failures/skips; 1.575 s | `.tmp/browser-ui-and-definition-store/final-presentation-tests.log` |
| Native store self-check and freshness | 26 passed, zero failures; two revisions generated and checked | `.tmp/browser-ui-and-definition-store/store-self-test-green.log`, `store-write.log`, `store-check.log` |
| Final secret-recovery focus slice | 3/3 privacy tests; affected format exit 0; actual focus moved to visible consent after recovery | `.tmp/browser-ui-and-definition-store/final-visible-focus-tests.log`, `final-visible-focus-format.log`, `delivery-recovery-focus-green.png` |
| Actual store-enabled and store-free publication | Both exit 0; source-absent publication also exit 0 and source restored | `.tmp/browser-with-store-visible-final-publish.log`, `.tmp/browser-without-store-visible-final-publish.log`, `.tmp/browser-without-store-final-absent-source-publish.log` |
| Actual package store/UI inventories and Browser closures | 4/4 and 2/2 passed against final outputs | `.tmp/browser-store-visible-final-package-tests.log`, `.tmp/browser-standalone-visible-final-package-tests.log` |
| Compiled product dependency closure | 7/7 passed | `.tmp/browser-ui-and-definition-store/final-compiled-closure.log` |
| MIT dependency policy and generated ratchets | Exit 0; 86 package/version pairs; ratchets current 3/3 | `.tmp/browser-ui-and-definition-store/final-license-policy.log`, `final-ratchets.log` |

Executed owner commands used `TMPDIR="$HOME/.cache/agent-tmp"` and MTP:

```bash
dotnet restore Configurator.slnx --locked-mode --verbosity quiet
dotnet build Configurator.slnx --configuration Release --verbosity quiet
dotnet format Configurator.slnx --verify-no-changes --no-restore --verbosity quiet
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 1 --no-progress
dotnet test --project tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 1 --no-progress
dotnet run eng/configurator/BuildDefinitionStore.cs -- --self-test
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --check
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser -p:ConfiguratorOutputRoot="$PWD/.tmp/browser-with-store" -p:DefinitionStoreSourceRoot="$PWD/store"
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser -p:ConfiguratorOutputRoot="$PWD/.tmp/browser-without-store"
dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .
dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --check
```

Package checks bound `CONFIGURATOR_PACKAGE_ROOT` to `.tmp/browser-without-store`
and `CONFIGURATOR_STORE_PACKAGE_ROOT` to `.tmp/browser-with-store`. They executed
`BrowserStorePackageTests` (minimum 4), `StandalonePackageTests/BrowserPackage*`
(minimum 2), and `StandaloneArchitectureTests/CompiledProductClosureHasNoEventOrTestDependency`
(minimum 7) through the Architecture test project with the same Release,
`--no-build --no-restore`, nonempty-count and `--no-progress` options.

Published acceptance used Apache 2.4.68 on three isolated localhost origins and
Google Chrome 153.0.8010.52 through Playwright. DevTools, Tavily and Context7 MCP
were unavailable; official API documentation supplied research facts. Package
worker identities are `RHpkSzhl` (store) and `A/UkP6ID` (store-free).
Screenshots live under `.tmp/browser-ui-and-definition-store/delivery-*.png`.
Observed outcomes:

- Home, Open, Help, Store and project details reflowed at 1440 and 390 pixels;
  320, 360 and 768 pixel RTL checks had no horizontal page overflow. The 360
  pixel viewport supplies 400%-equivalent reflow for a 1440 pixel window.
- System appearance followed emulated light/dark changes. All four explicit
  palettes rendered, with body contrast ratios 16.27, 16.67, 21 and 20.12;
  primary-button contrast exceeded 10 after transitions. Forced colors and
  reduced motion were inspected. These measurements do not certify every
  element or assistive technology.
- Escape within the inline navigation collapsed it and returned focus to Menu.
  Skip focused main without changing the route, answers or prepared artifacts.
  Cancelled departure preserved synthetic answers/output and trigger focus;
  confirmed departure removed the workspace before opening another route.
- A host returning 403 for every optional store file still installed the core
  worker, enabled controls and prepared three drafts without a store request.
  A reviewed store revision reopened offline. A store-free package admitted a
  local file, deliberately retained public metadata, reopened it offline and
  prepared a draft. A real independent public HTTPS file was admitted; a
  mismatched digest rejected with local upload still available. Copied source
  links contained pinned definition identity and selectors only.
- Secret controls were masked; complete preview and deliberate clipboard copy
  worked with synthetic values. Revocation cleared secret leaves and output,
  preserving ordinary answers. Synthetic sentinels were absent from local and
  session storage, public cache keys/text payloads and resource URLs; editing,
  preparation and revocation emitted no answer-bearing worker messages.
  Recovery originally removed its focused button and left focus on body;
  the final current-form consent action now restores focus to the existing
  consent control and scrolls it into view, verified in the published package.
- Event tenant insertion focused the new identifier. Duplicate-row review
  focused the second row; correction produced one manifest with exactly two
  distinct tenant names and no inactive artifacts. Narrow nested controls had
  no horizontal page overflow. Late operation rejection is separately covered
  by controlled owner tests; browser-managed updates left the existing realm
  on its old worker until clean reload.
- The clean final origin reported zero console errors or CSP diagnostics.

Download dispatch reported the intended `service.json` filename. Saved-file
completion is **unverified**: this isolated Chrome harness closed on product
downloads and on an independent standalone synthetic Blob download. No native
zoom control, physical network disconnection, screen-reader audit or additional
browser engine was available; offline media/network behavior used Playwright
emulation. These limits are distinct from green owner/package checks. No native
client, target apply, hosting, signer or remote publication claim is made.

Implementation evidence from the authorized `browser-complete-configuration`
workstream on 2026-10-09 currently establishes these separate owner checks:

| Gate | Observed result | Local result artifact |
| --- | --- | --- |
| Core final Release suite | 97 passed, zero failures/skips; 1.219 s | `.omo/evidence/20261009-browser-complete-configuration/core-workstream-final.txt` |
| Presentation final Release suite | 36 passed, zero failures/skips; 0.973 s | `.omo/evidence/20261009-browser-complete-configuration/presentation-event-final-tests.txt` |
| Browser final owner Release suite | 81 passed, zero failures/skips; 4.390 s | `.omo/evidence/20261009-browser-complete-configuration/browser-owner-final-tests.txt` |
| Final solution Release build after locked restore | Zero warnings/errors; 3.226 s wall time | `.omo/evidence/20261009-browser-complete-configuration/solution-build-final.txt` |
| Solution format verification | Exit 0; 125.975 s wall time | `.omo/evidence/20261009-browser-complete-configuration/solution-format-green.txt` |
| Last changed Browser slice format verification | Exit 0; 11.437 s wall time | `.omo/evidence/20261009-browser-complete-configuration/format-last-slice.txt` |
| Final static Browser publication | Exit 0; 17.233 s wall time; 33 dependency notices, ratchets current 3/3 | `.omo/evidence/20261009-browser-complete-configuration/browser-publish-final.txt` |
| Actual final published Browser package | 1 passed, zero failures/skips; 0.746 s | `.omo/evidence/20261009-browser-complete-configuration/browser-package-final.txt` |
| Compiled product dependency closure | 7 owner rows passed, zero failures/skips; 0.741 s | `.omo/evidence/20261009-browser-complete-configuration/browser-architecture-final.txt` |
| Event wire/host checks against eight actual Core-produced files | 15 tests passed; 8.129 s; independent exporter verification 8/8 valid, exit 0 | `.omo/evidence/20261009-browser-complete-configuration/event-actual-tests.txt`, `event-actual-verify.txt` |
| Definition schema freshness | Current | `.omo/evidence/20261009-browser-complete-configuration/core-schema-check.txt` |
| Native capability/schema ratchets | Generated/current 3/3, exit 0 | `.omo/evidence/20261009-browser-complete-configuration/governance-ratchet-write.txt`, `governance-ratchet-check.txt` |
| Intent YAML, changed rule twins and whitespace | Valid/identical/clean, exit 0 | `.omo/evidence/20261009-browser-complete-configuration/governance-intents-final.txt`, `governance-docs-checks.txt` |

The ratchet invocations were `dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --write`
and the matching `--check`, with the documented TMPDIR. The intent syntax check
used `dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release --no-build --no-restore -- validate-intents .agents/contract/intents.yaml`.
These local paths are session evidence, not distributed package inputs or
release certification. Event acceptance is wire/semantic validation of synthetic
files, not deployment or authenticated apply.

The package and compiled-closure checks used the actual final published assets
and compiled assemblies, respectively:

```bash
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --treenode-filter "/*/*/*StandalonePackageTests/BrowserPackage*" --minimum-expected-tests 1 --no-progress
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --treenode-filter "/*/*/*StandaloneArchitectureTests/CompiledProductClosureHasNoEventOrTestDependency" --minimum-expected-tests 7 --no-progress
```

An initial Browser-name wildcard selected zero parameterized closure rows and
correctly failed the minimum-test guard; the method filter above selected all
seven owners. This is not a full architecture suite or a native package matrix.

Published Chrome checks reported by the parent execution track have exercised
worker boot, all three independent neutral complete-output vectors, a
secret-deferred draft, offline reload and a fresh tab, uncached-source local
rejection, explicit clipboard export and consent revocation. Inspection found
65 allowlisted public app cache entries and no synthetic answer canary in
application cache/store/network sinks. The browser downloaded a declared `.env`
as `env.txt` despite the correct link hint; operators must check/rename downloaded
filenames before target use. Application-state clearing does not clear those
exported files or clipboard content.

The independent published track records its observations in
`.omo/evidence/20261009-browser-complete-configuration/platform-published-qa.md`
and `platform-final-narrow.png`. Published Chromium ran against the native static
host `http://127.0.0.1:5078/`. The broad published retest used cache
`configurator-app-V6l73mQ7`; minimal revision 2 was admitted. Offline neutral
inputs covered all seven admitted field types and nested object-list
uniqueness/bounds/removal, with preserved survivor identity. Complete/draft
actual downloads passed while offline; reload and fresh-tab boot started with
empty answers and consent. All 65 app-cache responses in that retest and captured request
URLs/bodies had no exercised answer canaries; local/session storage, cookies
and IndexedDB were empty. Controlled answer-bearing fetch/XHR/WebSocket/
EventSource/beacon attempts were blocked in both offline and online active
workspaces, with zero observed destination requests. This verifies exercised
page wrappers, not every browser primitive or extension.

Final-package retests linked required Count's `aria-invalid` to its field error,
and keyboard Add at the collection maximum focused the new row input. Event
typed controls prepared independent zero-, one-, two- and three-tenant manifest
vectors, including explicit empty objects. Unsupported conditional/repeat
inverse import reported `inverse-unsupported` and retained answers. Supported
neutral JSON import respected existing-answer conflicts, succeeded with cleared
conflicts and explicit consent, and rejected secret-bearing input after revocation
without partial answers. The subsequent published duplicate-row refinement used
cache `configurator-app-8oPgRM97`: `alpha/beta/alpha` marked the collection and
third row/input invalid, linked `value-duplicate`, left other rows valid and
published no export. `platform-final-duplicate-aria.png` records the 375 CSS px
retest without horizontal overflow. Preceding unchanged passes remain separate
evidence.

Two old-package active tabs retained distinct ordinary answers and consent while
an update installed a waiting worker; old/new public app caches coexisted without
automatic workspace replacement. After clearing, navigation away/reopening did
not independently prove activation in the original context; a fresh context
verified the new package. Navigation away/back cleared answers/exports, but
persisted BFCache restoration was not forced. Actual 375×812 and 640×450 CSS
viewports showed no horizontal overflow; text enlargement passed as a proxy,
while real browser toolbar zoom was ignored. No screen-reader pass is claimed.
Injected clipboard denial retained Download. Controlled source admission,
redirect/credential/referrer/stream/timeout checks executed the published JS,
but successful live CORS/worker transport remains unverified: the grounded
public endpoint returned 404. No live target deployment, OS permission or
native protected-save certification follows from these browser observations.

A final idle-host check opened three concurrent cached workspaces on
`configurator-app-8oPgRM97` in 3.769 s. All were ready and editable, preserved
distinct values and prepared/revealed an exact draft; no crash, page-error or
request-failure events occurred. Earlier spare-third-tab failures under
concurrent load did not reproduce in this idle check. Their cause remains
unclassified; three passing tabs are not scale certification.

## Maintainer authoring verification

The authorized maintainer-authoring implementation was executed directly in the
Configurator checkout on 2026-10-10 with SDK10.0.302 and
`TMPDIR="$HOME/.cache/agent-tmp"`. Existing browser/store work and user strategy
documents were preserved. No branch, worktree, staging, commit or remote
publication was created. The [maintainer contract](MAINTAINER_AUTHORING.md)
describes the delivered source/compiler/CLI flow; the
[public integration guide](../public/integrate-your-tool.md) describes adoption.

| Executed gate | Result | Evidence |
| --- | --- | --- |
| Locked solution restore | Exit0 | `.tmp/maintainer-restore.log` |
| Post-format normal solution Release build | Exit0, zero warnings/errors,111.08s | `.tmp/maintainer-solution-build.log` |
| Full solution format verification | Exit0 after formatting only five attributable source/test files and ordering their imports | `.tmp/maintainer-format-final.log` |
| Core owner |122 passed,0 failed,0 skipped | `.tmp/maintainer-core-final-tests.log` |
| CLI owner |58 passed,0 failed,0 skipped | `.tmp/maintainer-cli-final-tests.log` |
| Terminal owner |38 passed,0 failed,0 skipped | `.tmp/maintainer-terminal-final-tests.log` |
| Desktop headless owner |67 passed,0 failed,0 skipped | `.tmp/maintainer-desktop-final-tests.log` |
| Browser owner |135 passed,0 failed,0 skipped, including regenerated Event6 resources | `.tmp/maintainer-browser-final-tests.log` |
| Presentation owner |36 passed,0 failed,0 skipped | `.tmp/maintainer-presentation-final-tests.log` |
| Linux X11 native host |13 checks passed; no broader host/accessibility claim | `.tmp/maintainer-native-host.log` |
| Generated runtime/authoring and command schemas | Native `--check` exits0 for both schema owners | Task-owned execution context |
| Native store integrity |46 self-tests passed; baseline-required write/check passed for four revisions and generated curation schema | Task-owned execution context |
| Actual store-free and store-enabled browser publication | Both exit0, each retaining original notices for36 dependency packages | `.tmp/maintainer-browser-no-store-publish.log`, `.tmp/maintainer-browser-with-store-publish.log` |
| Candidate standalone skill adoption | Actual tool-managed identity, paired YAML/JSON bytes, nested literal outputs, protected mode600, strict/acknowledged/refused imports, comment/no-op/revision, retained history and source-only contribution | `.tmp/maintainer-adoption/REVIEW.md` |
| Final standalone CLI publication | Exit0; packaged schema and portable skill; executable SHA-256 `5031706878d98386f78f69f07845b680d107c183993ee8aab07587c827e59f97` | `.tmp/maintainer-cli-final-publish.log` |
| Final compiler adoption confirmation | Reserved `$defs` entry named `$id`: init/check/build all exit0; existing nested revision2 check exit0 with unchanged canonical digest | `.tmp/maintainer-adoption/evidence/final-reserved-*.json`, `final-nested-check.json` |
| Event ordinary source/host owner |16 passed,0 failed,0 skipped with compiler environment variables absent; exporter build zero warnings/errors | Event technical export anchor |
| Event explicit pinned compiler owner |11 passed,0 failed,0 skipped; final CLI write/check and source-only check exit0;361 environment definitions current | Event technical export anchor |
| Final Terminal Linux-x64 publication | Exit0; retained notices for26 dependency packages | `.tmp/maintainer-terminal-final-publish.log` |
| Final Desktop Linux-x64 publication | Exit0; retained notices for29 dependency packages | `.tmp/maintainer-desktop-final-publish.log` |
| Published Browser custody and updates | Actual Chromium153 Linux scenarios passed with the bounded export recovery described below | `.tmp/maintainer-platform/qa-evidence.md` |
| Clean Browser republications | Both exit0; retired stylesheet/sidecars removed; four retained store revisions and36 dependency notices | `.tmp/maintainer-browser-clean-publish.log`, `.tmp/maintainer-browser-store-clean-publish.log` |
| Final architecture/package owner |41 passed,0 failed,0 skipped,6.689s; actual CLI/Terminal/Desktop Linux packages and both Browser variants | `.tmp/maintainer-architecture-package-closure.log` |
| Package regression owner build and format | Normal Release build zero warnings/errors; exact-path whitespace verification exit0 | `.tmp/maintainer-package-fix-build.log`, `.tmp/maintainer-package-fix-format.log` |

The accepted owner commands were physically run against the normal solution
build, without analyzer suppression:

```bash
dotnet restore Configurator.slnx --locked-mode --verbosity quiet
dotnet build Configurator.slnx --configuration Release --no-restore --verbosity quiet -p:UseSharedCompilation=false
dotnet format Configurator.slnx --verify-no-changes --no-restore --verbosity quiet
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 1 --no-progress
```

The same test command was executed for CLI, Terminal, Desktop, Browser and
Presentation, with the exact counts above. The native host command was:

```bash
xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 13
```

Independent review exposed inherited-private-default, expansion-budget,
schema-resource and recorded-anchor failures before their fixes. Final review
found no remaining issue in those bounded changes. Early zero-test diagnostic
runs and builds with `RunAnalyzers=false` are excluded from acceptance evidence.
The final Core suite also covers reserved `$defs` names without confusing them
with schema resource keywords. Native store history retains neutral2 and Event5
bytes; current sources compile into neutral3 and Event6 instead of reusing their
identities.

Source and package owners total497 passing Configurator cases, with no failed or
skipped cases in their accepted runs. Unit tests and publication alone do not
prove browser platform scenarios.
Tavily and Context7 were unavailable; official-source
Firecrawl research was the disclosed fallback. No remote CI, other-host,
stakeholder, future-agent, legal or scholarly certification is implied.

Event commands were executed in the sibling Event checkout with the same SDK and
temporary-directory configuration. The two actual test selections were:

```bash
env -u CONFIGURATOR_COMPILER -u CONFIGURATOR_COMPILER_SHA256 dotnet test --project tests/Event.Wire.Contracts.UnitTests/Event.Wire.Contracts.UnitTests.csproj --no-restore --configuration Release --verbosity quiet -p:BuildProjectReferences=false -p:UseSharedCompilation=false -- --treenode-filter "/*/*/*ConfiguratorDefinitionArtifactTests/*" --minimum-expected-tests 16 --no-progress --maximum-parallel-tests 1
CONFIGURATOR_COMPILER=/home/amir/ISLAMU/Github/Configurator/artifacts/configurator/cli/linux-x64/islamu-configurator CONFIGURATOR_COMPILER_SHA256=5031706878d98386f78f69f07845b680d107c183993ee8aab07587c827e59f97 dotnet test --project tests/Event.Wire.Contracts.UnitTests/Event.Wire.Contracts.UnitTests.csproj --no-restore --configuration Release --verbosity quiet -p:BuildProjectReferences=false -p:UseSharedCompilation=false -- --treenode-filter "/*/*/*ConfiguratorCompilerHandoffTests/*" --minimum-expected-tests 11 --no-progress --maximum-parallel-tests 1
```

The final Event release receipt has SHA-256
`8a5e5e57c2501903efb20d49c3d5b4e1d79fb39228353f282f2f0cb42ba5867b`.
The source copies have digest
`106504676e3087b35eef0761702dc88714d8f2ab9a4da823d7356f348ee39304`;
canonical Event/example/store revision6 copies have digest
`b50171ca0a52aa48c3099088db925e440791aff3a450e03e1caca97cf3e4066e`.
All copies compare byte-identical. Generated provenance describes the compiler
used; it grants no publication, trust promotion or target apply authority.

Published Browser inspection used Chromium153.0.8010.52 on Linux with an isolated
Apache host and synthetic fixtures. Local compact YAML was admitted before
answers; explicit public retention stored canonical runtime JSON, and simulated
offline reload/reopening started a fresh workspace. Independent draft145-byte
and complete204-byte vectors matched actual clipboard and download output.
Revoking consent cleared three top-level/nested/list secret inputs and removed
prepared output while preserving public values. Clear and navigation reset the
workspace. Scans of82 active and83 final cached responses found no answer
sentinels; local/session storage and IndexedDB were empty. Deliberate application
connection attempts were blocked; observed answer-time traffic carried only a
public asset GET with no body.

Two independent active tabs retained their original workspace and cached Core
asset when the actual store-enabled package installed a waiting worker. After
closing both tabs, the new package activated and its Store booted offline.
Invalid authoring rejected without displaying the invalid scalar and restored
file-picker focus. Keyboard focus and375px reflow were inspected; CSS200% zoom
kept content within the viewport. The exact observations, package manifest
hashes, synthetic vectors and native host commands are in the task evidence.
Only owned tabs/host were closed and the synthetic clipboard was cleared.

Chromium's simulated offline mode crashed a generic Blob download control as
well as actual download clicks. Recovery verified both exports with simulated
offline disabled and Apache physically stopped, confirmed by failed curl. This
proves export without reaching its host; it does not prove physical disconnection
of every network. Offline boot/edit/draft/copy were separately observed under
simulated disconnection. Other engines, assistive technology and native browser
zoom remain unqualified; browser exports carry no native protected-save promise.

The first full architecture/package run executed41 cases:39 passed,2 failed,
none skipped. A removed stylesheet remained in a reused Browser publish directory;
`PublishConfiguratorBrowser` now replaces its generated destination before
publication. The store inventory test also rejected standard SDK Brotli/gzip
sidecars; its closed-file assertion now verifies that each transport encoding
decodes to exactly its admitted canonical bytes, including end-of-stream.
The normal Architecture owner rebuild passed with zero warnings/errors (2.67s),
and the exact store transport case passed1/1 before republishing the Browser
variants. The final full package run passed41/41, zero failures/skips; the initial
failure is retained in `.tmp/maintainer-architecture-final-tests.log`.

The final package command was:

```bash
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 41 --no-progress
```

All three native Linux payloads were physically published with
`dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator
-p:ConfiguratorSurface=<cli|terminal|desktop> -p:ConfiguratorRid=linux-x64
-p:UseSharedCompilation=false`, one surface per command. Both Browser variants
were republished through `PublishConfiguratorBrowser`, passing an empty
`DefinitionStoreSourceRoot` for the store-free package and the explicit local
`store/` root plus `ConfiguratorOutputRoot=artifacts/configurator-with-store` for
the store-enabled package. Final worker-manifest SHA-256 values are
`63dfce4aa1a2e7bd18eb545c7594a6b11bc6443a8d4529fca7ca38388f4daf51`
and `3896d753e850330f44495cf4f331c12a078e7ba64074f6f0acb49bcfb3bfb3dd`,
respectively. Earlier platform evidence retains its original package identities;
fresh package confirmation is appended to that evidence rather than relabeling it.

The final clean-package confirmation passed against both manifest identities:
fresh local YAML admission and exact204-byte complete clipboard output, default
consent off, three masked secret inputs, and complete revocation clearing.
Active/final scans of82/83 cache responses found no answer sentinels; storage and
IndexedDB remained empty. Two active old tabs retained separate values and Core
assets while the new worker waited. A cleared tab booted/reopened offline with
empty answers while the other tab kept the update waiting; after both closed,
the new Store activated, discarded the old app cache and booted offline.
Earlier download, keyboard, invalid-source, navigation and connection-guard
observations retain their original package hashes and were not redundantly rerun.
Owned host/tabs were closed, online mode restored and synthetic clipboard cleared.

## Native ownership qualification (2026-10-27)

The `cross-platform-quality` native slice separates cancelled eligibility from
physical settlement, retains bounded publication facts and adds deliberate
Desktop close choices. This is the three-increment native slice, not completion
of the required Browser/CLI/editing/preparation/CI follow-up program.

Activation was `main` at `d6c23eef32766f3743088c36858589afa8ed2af8`.
Receipt extraction is `9d96269`; physical settlement is `f1eceb0`. The final
close candidate was qualified on that committed prefix plus the owned close
changes, before its phase-close commit. No worktree or remote publication was
used. SDK 10.0.302, .NET runtime 10.0.10, Linux x64 and actual X11 under
`xvfb-run -a` were used with `TMPDIR="$HOME/.cache/agent-tmp"`.

### Executed commands and results

All commands below exited 0 after attributable corrections. Native result and
capture paths are local ignored evidence, not shipped answer storage.

| Check | Exact command or selection | Result |
| --- | --- | --- |
| Untouched receipt baseline | `dotnet test --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --configuration Release --verbosity quiet -- --treenode-filter "/*/*/*SetupTerminalPublicationReceiptTests/*" --minimum-expected-tests 1` | 9 passed, 0 failed/skipped; 768 ms |
| Untouched Desktop baseline | `dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1` | 185 passed, 0 failed/skipped; executable 2.897 s |
| Shared fact owner build | `dotnet build src/Configurator.Terminal/Configurator.Terminal.csproj --configuration Release --verbosity quiet` | Exit 0; 2 s wall |
| Terminal preservation | `dotnet test --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1` | 83 passed, 0 failed/skipped; 2.933 s |
| Shared receipt bounds/copies | `dotnet test --project tests/Configurator.Artifacts.Tests/Configurator.Artifacts.Tests.csproj --configuration Release --verbosity quiet -- --treenode-filter "/*/*/*ProtectedArtifactPublicationReceiptTests/*" --minimum-expected-tests 1` | 8 passed, 0 failed/skipped; 657 ms |
| Desktop build, each changed increment | `dotnet build src/Configurator.Desktop/Configurator.Desktop.csproj --configuration Release --verbosity quiet` | Exit 0; clean compiler/analyzer build |
| Physical settlement owner | `dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress` | 193 passed, 0 failed/skipped; 3.185 s |
| Deterministic close class | `dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --verbosity quiet -- --treenode-filter "/*/*/*DesktopCloseLifecycleTests/*" --minimum-expected-tests 1 --no-progress` | 7 passed, 0 failed/skipped; 1.613 s |
| Final Desktop owner | `dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress` | 201 passed, 0 failed/skipped; 3.597 s |
| Native executable build | `dotnet build tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --verbosity quiet` | Exit 0; no suppressed diagnostics |
| Physical settlement native lane | `xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 14 --capture-directory "$PWD/.tmp/cross-platform-quality-settlement"` | All fourteen required names passed; normal/minimum captures inspected |
| Final close native lane | `xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 17 --capture-directory "$PWD/.tmp/cross-platform-quality-close"` | All seventeen required names passed; 9471.2676 ms native elapsed |
| Terminal local package | `dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=terminal -p:ConfiguratorRid=linux-x64 -verbosity:minimal` | Exit 0; current ratchets 3/3; notices retained for 29 dependencies |
| Desktop local package | `dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=desktop -p:ConfiguratorRid=linux-x64 -verbosity:minimal` | Exit 0; current ratchets 3/3; notices retained for 31 dependencies |

The pre-consumption Red ran before the physical fix: dispose failed, cancel/edit
passed, exit 2, 1.108 s. The ordinary-close Red ran before the close handler:
one failed executed test, exit 2, 1.366 s. Compiler-only failures and the initial
incorrectly awaited headless callback were not counted as runtime Red.
The first full settlement run exposed stale disposed-child busy flags; the
completion owner now updates them only after settlement. The close picker
regression now awaits the actual Closed event and asserts detached state rather
than dereferencing cleared status bindings. Its late-selection and value-safety
assertions remain intact.

Native Event setup also now selects the definition's required
`Artifact to prepare` answer independently of the output picker. The previous
setup left settings inactive. All original thirteen named guards and their
expected manifest assertions are preserved. There are no remaining quarantined
failures in these executed lanes.

### QA matrix and retained identity

| Scenario | Observable evidence | Verdict / artifact |
| --- | --- | --- |
| Save/bundle/read invalidation before consumption, stale generations and eventual zeroing | `DesktopPublicationLifetimeTests`, preserved shell/import/writer tests | PASS; `TestResults/Configurator.Desktop.Tests-linux-net10.0.tunit-report.json` |
| Installed prefix, cleanup/verification facts, copied 16-settled-plus-active history and 64-outcome bound | Desktop lifetime tests, Terminal preservation and shared Artifacts receipt cases | PASS; Desktop, Terminal and Artifacts MTP reports under `TestResults/` |
| Exact two-second deadline, repeated close, both settlement orderings, withdrawn intent and deliberate force | Seven deterministic close cases and actual-window control test | PASS; final Desktop MTP report |
| Real native lifetime and preserved adjacent output/import/Event behavior | Required set equals all seventeen completed names | PASS; `.tmp/cross-platform-quality-close/qualification.json` |
| Visible unknown/receipt controls, names/help and focus at 960x800 and 600x540 | D14/D16/D17 real X11 controls and light/dark footer captures | PASS for exercised controls; `.tmp/cross-platform-quality-close/D16-close-deadline-unknown-unknown-minimum-Light-end.png` and `D17-close-stay-force-unknown-desktop-Dark-end.png` |
| Changed native package assembly | Native release owner publication and retained dependency notices | PASS for build only; `artifacts/configurator/{terminal,desktop}/linux-x64/` |

SHA-256 identities of the qualified executables/assemblies:

- NativeHost DLL: `c17dd685a3626b7885ae0ae4ca21ee5b3c096bbde09a30215f03602983e67b95`.
- Its Desktop DLL: `d805c457420975dcd7f29e2a9577ccb8fb100b91f4db9221c2865c1027b5b4cf`.
- Desktop test DLL: `16e10f903e3fd196909e685300a3e6a39799cfe8583b36d1be4105decdf1493e`.
- Local `islamu-configurator-terminal`: `2194bde911ca8fc785f2310b6b7fef6b0ccfbadcd2833c623bbac5890118f116`.
- Local `islamu-configurator-desktop`: `09ac3b63554ca2c95f668138adf9c319c2125c180c4724eb8f01289a15a5becd`.

The final fresh LSP diagnostics requests timed out after publication changed
generated intermediates; prior changed-symbol diagnostics were clean and the
actual final compiler/analyzer builds and owner suites passed. No warning was
suppressed. Build/native command wall durations not printed by their tools were
not reconstructed or claimed as latency evidence.

Not executed: installed/published-payload interaction, human assistive technology,
complete keyboard/RTL/scaling cohorts, other hosts, Browser/CLI suites, global
coverage or the complete Architecture/package matrix. Dependency direction and
those product owners did not change. Local publication is package build evidence,
not installed runtime qualification, signing, deployment or target acceptance.
See the [ownership ADR](adr/ADR-cross-platform-quality-ownership.md).

## Cross-platform quality program qualification (2026-10-27)

The promoted program delivered its Browser, native editing, shared preparation,
form, acquisition and CLI corrections on top of the native ownership checkpoint
above. Product source settled at `dcc7617`; subsequent assurance commits do not
change runtime behavior. The [ownership ADR](adr/ADR-cross-platform-quality-ownership.md)
explains control/data flow; the [required-duty ledger](../../dev/backlog/cross-platform-quality-required-followups.md)
preserves obligations outside this delivery. No remote CI, deployment, signing
or new protection host was activated.

### Final owner and wiring evidence

All commands used SDK 10.0.302, Linux x64 and
`TMPDIR="$HOME/.cache/agent-tmp"`. Final locked solution restore and
`dotnet build Configurator.slnx --configuration Release --verbosity quiet`
exited 0; the build reported 2.2 seconds. Current source-owner passes were
retained rather than repeated after documentation/assurance-only edits:

| Owner | Passed | Failed / skipped | Evidence scope |
| --- | ---: | --- | --- |
| Core | 150 | 0 / 0 | Canonical composition, aggregate admission, cancellation and independent output vectors |
| Presentation | 167 | 0 / 0 | One form gate, typed controlled fixtures, ownership transfer and never-started disposal |
| Artifacts | 65 | 0 / 0 | Physical storage, immutable receipts and protected create-only behavior |
| CLI | 171 | 0 / 0 | Explicit authoring capability, candidates, concurrent editor preservation and command v2 |
| Terminal | 101 | 0 / 0 | Correction drafts, bounded acquisition and retained native facts |
| Desktop | 222 | 0 / 0 | Shared leaf/presence/focus behavior, acquisition, settlement and close |
| Browser | 233 | 0 / 0 | Exact leases, mandatory policy, reconnect, export retirement and single-pass adoption |
| Architecture | 69 | 0 / 0 | Final actual publications, installed CLI fixture and eight compiled owner boundaries; 17.591 seconds |

The final gate reviewer approved with no blockers and moderate overall
confidence, while noting that a later focused six-case run had overwritten
Desktop's default report. The parent then executed only the full Desktop lane
again with `--no-build --maximum-parallel-tests 1 --report-trx`,
unique native results and the safe evidence tool. All 222 passed, exit 0;
runner duration 4.331 seconds and TRX duration 4,016.5619 milliseconds are different
clocks. The validated unique receipt is
`.tmp/cross-platform-quality-final-owner-evidence/Configurator.Desktop-linux-x64-final-full-desktop-20261027.json`;
it records 222 passed, zero failed/skipped, empty diagnostics and unmeasured
coverage. The six-case file is not cited as a full-suite report. The reviewer
did not rerun checks; its original evidence limit remains historical in
`.omo/evidence/cross-platform-quality-promoted-gate-review.md`.

Owner commands use `dotnet test --project tests/Configurator.<Owner>.Tests/Configurator.<Owner>.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1`.
Desktop concurrency was restricted to one test. The final Architecture command ran
after all publications, not against a historical package. Focused compiled
boundary controls passed 26/26; forbidden-edge mutation controls demonstrate
rejection, including the existing static host's empty first-party set.
The native acquisition post-correction mutation removed worker dispatch and
caused four open/stat failures with two read cases passing; exact restoration
passed 6/6 in 790 ms. This is not historical pre-fix Red.

Actual dynamic coverage retained the complete current Core 150 and Presentation
167 suites: Core 3,223/3,815 class-line entries and 2,745/3,687 branches;
Presentation 1,699/1,771 and 1,192/1,346. Safe owner/run JSON resides in the
local `phase12-safe` temporary evidence directory. The [Testing anchor](TESTING.md#initial-collection-and-resolved-core-instrumentation-prerequisite)
records the installed legacy `libxml2.so.2` collector prerequisite and exact
dynamic settings. No test, compiled guard or warning was excluded. Ordinary
owner passes are not coverage evidence; these measured runs are. Later CLI
work changed no Core/Presentation product source. No global threshold or
mutation score is claimed.

### Final packages and actual surfaces

Serial `PublishConfigurator` runs for `cli`, `terminal` and `desktop` with
`ConfiguratorRid=linux-x64` exited 0. Serial `PublishConfiguratorBrowser`
published the separate Store root, default Store root, then default store-free
root; the final package suite checked stale-asset removal. Source-owned command
schema and all three ratchets passed their native freshness checks.

The current CLI validator executed all five cohorts successfully: schema
freshness, doctor, help, read-only validation and protected-create/conflict.
Its self-test accepted two and rejected 42 hostile envelopes. CI uses this same
tool for every configured native CLI lane and the Linux package gate; remote
matrix results remain unexecuted here. Package identities are SHA-256:

| Final payload | Identity |
| --- | --- |
| CLI executable | `9d41dd6e5f214f82ec87043eb9c9e41fe8c640c6f7a90619d865f3917b2d72c8` |
| CLI package contract | `cdedc5af534fdf0789cd03ebc2d68ad96c30c4dc14a178621c4335f92ff90259` |
| Packaged/current command schema | `89c955bce60411c1ce30396a5070e141cc44fbe585cf8d4003780d7099db7e41` |
| Terminal executable | `530b3b427fd40aed155562ea53292abaeffa2c30f34cc40886279a65532a90f5` |
| Desktop executable | `86826ae305c2f9257b9fa3bc31e20bfa005a0959b208d30873340bf11332d01f` |
| Final Browser asset manifest | `15797408fb996080919609817563dedaea58407a3021b85e903b9793a899d116` |

| Scenario | Observed result and local artifact |
| --- | --- |
| Browser exact registration and mandatory denied/rejected/retry initialization | Passed actual published cohorts; `.tmp/cross-platform-quality-browser-registration/qualification.json` and `cross-platform-quality-browser-policy/qualification.json` |
| Actual native reconnect, public admission, active-custody denial and listener release | Passed without internal reload; `.tmp/cross-platform-quality-browser-connectivity/qualification.json` |
| Shipped export module late-open denial, held-copy revocation and metadata reclamation | 200 cycles, peak 1/final 0 entries; `.tmp/cross-platform-quality-browser-export/qualification.json` |
| Single-pass drafts, complete rejection, retry/reveal/clear and deliberate download | Three redacted incomplete drafts; complete rejection yields zero outputs; independently validated JSON download 82 bytes, digest `87bb6f312597ca2d42c011e8fada46266e5df7572e30c243048364064a563595`; `.tmp/cross-platform-quality-browser-adoption/qualification.json` |
| Final Browser package at fresh loopback origin 5199 | Three incomplete/redacted drafts and immediate clear; disabled answer fieldset; no horizontal overflow at 1440x900/390x844; directly inspected session images and `.tmp/cross-platform-quality-final-surfaces/qualification.json` |
| Published Terminal correction/clear/keyboard close | Final 110x36 PTY retained rejected public correction across experience change; clear status observed; Tab/Enter close exits 4 because no save occurred. Earlier protected draft/disclosure/resize cohort retains its own identity in `.tmp/cross-platform-quality-terminal-drafts/qualification.json` |
| Final real X11 lifetime/editing/close guards | All 18 required names completed; 7,669.0392 ms; `.tmp/cross-platform-quality-final-native/en/qualification.json`; normal/minimum light/dark captures inspected. Earlier separate en/ar runs establish exact touched resources, not whole translation |
| Published Desktop keyboard intake/edit/close | Local synthetic definition loaded via Tab/Enter, rejected leaf retained with its diagnostic and untouched sibling; ordinary close exits 0. `.tmp/cross-platform-quality-final-desktop/{start,loaded,invalid-leaf}.png` |

The final X11 command built `tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj`
in Release, then ran it with `CONFIGURATOR_NATIVE_UI_CULTURE=en`,
`--expected-rid linux-x64 --minimum-expected-checks 18 --capture-directory "$PWD/.tmp/cross-platform-quality-final-native/en"`.
The required-name set, not the minimum alone, determines success.

Earlier Browser semantic cohorts bind asset manifest
`14ee82b9ea6b6c09c7c4da91bd83a0c62ef737b0fd368d6df034d1978b0ec314`;
the final smoke supplements those unchanged-source passes without relabelling
their identity. A stale origin's pinned worker and an initial download oracle
failure caused by appending to a default remain recorded. Correcting the origin
and selecting/replacing the synthetic input fixed the qualification setup, not
the product. Early DOM clear proves immediate denial, not settled worker custody.
All task-owned views, static host and native apps were closed afterward.

Safe evidence self-tests, action pins and dependency closure remain separate
engineering checks. Final AgentWorkflow2/2 and ReleaseEngineering468/468 passed,
and the evidence sanitizer passed all18 synthetic vectors. Action pins11 and
dependency policy95 closure pairs passed without changing action/dependency
versions. No raw answer/download content was copied into reports.
Unqualified duties remain explicit: full responsive preparation and compound
roots, aggregate registry budgets, captured CLI handles/root exclusion, the Core
directory-I/O exception, complete Terminal viewport/locale/AT, wider Desktop
accessibility, other native hosts/installers, remote CI and first-party
publication authority. No broad performance, accessibility, erasure or
whole-product certification follows from these scoped passes.

## Full Project Test List

### Desktop readiness evidence

The 2026-10-09 readiness work recorded 64 Desktop MTP passes, 26 real native
protected-artifact passes and thirteen named Linux x64/X11 NativeHost passes.
The native executable has strict RID/backend/count/timeout guards; missing
display or incomplete results reject qualification. Headless and native-host
checks are distinct from actual published application proof.

The 2026-10-11 MVVM refactor retained all original 67 desktop cases and expanded
the owner suite to 173 passes. Native qualification again passed all thirteen
checks, including Event one/two/three-tenant projections, real create-only writes,
conflicts, recovery and close-time disposal. Forty-one architecture cases,
solution Release compilation, locked restore and non-mutating solution
formatting also passed. Shared-output Avalonia builds ran serially after a
parallel build exposed PDB file contention.

Synthetic visual inspection covered sixteen loaded/edited top/footer captures:
960x800 and 600x540, light/dark themes. The native harness subscribes to resize
and animation-frame signals rather than sleeps. Inspection fixed collapsed
minimum-size form space and default artifact selection lost during two-way
choice replacement. These source-harness captures do not establish manual
dialog, assistive-technology, additional-platform or newly published-payload
qualification. See [desktop verification](DESKTOP_CLIENT.md#native-qualification).

Published-app isolation uses Bubblewrap with a new network namespace, a clean
operator home, no mounted source/NuGet tree and a masked `/usr/share/dotnet`.
The single-file executable uses its embedded runtime, system native libraries,
Xvfb's local X11 socket, distro font cache and software rendering. A first
over-minimal graphics environment aborted; supplying its native font/graphics
prerequisites allowed startup. This qualifies only that observed environment,
not all Linux desktop backends. Synthetic operator output remains outside shared
evidence; capture only value-free outcomes and public source/payload identities.

The task evidence under `.omo/evidence/20261009-desktop-cross-platform-packaging/`
contains exact commands/counts, inventories, reviews and unrun limits. Partial
isolated-app checks and automated GTK interaction do not replace the complete
published workflow, physical keyboard/scaling or assistive-technology proof.
Readiness stays unqualified until all required evidence is present. No installer,
Windows/macOS qualification, signing, promotion or publication was performed.

The [project map](../../PROJECTS.md) owns project commands and package preconditions.
[TESTING.md](TESTING.md#test-projects) maps all product and engineering suites.
Use `dotnet test --project <affected-test-project.csproj> --configuration Release`
with actual paths, not a solution-level run or an absent source-system owner.

## Browser UX revision-5 verification

The November 2 integrated source passes Core 362/362, Presentation 277/277,
Browser 370/370, CLI provider integration 1/1 and actual-package Architecture
77/77. Release build and format checks pass without warning suppression. Native
example/Store/ratchet/editor freshness checks pass. Both Event families are at
revision 6; prior Store snapshots remain immutable.

Local AOT packages with and without Store are verified against their actual assets.
The [execution report](reports/BROWSER_UX_SIMPLIFICATION_EXECUTION.md#focused-november-2-continuation)
records exact counts, package roots, failed attempts and browser observation
limits. Earlier performance passes elsewhere in this document do not qualify
these changed sources. The final partial timing run retained 720 measured actions
and failed its retained limits; the complete 960-action cohort was not completed.
Performance, human comparison, physical-device and AT qualification remain open.
The focused continuation removes redundant metadata/visibility/lifecycle work,
corrects English/Arabic consent directions, and records new bulk-edit recovery,
cache/revocation/offline and waiting-worker activation evidence. Its small timing
diagnostic is not a qualification pass; the earlier scalar timeout is unresolved.

## AI Agent Operational Context

AI-agent workflow rules are not runtime operations. Keep them in
[AGENTS.md](../../AGENTS.md) and
[the context-engineering contract](../../.agents/CONTEXT_ENGINEERING.md) so
adopters do not have to scan agent tooling while using the utility.

The read-only `eng/agent-workflow` guard owns no state and never executes Git.
Use `validate-intents .agents/contract/intents.yaml` for YAML syntax and
`validate-commit -- git commit ... -- <literal-files>` to reject broad commit
pathspecs. Validation is not authorization to commit.

```bash
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- \
  validate-intents .agents/contract/intents.yaml
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- \
  validate-commit -- git commit --only -m "message" -- src/ExactFile.cs docs/ExactFile.md
```

The literal file examples illustrate validation grammar, not actual product
paths to stage. Select only owned paths when committing is explicitly authorized.
Never switch the shared checkout, create a worktree or alter another contributor's
changes contrary to the current task.

ReleaseEngineering separately validates commit syntax, immutable Change-Ids and
local release evidence. See its [operating instructions](../../eng/release/README.md).
No credentials, Event signer lists or machine MCP settings are transfer inputs.

## Schema-driven secrets qualification (2026-11-02)

This candidate implements authoring/runtime v3 (`bounded-v3`), command v5 and
unchanged private answers v1. It was built directly in the development checkout
over `4f130d1`, without a commit or remote publication. Core owns admitted secret
policies, bounded validation and generation; Presentation owns operation revisions,
consent and transient original-password custody. All four clients consume those
owners. Local validity never establishes target acceptance.

Commands used SDK 10.0.302, `TMPDIR="$HOME/.cache/agent-tmp"`,
`DOTNET_PROCESSOR_COUNT=2` and serialized builds with `-m:1 -nr:false
-p:UseSharedCompilation=false`. Final solution Release build completed with zero
warnings/errors; `dotnet format Configurator.slnx --verify-no-changes --no-restore
--verbosity quiet` exited 0. Locked restore, generated schema/example/command
freshness and the three release ratchets passed.

Each owner ran with `dotnet run --project tests/Configurator.<Owner>.Tests/
Configurator.<Owner>.Tests.csproj -c Release --no-build --
--minimum-expected-tests 1`; every listed pass had zero skips:

| Owner | Passed | Duration |
| --- | ---: | ---: |
| Core | 427 | 13.001 s |
| Presentation | 294 | 3.491 s |
| CLI | 258 | 6.403 s |
| Browser | 378 | 9.751 s |
| Desktop | 290 | 5.952 s |
| Terminal | 139 | 7.596 s |

Architecture/package execution initially passed 76/77 in 23.119 s. Its remaining
CLI validator still referenced retired v4/v2 fixtures. Migrating that owner to
v5/v3, retaining old-version rejection and admitting the new `secret` help route
made the targeted remaining case pass in 35.563 s. The validator's native
`--self-test` accepted six valid envelopes and rejected 79 invalid ones. The 76
unaffected passes remain current; this is aggregate evidence, not a second full run.

`dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator
-p:ConfiguratorSurface=<cli|terminal|desktop> -p:ConfiguratorRid=linux-x64`
published all three final packages with dependency notices. Browser publication
used `-t:PublishConfiguratorBrowser`, both with the default Store-free root and
with `-p:ConfiguratorOutputRoot=artifacts/configurator-with-store
-p:DefinitionStoreSourceRoot=<checkout>/store`. Both static AOT variants passed
the actual package/store inventory checks. Per-tool NuGet lock paths isolate
the Store builder, ratchet generator and local preview server from a pre-existing
ignored shared lock; the lock was preserved.

Actual execution separately established:

- The packaged Linux CLI generated password and token answers, validated them,
  and refused destination reuse with exit 4. Both files had mode 0600; receipts
  exposed no values. Earlier generated-original sidecar, derivation and partial
  publication cases are recorded in the workstream context and CLI suite.
- Desktop native X11 guards passed all 21 named scenarios in both `en` and `ar`,
  including generation, correction focus, original-password custody, exact PEM
  clipboard transfer and stale edits. The final packaged executable also loaded
  the public synthetic example and generated a masked password through its actual
  Generate button; the UI reported local generation. Startup was not inferred
  from the managed suite.
- The final Terminal package ran in a real 110×36 PTY, generated a masked password
  and closed without publication (exit 4). Earlier built-binary PTY checks proved
  deliberate reveal/hide, ordinary-copy blocking, headless clipboard refusal
  without answer loss, no OSC52 export and clearing an input modal on SIGTERM.
- The final published Browser AOT payload generated all 11 fixture profile
  families in Chrome. Keyboard reveal focused a multiline PEM control, explicit
  copy succeeded, Hide remasked it, and Arabic RTL keyboard generation worked.
  Generated original passwords started masked, explicit copy succeeded and
  dismissal restored Generate focus. Revocation cleared values; renewed consent
  restored eligibility without restoring old secrets. Local/session storage were
  empty; caches contained only public app assets and an empty public-definition
  cache. Observed requests were local public GETs, with no answer-bearing traffic.
- The independent published crypto probe passed all 37 profiles; measurements
  and limits are in [Secret cryptography](SECRET_CRYPTOGRAPHY.md).

Candidate SHA256 identities:

| Payload | SHA256 |
| --- | --- |
| Linux CLI executable | `b7690fa1590010bd332c7aaa3e6f68421b20197d4002ac590847f9a14246a3bb` |
| Linux Terminal executable | `d769f4667820766284ce45c80cdeca80cc98528dadf4557e42901b95c2550a38` |
| Linux Desktop executable | `94b8b7282fba8503025bd376fe7c1bc55774b3fa6022f7e9ff29eac9633da12b` |
| Browser `dotnet.native.5kv7g0fb56.wasm` | `855b4b5777f9c2003e45bf1ccc8044af373e77656276ec9d472414b22c408bae` |

Evidence: `$TMPDIR/schema-driven-*-final.log`, native/package logs named in the
[execution context](../../dev/active/schema-driven-secrets/schema-driven-secrets-context.md),
and ignored `.playwright-mcp/schema-driven-browser-final-{network.txt,cleared.yml}`.
The network's optional Store lookup returns 404 in the Store-free package.

Known limits: the unchanged Artifacts bundle fixture already failed 27 cases on
obsolete v1 metadata before implementation; the final owner run retained those
same 27 failures (38/65 passed). No unrelated test was weakened. Some initial
pre-fix Red runs were interrupted by workstation memory pressure, so their
historical sequencing remains unproven despite current passing regressions.
Other operating systems, assistive technology, mobile performance and target
application acceptance are not qualified by this workstation evidence.

## Fixed development baseline 0.0.1 (2026-11-02)

The user explicitly fixed Configurator product/public contracts at `0.0.1` for
ongoing development, including breaking changes. `Directory.Build.props` owns
product metadata (`0.0.1`, assembly/file version `0.0.1.0`);
`Configurator.Core.ConfiguratorVersion.Current` supplies the public wire version.
Definition, answer and Store version fields are strings. The CLI envelope is
`islamu-configurator-command/v0.0.1`. All five active schemas have stable filenames
without version suffixes. Generators maintain only current outputs, without
obsolete-version retirement lists or a one-off Store migration command.

Superseded schemas are in ignored `schemas/archive/pre-0.0.1-20261102/`; old
public examples, Store snapshots and the unused v2 qualification fixture are in
ignored `dev/archive/pre-0.0.1-20261102/`. Git history was not rewritten. The
current examples and Store use fresh native-generated family identities;
revision/digest conflict checks remain intact. Third-party dependencies,
cryptographic formats and target application versions did not change.

Verification used the same controlled SDK/build settings as the preceding
record. `dotnet restore Configurator.slnx --force-evaluate --verbosity quiet`
and a subsequent `--locked-mode` restore exited 0. Solution Release build exited 0
with zero warnings/errors in 88.34 s; full format verification and the final
changed-test formatting slice exited 0. Native schema/example/Store/reference
checks passed (four examples, two Store revisions, three generated ratchets).
Store self-tests passed 50/50; command-reference self-tests 113/113; CLI package
validator self-tests accepted 6 valid and rejected 79 malformed envelopes.

| Owner | Final evidence |
| --- | --- |
| Core | Full run 415/427; 12 stale example answer-envelope failures corrected, then all 23 cases in the three affected classes passed. All 427 cases have current passing evidence. |
| Presentation | 294/294, 2.326 s |
| CLI | 258/258, 6.236 s |
| Browser | 378/378, 12.256 s |
| Desktop | 290/290, 7.275 s |
| Terminal | 139/139, 6.439 s |
| Artifacts | 65/65, 1.284 s; migrating the obsolete fixture resolved the earlier 27 failures without weakening protected-write assertions |
| Architecture/packages | 77/77, 58.272 s, including actual CLI execution and both Browser Store variants |

All final passing runs had zero skips. The Core full run took 12.023 s; its
fixture-resource rebuild took 4.65 s and targeted rerun 1.180 s. No product logic
changed to accommodate those fixtures. The final architecture assertion checks
exactly the five current schema paths recursively, so archived or additional
versioned schemas cannot pass package qualification.

The native publication targets and both Browser AOT targets exited 0, retaining
7/30/32 dependency notices for CLI/Terminal/Desktop and 37 for each Browser
variant. Actual Linux package checks generated a masked password through Desktop
and a 110×36 Terminal PTY; the latter closed without publication (exit 4).
The packaged CLI emitted the current envelope and protected answer contract,
generated 32 characters and installed mode 0600; only predicates were printed,
and the synthetic generated file was removed. The English X11 native harness
also passed all 21 required checks.

Actual Chrome execution of the published AOT payload generated all 11 secret
fixture families with masked results, explicitly copied/dismissed an original
password, and verified revocation followed by consent renewal and fresh generation
without restoring cleared values. Network observations contained public local
GETs only. The Store-enabled variant loaded its 0.0.1 catalogue, selected revision 1
of `minimal-self-hosted` and opened the pinned workspace. That route uses the
Store's slug, independently of the target application's `project.id`.

| Final payload | SHA256 |
| --- | --- |
| Linux CLI | `a1bae32c408fb33c487cec89c3a82c8b24cf6090e1fb6363db4d1bbc002a8738` |
| Linux Terminal | `5b2cf5220273ad0a7fee188989a958b908574837affe7a475b220963df4cb70b` |
| Linux Desktop | `7bdbe83ad977128835bfce8f0666ace7aff76467f522aee1cf174cc997f1a1b8` |
| Browser `dotnet.native.to7e26ywiq.wasm` | `6d586f64c5c51130650d64c24e9232ad82ff81ab19af2efb17d78d86fdbf5529` |

Exact commands and logs are in the
[workstream context](../../dev/active/schema-driven-secrets/schema-driven-secrets-context.md).
Local evidence uses `$TMPDIR/development-baseline-*` and ignored
`.playwright-mcp/development-baseline-{browser,store}-*` files. Owned documentation
validation passed 66 files/973 links after this final record; skills passed their
three existing validators. Other OS/device/assistive-technology qualification was
not repeated for this contract reset. No commit or remote publication occurred.

## Import measurement compiler seams

Import comparison candidates now opt into conditional compiler hooks instead of
MSBuild source-text replacement. The [instrumentation procedure](IMPORT_COMPARISON.md#instrumentation-ownership)
describes the switches, synthetic fixtures and actual native entrypoints. Typed
internal callbacks and Terminal partial-window access make renamed observations
compiler-checked. Callback state and friend access are absent from ordinary builds;
the compiled product boundary check verifies that exclusion separately.

The six native memory warmups, twenty observed cycles, timing samples and weak
ownership observations retain their existing definitions. Replacing the hook
mechanism does not renew previous latency, memory, published-WASM or host results.
Current qualification and limits are recorded below.

## Architecture and conventions qualification (2026-11-02)

This implementation was executed directly in the existing checkout, including
its concurrent development changes. It introduces explicit domain wire maps,
structural answer paths, cohesive Core owners and one shared workspace mutation
authority. Client resources use explicit workspace locales; native selection
outcomes are typed; Browser editor handles and callbacks retire with their
workspace. The [ownership decision](adr/ADR-domain-vocabulary-and-workspace-ownership.md)
records the boundaries and retained Monitor synchronization. Internal C# names
and namespaces were migrated directly without compatibility aliases. Public
product and contract versions remain `0.0.1`.

### Source and owner evidence

Verification used SDK 10.0.302 on Linux x64, `TMPDIR="$HOME/.cache/agent-tmp"`,
`DOTNET_PROCESSOR_COUNT=2`, and serialized Release builds with
`-m:1 /p:UseSharedCompilation=false /nodeReuse:false`. The final consolidated
`dotnet format Configurator.slnx --verify-no-changes --no-restore` and
`dotnet build Configurator.slnx --configuration Release --verbosity quiet`
passed with zero warnings/errors. After the Browser help, globalization and
search corrections, a final `dotnet restore Configurator.slnx --locked-mode`,
format verification and solution Release build all passed again; the final
build reported zero warnings/errors in 36.87 seconds. Package qualification is
separate below.

Each owner was physically executed with MTP's `--minimum-expected-tests 1`
guard; all reported zero failures and zero skipped tests:

| Owner | Passed | Covered boundary |
|---|---:|---|
| Core | 469 | Bounded admission, explicit vocabulary, canonical identity, paths and secrets |
| Artifacts | 66 | Restricted create-only output, partial receipts and bounded native input |
| Presentation | 307 | Workspace generations, custody, import retirement and shared map bounds |
| CLI | 270 | Parser-level machine envelopes, cancellation, localization and artifact outcomes |
| Terminal | 150 | Dispatcher completion, locale, keyboard routing and offline editing |
| Desktop | 297 | Typed selection, reactive locale, themes and retired workspace results |
| Browser | 395 | Custody, import/export, resources, module retirement and navigation |
| Architecture/packages | 83 | Compiled dependency boundaries, release guards and actual native/Browser package contents |

The final Architecture run completed in 55.497 seconds after all native and both
normal Browser packages were refreshed. The eight suites total 2,037 passing
cases. Its six new release-guard cases prove diagnostic flags reject before
existing output is changed; they are included in the 83, not additional cases.

The subsequent supported MudBlazor `GetState` search correction passed an
additional 14 Browser shell cases and a zero-warning/error owner build. The
navigation test boundary dispatches location notifications through bUnit's
renderer, matching Blazor's UI dispatch contract; the actual navigation guard
and its cancellation assertions remain exercised.

Native schema and command-schema freshness checks passed, as did all four
maintained definition examples. Command reference generation was current,
three contract ratchets were current, two editor assets validated, the portable
skill validated, and license policy accepted 98 package/version pairs. Generated
outputs were checked through their owning tools; no diagnostic was suppressed.

### Native packages and actual host

The Linux x64 CLI, Terminal and Desktop outputs are self-contained, untrimmed,
compressed single-file applications. This does not claim native AOT support.

| Package | Executable SHA-256 | Actual qualification |
|---|---|---|
| CLI | `d20c255b71df97c448524f90de331aac55b96c0d4de3b2da39eaf79cb2c831a1` | Validator self-test accepted 6/rejected 79 cases; all 6 actual package cohorts passed |
| Terminal | `c8faf924f6f7e1cd363a4bda35f7ea122aac7a6fef528975a9cf8ceb36e9228c` | English/Arabic 80×24 PTYs, resize, masked input, Tab/Shift+Tab, Clear and Close |
| Desktop | `0b0d3c52614bba05e7905a4ff009d5148c74f9a232870d6dd3eb729c10a37f9e` | Actual package retained Custom selection through Arabic switch, displayed LTR native paths and closed with exit 0 |

The CLI package and validator schema hashes both equal
`b3db48d898d53db5b58ea6cfc1f518ab5ed07579f77d7eae5b76b5271c8a4be2`.
Final CLI/Terminal republishing preserved executable hashes, so the matching
runtime evidence remains applicable. All package notices were retained.
Desktop's native-host runner separately passed 21 checks in English and 21 in
Arabic on Linux X11. See the [Desktop evidence](DESKTOP_CLIENT.md#native-qualification)
and [Terminal contract](TERMINAL_CLIENT.md) for the runtime mechanisms and limits.

The isolated Desktop environment lacked services required for a native file
dialog. Selection and failure behavior passed typed-boundary/native-host tests,
but real dialog completion is unqualified. Other operating systems, physical
devices, assistive technologies and community Arabic review remain unqualified.

### Import ownership diagnostics

Separate instrumented Desktop and Terminal builds each completed six warmups
and twenty observed cycles on .NET 10.0.10. The maximum synthetic input was
237,596 bytes, SHA-256
`799183a80c43b4ee16736baea82e03f83aee01843fb0c41e9ec28a7f674cf95d`.
Every observed cycle saw live owners before retirement, recorded 593 checkpoints,
and finished with owner vector `[1,1,1,1,0,0,0,0,0,0]`: proposal creation, input
disposal, cleared bytes and proposal disposal observed, then six absent weak
owners. Maximum conservative whole-heap/retired-heap observations were
32,772,848/7,803,640 bytes for Desktop and 63,112,056/5,026,320 bytes for Terminal.
These diagnostic whole-heap observations do not establish a release performance
budget. Terminal's probe candidate precedes the independent keyboard correction.

The Artifacts suite directly observes retained input and publication memory
becoming zero after success, cancellation and exceptional paths. `SecretValue`
tests establish copy isolation, idempotent disposal and revoked access; clearing
its private character array remains source-reviewed rather than directly
observed by those tests. No inspection API or friend-assembly expansion was
introduced solely to expose that buffer. This does not claim erasure of immutable
strings or operating-system clipboard copies.

The ordinary compiled dependency closure passed eight runtime-owner checks with
no engineering probe types or callbacks. Ordinary release targets additionally
reject any of the three diagnostic switches with `CONFIGURATOR_PROBE_RELEASE_FORBIDDEN`;
six actual target invocations rejected before modifying existing output.

An isolated Browser Release/AOT candidate then passed twenty actual file-input
and Clear interactions through Chrome DevTools. Every cycle positively observed
live proposal, evidence, answers and assignments before clearing. Rechecking all
cycles after collection produced `[20,0,1,1,1,1,1,0,0,0,0,0]` for every cycle:
twenty starts, zero probe errors, five observed lifecycle facts and five absent
weak owners. The numeric receipt SHA-256 is
`df57c7c564fdfee03ebf2fb47be25d332104d184eac26999e6be0203fb848997`.
Its native WASM SHA-256 is
`16ca022549dddf8ccb82164fde3fdfc24885bb379b58b52dc1b531e1c6b6035b`;
its Browser Webcil SHA-256 is
`9aeffdbf2898615bf5637214de9dff585dae41e798c15ce798017902a417b50b`.
An initial automation upload addressed a retired file-input element and generated
a disposed-reference console error without observing input. Re-querying the
current element allowed all twenty observed cycles to complete. That diagnostic
page is not claimed to have a clean console; missing input was not counted as a
passing cycle. Ordinary-package absence is qualified separately.

### Published Browser checkpoint

Chrome DevTools exercised a published AOT candidate with full ICU globalization:
English/Arabic rendering, masked secret generation, consent revocation clearing
all secret fields and output, literal map keys with duplicate/count/length
rejection, and empty local/session storage. Earlier candidate checks additionally
verified focus restoration, editor retirement, offline cached reload and an
uncached-origin offline rejection. All 99 inspected cached bodies matched public
package assets; definition cache was empty. Deliberate JSON download was also
independently saved and its synthetic content confirmed by the user.

These observations retain their candidate identity; final Store/no-Store
publication and container checks are separate. The final Store-enabled package
contains 393 files, full ICU data and two catalogue definitions. Its ordered
inventory SHA-256 is
`ef66e1b910e2c0c80d74440c4846613f3d26098f4f0cc9c4fe364d00bcca3b55`;
its service-worker manifest SHA-256 is
`af4d3b2a9e133d7df879e18cbf194b294c4f38cf36adef3151e3afa8480a5df8`;
its native WASM SHA-256 is
`544ac939aad6278346a328f309fc34d8836340ff33a940c6ccae593683256d04`.
A fresh browser context booted this exact candidate, confirmed full
globalization and absent engineering exports, and exercised search results,
Escape focus restoration, query retention and field navigation. Editing while
search was closed kept it closed and retained the query. No warning or error
was reported on that page. Republishing the default output from Store-enabled
to no-Store removed all catalogue assets and left 384 files. Its inventory
SHA-256 is `a3d495c5262780d16e9a551b539307e67905855270c0fc71c8538304e130d792`;
its manifest SHA-256 is
`e261fc0d40fc1d4f91d1d62dc88e39e517acf0cda563f8351eadcd35e9e8fd1d`.
Both variants retain the same native WASM hash above. Final Architecture package
checks passed against both outputs.

### Browser Host containers

The no-Store image built successfully from the current source and ran read-only
as UID 1654 with all capabilities dropped and `no-new-privileges`. Actual HTTP
checks passed for public routes, unknown/private paths, methods, MIME and exact
asset bytes, security headers and absence of the synthetic private marker in
logs. Its public payload contained 384 files, full ICU data and no Store assets.
The image identity is
`sha256:62b80043dd546318fa8587ea008a2343f543644f8d285b427cd419b5d4170714`;
its worker manifest SHA-256 is
`55645009c0d6694c8b39ab57ddb2932117180d742b7097b051993d4932d572c2`;
its native WASM SHA-256 is
`36fcb68d122d32e4b57d7f6e555157912217c76c4a4e1dcd001300b41ec03ffb`.
Container builds have their own payload identity; workstation hashes are not
substituted for these bytes. A fresh Chrome context booted the no-Store image,
showed two built-in definitions with explicit optional-directory-unavailable
guidance, and opened the Minimal workspace with secret entry disabled. Runtime
globalization was `all`, engineering exports were absent and local/session
storage was empty. The missing optional index produced a handled resource error through the
active worker; an explicit verification fetch repeated it. This page is not
claimed to have a clean console.

The Store-enabled image also built successfully and passed the same hardened
runtime assertions, including exact Store-index bytes. It contains 393 files and
one full ICU asset. Its image identity is
`sha256:3ccc2a6af70e89702dce86f4368f4c550580e17c339b303aa698972a6142c0e5`;
its worker manifest SHA-256 is
`c066d2a3407f988dc0323fb9d8ae3ebd9788dc39c36e88cd3c552d4d15fe34c4`.
Both images have the same native WASM hash above. A separate read-only fixture
container verified `.mjs` MIME and exact bytes without modifying either emitted
package; that temporary fixture container was removed afterward. A fresh Chrome
context booted the Store-enabled image, fetched its two-project optional index
with HTTP 200 and JSON MIME, and opened a built-in workspace. Full ICU mode,
native WASM identity and absent engineering exports were confirmed; local/session
storage was empty and no console warning or error was reported on that page.

No remote publication, signing, trust promotion or target application acceptance
is implied by these local checks.

## Enterprise architecture Browser image verification (2026-11-06)

The actual Dockerfile initially failed publication because its context filter
excluded `Configurator.BuildCapture`, now referenced by the ratchet generator.
The filter admits that first-party project's top-level C# sources and project
file while retaining its private, task, artifact and compiled-output exclusions.
No task-host exception or runtime dependency was added. Both resulting web
payloads exclude the native capture, Artifacts and MSBuild assembly families.

Both local image builds used SDK 10.0.302, the pinned Dockerfile bases,
repository-root context, the Dockerfile-specific ignore file, linux/amd64,
`--layers --jobs 1`, and the two `INCLUDE_DEFINITION_STORE` values:

```bash
podman build --layers --jobs 1 \
  --ignorefile src/Configurator.Browser/Dockerfile.dockerignore \
  --platform linux/amd64 --build-arg BUILDPLATFORM=linux/amd64 \
  --build-arg INCLUDE_DEFINITION_STORE=false \
  --iidfile .tmp/eaq-current-image-verification/no-store.iid \
  -f src/Configurator.Browser/Dockerfile \
  -t localhost/configurator-eaq-no-store:20261106 .
podman build --layers --jobs 1 \
  --ignorefile src/Configurator.Browser/Dockerfile.dockerignore \
  --platform linux/amd64 --build-arg INCLUDE_DEFINITION_STORE=true \
  --iidfile .tmp/eaq-current-image-verification/store.iid \
  -f src/Configurator.Browser/Dockerfile \
  -t localhost/configurator-eaq-store:20261106 .
```

Both exited 0 and passed all three ratchets. The no-Store Host build reported
zero compiler warnings/errors; Podman separately warned that its explicit
`BUILDPLATFORM` argument was unconsumed. The Store command omitted that redundant
argument. The failed build log remains alongside the successful logs.

| Variant | Local image ID | Worker-manifest SHA-256 |
| --- | --- | --- |
| No Store | `317c8dbc7c8e0517ca46911821100f3b9de6ebdc6e760c55c2b303cdabbf1166` | `0001dc5e49ea05d5236368442f315540e93a4ee356d72573bbec6dbd82ddad29` |
| Store | `1da4ea6bea4c428f8fb8485bb3fad08a3cd70e1b70ce79b092ad0e24e68e9304` | `eba3dc1a0aba7a22e3390340ccdfd5e26e2cd79de0374194ee1ce4dfa7f1bc6c` |

Both Host DLLs hash to
`924a87742971aaaab6f51a19cf72aae2298eb84fa082873d91e71a86fd327524`.
Clean public trees were extracted from never-started containers for the package
checks, without their runtime attack mounts.

Each actual image ran read-only as UID/GID 1654 with all capabilities dropped
and `no-new-privileges`. Kernel observations confirmed zero effective
capabilities and `NoNewPrivs: 1`. Three synthetic unpublished files were physically
mounted in `wwwroot` before HTTP verification. The existing
`BrowserHostPolicyTests` passed 22 cases against each image; all four
`BrowserStorePackageTests` passed against the exact extracted pair, with zero
failures/skips. Host logs contained zero bytes.

Fresh pinned Chrome 151 profiles exercised the published runtime. Ten bridge
cases covered missing/unchecked/wrong-lifetime consent and retirement of owned
buffers. The bridge and editor bundle bytes match across both images. A real
Store Form/editor journey used synthetic values: accepted secrets were not
prepopulated in the public document, pending private text retired on revocation,
and a fresh consent grant did not restore the cleared credential.

Cache checks matched 95 no-Store responses, then 96 Store responses plus one
deliberately admitted public revision. All compared bytes matched public
inventory; storage, IndexedDB and cookies were empty. The first Store audit used
the launch URL instead of the document base and classified all entries unknown.
That result is retained; the corrected identity comparison uses the observed
application base. A separate outgoing-channel observation during a synthetic
edit/discard saw one ServiceWorker and one MessagePort send, no canary payload,
no truncated/unsupported scan and 97 unchanged public cache responses. Temporary
observers were restored afterward.

Both origins were stopped for real offline journeys. The no-Store catalogue
opened a built-in workspace. Store reload correctly rejected its transient
launch with `family-unknown`; deliberate reopening from the cached catalogue
admitted the pinned public revision into a fresh workspace with secret consent
off. Cached definitions did not imply persisted answers or registry authority.
All owned browsers, profiles and runtime/extraction containers were retired.

Task-local logs, raw reports and value-free observation records are under
`.tmp/eaq-current-image-verification/`; these are not a durable release archive.
The checks qualify these local image/privacy mechanisms. They do not establish
complete build-input capture, frozen candidate provenance, performance,
assistive-technology, other-host or participant qualification, signing, trust
activation or publication. Those remaining workstream gates stay explicit.
