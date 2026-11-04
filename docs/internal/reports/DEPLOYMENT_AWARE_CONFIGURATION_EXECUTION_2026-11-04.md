# Deployment-aware configuration execution — 2026-11-04

> **Audience:** Contributors | Maintainers | Reviewers
> **Kind:** Execution record
> **Status:** Implementation and automated/native/package verification complete; actual Browser qualification open
> **Owner:** Configurator maintainers
> **Last Verified:** 2026-11-04
> **Source Anchors:** [`DefinitionScopeResolver`](../../../src/Configurator.Core/Definitions/DefinitionScopeResolver.cs), [`DefinitionConfigurationContext`](../../../src/Configurator.Presentation/Form/DefinitionConfigurationContext.cs), [deployment decision](../adr/ADR-deployment-context-and-destinations.md), [packaging](../PACKAGING.md)

This record describes the uncommitted candidate in the existing checkout based
on `97c5a1789545b73a9536a4b9d4fa934976afb16c`. It is not a release, target
acceptance receipt or qualification of unrelated changes already in the checkout.
No branch, worktree, commit or remote publication was created.

## Delivered flow

Definitions declare finite recipes, component scopes, prerequisites and output
destinations. Core admits this metadata before answers and resolves the selected
scope through observations from the existing validation kernel. One immutable
dependency index belongs to the definition; each evaluation builds its own
concrete mask. Requiredness, effective values and complete/draft composition all
use that mask. Global malformed-input checks remain active. An explicit request
for an excluded destination rejects instead of broadening scope.

Presentation changes a compatible recipe/scope pair in one accepted-answer
revision. That edit invalidates prior preparation, import, document and exposure
work. Inactive accepted answers remain available for switch-back, while excluded
secret values never enter effective output. Consent revocation clears secret
leaves across scopes. Clients consume the same current context and eligible
destinations through their existing interaction models.

Destination metadata distinguishes Docker process env-files from Compose
interpolation input. Their codecs preserve representable literals and reject
unsupported control/multiline values. Import captures explicit public context,
dialect and input revision. Repeated root scalars for genuine multiple consumers
are recoverable only under a common output parent and matching guards; all
observed values and presence must agree. Conflicts refuse atomic adoption.

Browser aggregate delivery retains named file boundaries, uses bounded private
buffers and reuses the existing export generation fence. Its clipboard and ZIP
delivery ceilings include overhead, separately from source bytes. Native
clients retain restricted create-only output and truthful installed receipts.

## Executed owner evidence

Every command below used the repository root, SDK `10.0.302`, an `rtk` prefix
and `TMPDIR="$HOME/.cache/agent-tmp"`. Release compilation used
`-p:UseSharedCompilation=false` and serialized builds (`-m:1` and/or
`-p:BuildInParallel=false`); build and MTP test execution were separate. Test
runs had a nonzero minimum-count guard.

| Owner/check | Entry baseline | Current candidate result |
| --- | --- | --- |
| Core full suite | 469 passed | 536 passed, 0 failed/skipped |
| Presentation full suite | 307 passed | 318 passed, 0 failed/skipped after latest Core inverse |
| CLI full suite | 270 passed | 277 passed, 0 failed/skipped |
| Terminal full suite | 150 passed | 153 passed, 0 failed/skipped |
| Desktop application slice | 40 passed | Full owner 304 passed, 0 failed/skipped |
| Browser output-selection slice | 6 passed | Full owner 418 passed, 0 failed/skipped |
| Existing entry packages/compiled boundaries | 40 passed | Complete current Architecture suite 83 passed, 0 failed/skipped |

The final solution Release build exited 0 with zero warnings/errors (1m44.46s):

```bash
dotnet build --configuration Release --verbosity quiet --no-restore -m:1 \
  -p:UseSharedCompilation=false
```

Core final test-project Release build exited 0 with zero warnings/errors.
Its final full-owner command exited 0:

```bash
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj \
  --configuration Release --no-build --no-restore --verbosity quiet -- \
  --minimum-expected-tests 1
```

The corresponding Presentation command, with minimum count 318, passed 318/318
after the final Core inverse; its Release test-project build had zero
warnings/errors. Focused context tests passed 11/11. Browser product and
test-project Release builds had zero warnings/errors; its final full MTP command
with minimum count 418 passed 418/418. Final focused bundle tests passed 15/15,
including cancellation between assembly entries for clipboard and ZIP without
partial adoption or JS exposure. Other focused counts were scope 5, review 11,
output selection 6, export lifecycle 8 and Developer editing 59. The six full
product suites total 2,006 passing tests; the bounded Desktop/Browser entry
baselines are not retrospectively claimed as full-owner baselines.
Focused Core checks passed 116/116 before later integration additions: deployment
scope 9, concrete boundary 18, admission 9, authoring 5, dialect 12, Event scope 4,
Event provider 54 and explicit context import 5. The final Core run includes
later Event and scalar-group regressions.

Native schema generation `--write`, `--check` and `--check-examples` passed;
example agreement was 4/4. Only the setup-definition and authoring schemas
changed for definition metadata. Native Store generation/freshness passed after
revision 3. CLI and CLI.Tests Release builds had zero warnings/errors; its full
MTP command passed 277/277. CommandSchemaGenerator Release build and native
`--write`/`--check` passed. `RenderCommandReference.cs --self-test` passed 113/113;
`--write`/`--check` reported written/current. The CLI schema and command reference
were generated from their owners. Native ratchet `--write`/`--check` passed,
3/3 current; only the definition/authoring/CLI schema hashes changed in
[`frozen-contract-baseline.json`](../../../eng/configurator/generated/frozen-contract-baseline.json).
Final distribution checks passed against all five settled publications.

The native browser-editor owner passed `--write .`, `--check .` and `--self-test`
(10 passed, 9 rejected, network none). The managed header changed its own input
hash, so the generator refreshed one source-hash line in
`configuration-editor.manifest.json`; editor JavaScript, licenses and approval
content were unchanged. The explicit documentation checker passed 25 owned
Markdown files, 286 local links, zero errors using its existing managed binary
(`--no-build --no-restore`) and an isolated lock path. The final report/ledger
recheck passed three files, 12 local links, zero errors. The public launch guide adjunct separately
passed one file, seven links, zero errors.

Terminal Release compilation had zero warnings/errors. Its built MTP executable
passed 153/153 through the actual runner:

```bash
dotnet run --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj \
  --configuration Release --no-build -- --minimum-expected-tests 153
```

A driver attempt that selected zero tests was not counted as evidence. The three
new tests cover narrowed scope, held preparation invalidation and actual Linux
protected-save bytes/0600 permissions. CA2000 ownership diagnostics were fixed
by retaining Terminal.Gui's existing `Add(params View[])` ownership pattern;
no diagnostic was suppressed.

Actual Linux PTY execution at 110×36 used Tab/Enter to narrow worker/dashboard to
dashboard-only, changed fields/destinations from two to one, prepared with Alt+E
without worker credentials, and saved with Alt+S. The only file was exact
`{"name":"dashboard-demo"}`, mode `0600`; keyboard Close exited 0. A second
90×30 session changed to Arabic and narrowed using the keyboard. Closing without
saving returned the expected no-save exit 4. Neither process remained running.
Exact evidence is in [Terminal qualification](../TERMINAL_CLIENT.md#deployment-scope-qualification-2026-11-04).

Desktop product and NativeHost Release builds had zero warnings/errors; the full
Desktop owner passed 304/304. NativeHost passed all 22 guards separately in
English and Arabic on Linux x64/X11/Xvfb, including D22 narrowed output bytes and
`0600` permissions. Captures and `qualification.json` are retained under
`.tmp/deployment-aware-configuration/desktop-native-{en,ar}/`. Native captures
were visually inspected; manual dialogs, physical keyboard/AT and installed
payload behavior remain separate checks. The host command was:

```bash
CONFIGURATOR_NATIVE_UI_CULTURE=en xvfb-run -a dotnet run \
  --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj \
  --configuration Release --no-build -- --expected-rid linux-x64 \
  --minimum-expected-checks 22 \
  --capture-directory "$PWD/.tmp/deployment-aware-configuration/desktop-native-en"
```

The Arabic invocation changed the culture to `ar` and capture suffix to `-ar`.

## Settled packages and distribution exit

The owning release targets published CLI, Terminal and Desktop for Linux x64 to
`.tmp/deployment-aware-configuration/packages/{cli,terminal,desktop}/linux-x64/`.
Both actual Browser AOT publications succeeded: Store-free under
`.tmp/deployment-aware-configuration/packages/browser/wwwroot/`, Store-enabled
under `.tmp/deployment-aware-configuration/packages-with-store/browser/wwwroot/`.
All output was isolated from the user's default publications.

Each native publication used the owning command below. The other two actual
invocations changed only `ConfiguratorSurface` to `terminal` and `desktop`:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj \
  -nologo -m:1 -verbosity:quiet -t:PublishConfigurator \
  -p:Configuration=Release -p:ConfiguratorSurface=cli \
  -p:ConfiguratorRid=linux-x64 \
  -p:ConfiguratorOutputRoot="$PWD/.tmp/deployment-aware-configuration/packages"
```

The two Browser commands were:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj \
  -nologo -m:1 -verbosity:minimal -t:PublishConfiguratorBrowser \
  -p:Configuration=Release \
  -p:ConfiguratorOutputRoot="$PWD/.tmp/deployment-aware-configuration/packages-with-store" \
  -p:DefinitionStoreSourceRoot="$PWD/store"

dotnet msbuild eng/configurator/Configurator.Release.proj \
  -nologo -m:1 -verbosity:minimal -t:PublishConfiguratorBrowser \
  -p:Configuration=Release \
  -p:ConfiguratorOutputRoot="$PWD/.tmp/deployment-aware-configuration/packages"
```

Browser publication used `DOTNET_PROCESSOR_COUNT=2`, `UseSharedCompilation=false`
and `BuildInParallel=false` to bound build resource use. The Store-enabled log
records actual AOT of 66 assemblies, including Core, Presentation and Browser,
bitcode/object compilation, final WASM linking and optimization. Its elapsed
time was 7m45.255s. Store-free publication reused those AOT outputs and completed
in 18.415s. Both retained original metadata and license notices for 37 dependency
packages. These are build observations, not application timing/memory results.

| Published artifact | SHA256 |
| --- | --- |
| CLI executable | `f6add55a2d685ec1f7e3794f1d99509eb722c2b74f6585015198deeacfabdcf4` |
| Terminal executable | `1c511181e78fcd906b92ba5e0eeabb77fc48a290ccacef35d659c0745fd5a864` |
| Desktop executable | `3f74338257e5f83a58f1d1f58a89adac41059b90e509dbbc41410b59e1e669b0` |
| Final native WASM, both Browser variants | `557a5ff1f93303bc282ce9239ac8f547acb9a1bf151c3674bd66f95c2dcd1f00` |

The complete current Architecture suite passed **83/83**, zero failures/skips,
exit 0, in 46.756s with both explicit package roots. It exercised actual packaged
CLI commands, native contracts, compiled boundaries, distributed schemas,
dependency notices/policy, editor closure, Store canonical identity and SDK
worker asset hashes. No failure was quarantined and no test was skipped.

```bash
CONFIGURATOR_PACKAGE_ROOT="$PWD/.tmp/deployment-aware-configuration/packages" \
CONFIGURATOR_STORE_PACKAGE_ROOT="$PWD/.tmp/deployment-aware-configuration/packages-with-store" \
dotnet run --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj \
  --configuration Release --no-build -- --minimum-expected-tests 1
```

Final no-build native freshness checks also exited 0: definition schemas current,
source/runtime examples 4/4, Store 4 revisions generated/current, ratchets 3/3
current. No product suite was repeated merely because packages were built.
The compiled package checks do not establish actual Browser interaction.

## Invariant-first evidence and repairs

The initial scoped Core runtime test failed because dashboard-only preparation
returned two artifacts, including the retained worker credential output.
Explicit-context import Red ran 3 tests: 2 failed and 1 passed. Presentation's
initial atomic-context Red failed 9/9. Browser aggregate Red failed 7/12;
controls and pending-document slices each failed 3/3 at missing capabilities.
Terminal adapter Red ran 49: 47 passed and 2 failed on absent context controls.
Desktop context Red ran 4: 2 passed and 2 failed on retained worker selection.
These were executable assertions; incomplete-source compiler errors and
zero-test attempts were not counted as runtime Red.

Independent review found two additional scope defects. An uncertain missing
`dependentRequired` trigger could drop a public obligation; closure now remains
unresolved. An excluded numeric row retained raw content in the effective array;
an empty placeholder now preserves indexes without the content. Both received
runtime regressions. Scoped dotenv metadata also now requires its explicit
surface/dialect pair.

Integration repaired global-diagnostic ownership in Presentation progress,
inactive nested draft blocking, scoped canonical replacement, stale import
adoption and legacy manifest-selector behavior. The dialect data contract has a
narrow enum/nullable-enum allowance in the compiled portability test; a compiled
forbidden-document canary still rejects native dotenv document contracts.
An obsolete quote-wrapped Docker expectation was changed to the independently
qualified literal bytes.

Repeated-scalar import Red ran 5: 4 failed and 1 passed. Final Core Green includes
all five. Recovery observations are local to each inspection; item-relative,
container, ancestor/descendant, different-parent and different-guard overlaps
remain unsupported. Independent source review found no further issue. Initial
full Core runs of 529/521/8, 530/527/3 and 536/534/2 were repaired integration
failures; they were not quarantined as unrelated baseline failures.

## Event identity and actual consumer evidence

The maintained source was checked against read-only Event commit `b6ad1594b7`.
Dirty target defaults/runbooks were distinguished from image facts. The public
inventory records the exact 92-name missing-field delta and recipient/default
evidence. Redundant erasure exports were removed because Event projects the
canonical `ERASURE_DATABASE_*` inputs internally. Both genuine admin-origin
consumers remain, including Standalone.

Both maintained families advanced natively to revision 3. Example canonical
digest: `87926b48285104642ee7691c9117319a454d9f019eb88d7d19a8cd87e7cc8d9e`.
Store snapshot 1 SHA256 remains
`65443aa92be441ec42728acf0cf31fd1dcf49fa5914689e1a6c48cc488b3cbf6`;
snapshots 1 and 2 were unchanged when snapshot 3 was appended. Revision 2's four
actual CLI environment writes matched independent expected files; revision 3's
actual admin-origin Red/Green demonstrated the corrected mapping. Manifest
expected bytes were preserved. The temporary native migration tool was removed.

Docker Client/Server `29.8.1` and Compose `v5.5.1` reproduced synthetic literal
values exactly, including spaces, Unicode, dollars, quotes, backslashes, equals
and empty assignments. Docker used the existing explicit
`unix:///var/run/docker.sock` endpoint and installed `redis:7.2.16-alpine` image
(`sha256:ee061d86c25773048211e99f00d63b47b9ecfe28560113e16cce2c962d5bec1c`),
with `--rm --pull=never --network=none --read-only --cap-drop=ALL
--security-opt=no-new-privileges --entrypoint /usr/bin/env`. No image was pulled
and Event was not started. Compose used an allowlisted `env -i` environment;
the conflicting-host-variable negative case confirmed that `--env-file` alone
does not isolate interpolation. Both positive byte comparisons exited 0.

Research used official [Docker environment guidance](https://docs.docker.com/reference/cli/docker/container/run/#env),
[Compose interpolation guidance](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/),
and [Microsoft Blazor synchronization-context guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0). Functional conclusions
informed independently written code; no external implementation or dependency
was imported. These references were refreshed on 2026-11-04.

## Qualification limits

CUA inventory, including a later retry, returned no enabled browsers; opening the in-app browser failed
with `Browser is not available: iab`. An enablement request is pending. Actual
published-WASM keyboard/RTL/reflow, offline custody, copied/downloaded files,
peak allocation and timing have not been observed for this candidate. Automated
component/archive tests and package inspection cannot substitute for those
checks. Physical-device, assistive-technology and participant studies are also
unclaimed; predecessor workstream studies retain their own status.

The SDK file-app default attempted to restore an unrelated vulnerable
`Microsoft.DotNet.ILCompiler` 10.0.10 (`NU1903`, `GHSA-p3jp-9v83-76w9`). Four
publication engineering tools now explicitly use managed execution rather than
acquiring that unused compiler dependency. Evaluation confirmed Browser Release
still has `RunAOTCompilation=true` and native publication remains self-contained,
single-file and untrimmed. No audit or analyzer warning was suppressed.

Unrelated pre-existing Browser CSS trailing blank lines remain outside this
change's ownership. Bounded owned-file whitespace checks passed; whole-checkout
whitespace is not claimed clean. No target startup/login, target acceptance,
other operating-system qualification or remote publication is claimed.
