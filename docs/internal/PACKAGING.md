# Configurator packaging ownership

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Implemented local packaging; publication authority not activated
> **Owner:** Packaging/Release
> **Last Verified:** 2026-11-02
> **Source Anchors:** `eng/configurator/Configurator.Release.proj`, `src/`, `tests/Configurator.Architecture.Tests/`

The [current development candidate record](OPERATIONS.md#fixed-development-baseline-001-2026-11-02)
binds the current Linux packages and both Browser AOT Store variants to
their executed checks and hashes. All affected Artifacts cases now pass;
historical Red gaps and unexecuted hosts remain explicitly scoped.

`global.json` selects both the SDK and workload-set versions. Before the first
restore on a clean SDK installation, `dotnet workload update` initializes the
pinned manifests, including on native-only and public-data CI jobs. The command
runs from the repository root so the pin remains authoritative. Browser builders
add `wasm-tools`; native-only jobs do not install that workload.

CI explicitly restores and builds the static Browser host before auditing its
dependencies with `--no-restore`. The host has no product project references, so
restoring Browser tests alone does not produce its assets file. Audit logs name
each project before invoking the SDK, including when the SDK rejects its graph.

Native CLI jobs use the runner-owned temporary directory for their tests.
macOS system temporary paths can traverse `/var` aliases; source-authoring
tests must not depend on that alias while the actual loader correctly rejects
symlinked paths. This changes test placement, not source-admission policy.

The standalone CI job allows 45 minutes for locked builds, three actual Release
AOT Browser publications and the complete owner suites. The observed pre-test
stages consumed about 25 minutes on a clean hosted runner; the deadline
includes the remaining verification rather than changing any test or interaction
budget.

Definition schema and release capability generators normalize indented JSON to
LF before writing bytes or computing frozen-contract hashes. Native package
freshness checks compare those canonical bytes on every host; Windows must not
introduce CRLF differences into unchanged contracts.

`Configurator.Release.proj` explicitly declares the five operator schemas copied
to native and Browser packages. Engineering capture, check-execution,
test-evidence, provenance and qualification schemas are separate tool contracts;
adding one under `schemas/` must not expand the operator package inventory.
The existing standalone package assertion verifies that exact inventory.

`eng/configurator/Configurator.Release.proj` owns native surface/RID admission,
standalone publication and independent static browser publication. Its default
`Build` target restores locked development dependencies and builds the selected
native client. `PublishConfigurator` requires an explicit RID, embeds native
dependencies, disables trimming, renames the apphost to the documented operator
name and includes notices, schemas and generated contract data.
Both native and Browser release entrypoints reject the import memory, ownership
and native comparison probe switches with `CONFIGURATOR_PROBE_RELEASE_FORBIDDEN`
before running generators or changing output. Measurement candidates use direct
project build/publish to isolated output; see
[instrumentation ownership](IMPORT_COMPARISON.md#instrumentation-ownership).
This release gate complements ordinary assembly metadata checks; it does not
inspect metadata inside compressed native bundles or Browser Webcil files.
For CLI packages, the release target copies the canonical portable integration
skill and resources to `skills/configurator-integrate/`. The internal repository
skill is only a router and is not a second distributed workflow.
`ValidateConfiguratorSkill.cs --portable` validates the shipped flat-scalar
Agent Skills metadata; `--self-test` exercises valid and rejected metadata.
Nested portable metadata is outside this validator's current supported profile.
`write-dependency-notices.cs` reads the actual restored assets closure and retains
original nuspec metadata and every package license/notice file, including native
dependency attribution. It also evaluates selected SDK framework references so
the embedded .NET native/Mono runtime's own notices are retained; those runtime
packs are not ordinary NuGet asset libraries. This conservative inventory also retains build-time
package notices; it does not copy assemblies or executable inputs.

The ratchet, definition Store, browser editor and dependency-notice file-based
engineering tools explicitly use managed execution (`PublishAot=false`). They
run during packaging and need no native AOT compiler dependency. This setting
does not alter the packaged native single-file contract or the Browser Release
`RunAOTCompilation=true` setting; Browser publication still compiles WASM AOT.

RID-specific restore uses separate `obj/configurator-release/<rid>/packages.lock.json`
files so native publication never rewrites framework-only development locks.
Those ephemeral locks are generated from pinned dependencies for packaging;
publish then uses the evaluated RID closure without another restore. CI's normal
owner/probe restores are locked. Terminal.Gui is the official pinned package;
its embedded grammar/theme notices supplement ordinary NuGet metadata through
`eng/configurator/dependency-notices/`. A supplement matches an exact package
version and content hash; missing or mismatched review data rejects publication.
Unrelated surfaces do not acquire these notices. See the
[dependency decision](TERMINAL_GUI_DEPENDENCY.md).
Native publication replaces its generated `dependency-notices/` tree before
writing the current closure, so retired dependencies cannot survive a republish
into the same package destination.

Native `PublishConfigurator` invokes `RefreshConfiguratorSchemas` at the schema
copy stage. This target validates the native surface and requires a RID, removes
only `$(ConfiguratorPublishDirectory)schemas`, then copies the current public
`schemas/*.schema.json`. Retired versions cannot survive republishing; unrelated
package content outside the owned schema directory is retained. The stage can run
independently without restoring, building or publishing executables.
`ReleaseContractTests` seeds an unsupported command schema in a temporary package,
executes this actual MSBuild target for each native surface, and checks exact
current-schema names/bytes plus preservation of unrelated package files. It uses
the repository's current schema set without a historical-version allowlist.

`PublishConfiguratorBrowser` removes every inherited native publication flag
before restoring/publishing `Configurator.Browser`. Distribute its complete
`wwwroot/`, not native apphost output. This separation avoids interpreting browser
capability as native protected-save support. Supported native packaging RIDs and
operator paths are listed in the [public guide](../public/packaging.md).

Publication replaces the generated `<ConfiguratorOutputRoot>/browser/` directory
before publishing, preventing retired assets from surviving a repeated package
build. SDK-generated Brotli/gzip sidecars are transport encodings of public assets;
the store package test requires each sidecar to decode to its exact canonical
JSON bytes and rejects any other store file.

### Local editor assets

`eng/configurator/BuildBrowserEditor.cs` owns the local editor bundle. Its source
manifest and native npm lock under `eng/configurator/editor/` pin the reviewed
component graph. The recipe binds the documented approval decision, lock,
original-notice hash and exact Node/npm/Bun build-tool versions. These tools are
used for regeneration, not conveyed as Browser runtimes.

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --self-test
dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --write "$PWD"
dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --check "$PWD"
```

Generation disables npm lifecycle scripts, verifies installed original terms
against the approved closure and invokes the local bundler. Never hand-edit the
three generated files under `wwwroot/vendor/`: `configuration-editor.js`,
`configuration-editor.manifest.json` and `configuration-editor.LICENSE.txt`.
The manifest binds input, component, toolchain and output identities. Its
component inventory supplements the separate NuGet/runtime notice inventory.

`VerifyBrowserEditorAssets` runs the offline freshness check before Browser
publication. It requires no npm installation or dependency download. Missing
approval, changed inputs/closure/toolchain metadata, altered output or omitted
terms fail closed. `PublishConfiguratorBrowser` additionally requires all
editor files in the actual publication. The worker allowlist includes only the
fixed public bridge, bundle, manifest and notice URLs; no source text, history,
diagnostics or answer-derived completion enters that inventory.

Distribute the complete notice file alongside the bundle. The root
`THIRD-PARTY-NOTICES.md` routes reviewers to the exact
[dependency decision](legal/dependencies/codemirror.md); original component
copyright and permission text is not replaced by the project's MIT license.
The [native owner guide](../../eng/configurator/editor/README.md) describes
metadata-only review and generation. Reproducible assets and package hashes do
not establish runtime custody or user-experience qualification.

### Optional definition store assets

Optional store inclusion is explicit through `DefinitionStoreSourceRoot` on
`PublishConfiguratorBrowser`; the empty property never reads or requires `store/`.
`PrepareDefinitionStoreAssets` in the Browser project invokes the native
`BuildDefinitionStore.cs --source ... --output ... --write` before
`ResolveProjectStaticWebAssets`. Generated index and canonical snapshots live
under the configuration-specific intermediate directory, mapped to
`wwwroot/definition-store/`, rather than being copied after publication. The SDK
therefore inventories and hashes every generated public asset in
`service-worker-assets.js`. Metadata schema generation remains owned by the
builder and repository schema; it is not a second hand-maintained contract.

The generation target clears its owned intermediate store output on each build.
`ClearPublishedDefinitionStore` clears only the owned `definition-store/` subtree
before publication, including when the property is empty. A store-enabled build
followed by a store-free build in the same destination cannot retain catalogue
files or manifest entries. No source directory mutation is required. Invalid
explicit store content fails validation before assets are admitted.

Store snapshots are bounded public app content. Packaging them does not grant
consent to retain independently opened definitions, and never admits answers or
prepared artifacts into public caches. Discovery remains lazy in the browser;
store absence cannot become a startup, Core, CLI or native-client dependency.

Local source loading in Terminal, Desktop and Browser uses Core's shared
compact-source compiler independently of store assets. Browser local upload
accepts JSON/YAML; public remote and retained definition assets remain canonical
runtime JSON. Source comments, answers and prepared output never become public
package or offline-cache assets.

The published PWA includes `manifest.webmanifest`, `service-worker.js`, the
SDK-generated `service-worker-assets.js`, `pwa-lifecycle.js` and original icons.
Ordinary interop modules live under `js/`; the PWA bootstrap and service workers
remain at the application root so their registration and scope stay aligned.
The worker caches only allowlisted public SDK-manifest app assets; explicit
definition retention is separate. No answers or output enter either cache.
Publish all assets as one static tree with correct MIME types, HTTPS/localhost
secure context and SPA fallback for `/launch/*` and `/open`. Missing asset
requests must not return `index.html`. Base path, worker scope and manifest
must agree. Worker activation waits for a clean lifecycle across tabs; a package
does not prove real browser update/network/cache behavior.

## Desktop build-statistics policy

Desktop, Desktop.Tests and Desktop.NativeHost must exclude the existing Avalonia.BuildServices
package's build and buildTransitive assets. `Directory.Packages.props` owns its
11.3.2 pin; the three project references own the asset exclusions. Restore owns
lock-file regeneration. Avalonia's required resource/XAML build assets remain
enabled. No capture task admission or unknown-task exemption follows from this
dependency policy.

Verify the effective imports and actual capture graphs for these projects:
the statistics task must be absent and the required resource/XAML tasks must
retain their normal execution and capture bindings. A declared exclusion or
environment opt-out alone is not proof. Verify Desktop's Release/full owner,
actual publication and dependency/notices closure before claiming acceptance.
Central package configuration is a consumed build input; refresh affected
capture/check identities even when product payload bytes happen to agree.
See [Operations](OPERATIONS.md#desktop-build-statistics-verification) for the
evidence boundary and the [source-build guide](../public/packaging.md#building-from-source)
for contributor guidance.

## Qualified definition generation

Product and public contract versions stay `0.0.1` during development, including
breaking changes. Authoring/runtime, command, answers and Store each have one
current format. Package tests compare distributed schemas and frozen baselines with
their owning generators, then execute real package consumers. The optional
store's older active inputs are replaced through native fresh-family generation,
not a compatibility reader or historical digest rewrite. See
[the earlier client evidence](OPERATIONS.md#definition-v2-qualification).
The schema-driven secret payload requires new package qualification. The
`Configurator.DependencyProbes.Cryptography` project exercises the real Core
profiles under Browser AOT before product integration; its results cannot
substitute for running the actual published Browser. The portable
`BouncyCastle.Cryptography` dependency retains its identity and notices in each
distributed closure. See [cryptographic contracts](SECRET_CRYPTOGRAPHY.md).

## Browser container

Deployment-aware browser delivery uses the framework `System.IO.Compression`
ZIP implementation; it adds no package or archive artifact format to Core.
The browser adapter bounds source and final delivery bytes separately to
4,194,304 and limits entries to 64, including archive metadata in the delivery
bound. Component tests inspect actual ZIP names and contents and generation
refusal. They do not establish WASM peak allocation, actual browser clipboard
or download behavior, keyboard/RTL/reflow, or assistive-technology qualification.
Record those observations separately against the actual locally published
package; a successful build or headless component run cannot substitute for
those host observations.

`src/Configurator.Browser/Dockerfile` builds from the repository root and invokes
`PublishConfiguratorBrowser`; it does not bypass ratchet checks, locked project
restores, public schemas/contracts, dependency notices or the SDK's PWA asset
inventory. The Dockerfile-specific `Dockerfile.dockerignore` admits only build
inputs and public definitions, excluding `.git`, task workspaces, environment
files and host `bin`/`obj`/artifact output.
Ratchet verification references the first-party `Configurator.BuildCapture`
project to record consumed inputs. Its project file and top-level C# sources
therefore belong to the image build context; generated capture sessions and
compiled outputs remain excluded. This build dependency does not enter the
final static Host image.
The admitted inputs include feature-folder source, English/Arabic resources and
the editor's reviewed manifest, license, recipe and approval record. The editor
verifier checks these existing inputs inside the image; it does not rebuild or
download editor dependencies during container publication.

The build uses the repository SDK 10.0.302. Its final stage contains the pinned
ASP.NET Core runtime, the small `Configurator.Browser.Host` executable and the
complete published `wwwroot/`, not the SDK, native clients or a server-side
answer workspace. The Browser remains standalone client-side WebAssembly.
Base-image tags and manifest digests are pinned;
the [container dependency record](legal/dependencies/browser-container.md) owns
their identities and redistribution boundary. Keep that record synchronized
when changing either image.

The Dockerfile follows `base`/`build`/`publish`/`final` stages. The build stage
copies central/project/lock inputs first for cacheable locked restores,
including `App.razor`: Razor presence enables the SDK's implicit
`Microsoft.AspNetCore.App.Internal.Assets` reference. Omitting that input
produces `NU1004` against the unchanged lock file. It then copies the remaining
source and builds the static host without another restore. The publication stage
publishes the host and continues to use the existing Browser release owner.
The final stage runs `dotnet Configurator.Browser.Host.dll` as the base image's
non-root application user. No external web-server configuration is generated,
copied or required.

`INCLUDE_DEFINITION_STORE` is a closed `true`/`false` build argument, defaulting
to `false`. Only `true` passes the copied public Store directory as
`DefinitionStoreSourceRoot` to the existing publication target. Container
packaging never revises Store sources or retained revisions, authenticates a
publisher or repairs same-family/revision content conflicts.

`src/Configurator.Browser.Host/Program.cs` owns origin-root public-file hosting:
current application navigation, real missing-asset errors, WASM/JSON/JavaScript
MIME handling, response compression and revalidation of update assets. Responses
retain the shipped first-party CSP and add nosniff/no-referrer policy. The host
has no product project references, answer model, upload endpoint or persistence.
Request-access logging is disabled; ordinary missing/rejected requests must not
write user-supplied URL text to logs. There is no upload handler, proxy, target
API or private persistence. Only `/tmp` needs runtime write access.

The Host CSP includes `frame-ancestors 'none'`. Its content-type provider admits
exact ordinal paths from the SDK's `service-worker-assets.js` JSON assignment
(read as data, never executed), plus worker/inventory URLs, root license/notices
and the fixed schemas/contracts copied by `Configurator.Release.proj`.
The manifest is bounded to 4 MiB and 10,000 entries; malformed or noncanonical
paths reject startup. Unknown MIME types reject even an inventoried path.
JavaScript uses `text/javascript`, WASM `application/wasm`, ICU data
`application/octet-stream`, web manifests `application/manifest+json`, source
maps `application/json`, and the exact extensionless `/LICENSE` uses `text/plain`.
No unknown-file fallback or webroot directory enumeration grants publication.
Dependency-notice trees remain distributed attribution, outside HTTP admission.
SDK compression sidecars are not separate admitted URLs; response compression
operates on canonical admitted bytes.

GET/HEAD preserve the five existing navigation patterns: `/`, `/open`, `/help`,
`/store/{slug:nonfile}`, `/launch/{id:nonfile}`. Unknown routes/assets return 404
and other methods return 405. All responses retain `Cache-Control: no-cache`.
This transport revalidation policy does not broaden the Browser worker's cache
allowlist or authorize storing answers, drafts or prepared artifacts.
`BrowserHostPolicyTests` exercises a running Host at `CONFIGURATOR_HOST_URL`;
qualification must first copy the public package into an isolated tree and add
synthetic `host-attack.json`, `host-attack.unknown` and `host-attack` files.
Run its nonempty filtered cohort against both Store modes and separately record
actual nonroot/read-only container execution; an ordinary HTTP test run does
not qualify container restrictions or a complete Browser journey.

The image is deployment-neutral. Its .NET process serves public static files; it
contains no reverse-proxy upstream, TLS termination, public hostname, host-port
binding, Docker network or platform-specific routing labels. Coolify or another
orchestrator owns those settings and forwards traffic to internal port 8080.
The public guide records Coolify's repository-root context and Dockerfile path;
the local loopback run example is a smoke-test command, not production wiring.

Verify actual image behavior, not Dockerfile text: build both Store modes,
inspect the runtime user and package closure, run read-only with dropped
capabilities, exercise routes/assets/MIME/cache/security headers and rejected
methods, and boot the served app in a real browser. Apply the existing Browser
package tests to the extracted static tree. A local rootless Linux image check
does not qualify Docker Desktop, another architecture, a production TLS proxy
or the complete browser/assistive-technology matrix. The
[public guide](../public/browser.md#docker-container) owns operator commands.

### Verified .NET image (2026-10-20)

Both corrected image builds completed with exit 0 using rootless Podman 6.1.2
on Linux amd64. Docker's local daemon was unavailable; no Coolify deployment
was performed. The store-free image ID is
`b7c914a261f6ccb3e005dc0523c78e05b4b19051b230511e0f296b0f4914ecaa`;
the Store-enabled image ID is
`bd056394656d00c180a588eb915fd821d9ce7deb003a3887a0e48db3c86d4cf7`.
Inspection and execution confirmed `dotnet Configurator.Browser.Host.dll`,
UID 1654, a read-only root with `/tmp` tmpfs, dropped capabilities and
no-new-privileges. The runtime contains neither the SDK nor NGINX. The deleted
repository configuration remains absent.

The new host's Release build passed. Its generated lock has no NuGet package
dependencies. All four existing `BrowserStorePackageTests` passed against fresh
trees extracted from these exact images, with zero failures/skips. The test
roots were `.tmp/browser-container-dotnet-01a157c8/without-store` and
`.tmp/browser-container-dotnet-01a157c8/with-store`.

Actual HTTP checks passed for exact-index navigation fallback, real 404s for
missing assets/unknown routes/private host binaries, 405s for non-read methods,
privacy/update headers, WASM/webmanifest MIME, gzip and decoded published-byte
equality. An invalid Store argument rejected with exit 64. A synthetic query
canary emitted no container logs, and graceful .NET shutdown exited 0.

Chromium rendered the desktop catalogue and mobile neutral-definition workspace.
The PWA readiness gate opened with a controlling service worker, secret consent
off, no password inputs and no captured page errors; the 390-pixel workspace
had no horizontal overflow. The existing home-page primary-link contrast issue
was still visible and was not changed. These bounded observations do not
establish full browser, assistive-technology, contrast or production-host
qualification.

## CLI native admission and host gates

The release project admits exactly seven CLI RIDs: `linux-x64`, `linux-arm64`,
`linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, `osx-arm64` and `win-x64`.
Admission validates the requested RID against the selected surface's evaluated
item list, so the CLI expansion cannot widen Terminal or Desktop admission.
Terminal retains `linux-x64`, `linux-arm64`, `osx-arm64` and `win-x64`;
Desktop retains only `linux-x64`. Browser publication is unchanged.
Transient answer semantics are shared across the packaged clients: explicitly
supplied empty values remain distinct from omission. Browser consent denial
clears secret leaves and retires all current export owners before awaiting
cleanup, without affecting later output. This logical revocation neither
retracts files/clipboard contents already exported nor supplies native
owner-only/create-only protection. No packaging inputs, dependency closure or
protected-save host support change with this repair.
`ReleaseContractTests` evaluates actual MSBuild contract JSON, requires exact CLI
membership and successful validation for each CLI RID, rejects Windows 7 and
unknown RIDs, and checks that the three new RIDs remain refused for Terminal.
All surface contracts continue to report `protectedExportHosts: ["linux"]`.

The existing standalone CI lane still checks the three Linux x64 clients and
static Browser. A separate CLI-only native matrix uses these explicitly selected
GitHub-hosted labels:

| Runner | CLI RID | Evidence boundary |
| --- | --- | --- |
| `ubuntu-24.04` | `linux-x64` | Ubuntu/glibc, not Arch, Debian or musl |
| `ubuntu-24.04-arm` | `linux-arm64` | Native Arm64 Ubuntu/glibc, not musl |
| `macos-15-intel` | `osx-x64` | Native Intel macOS |
| `macos-15` | `osx-arm64` | Native Arm64 macOS |
| `windows-2025` | `win-x64` | Windows Server, not Windows 10/11 |

Each native lane restores locked CLI-owner dependencies, runs only
`Configurator.Cli.Tests` with a nonempty-test guard, publishes the actual
self-contained CLI package through `PublishConfigurator`, then executes that
package's doctor, root-help, read-only validation and protected-create machine
entrypoints through the native validator below. The standalone Linux package
gate runs the same validator and its self-tests. Source-owner tests are not
evidence for an actual packaged executable.
Existing pinned checkout,
setup-dotnet and upload-artifact Actions are reused, with no new dependency,
Action, container image, publication credential or dispatch.

### Actual CLI package contract validation

Run from the repository root with SDK 10.0.302 and the native package for the
current host. This tool does not publish or regenerate shared contracts:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run .ci/scripts/validate-cli-package.cs -- --self-test
dotnet run .ci/scripts/validate-cli-package.cs -- --package-directory artifacts/configurator/cli/linux-x64 --expected-rid linux-x64
```

Use the matching package directory and RID on each configured runner. The
expected RID must equal `RuntimeInformation.RuntimeIdentifier`; cross-published
files do not become native evidence. The tool records executable, package
contract and packaged/current command-schema SHA-256 identities, checks schema
byte freshness, and executes the packaged `doctor --machine`, `--help --machine`
and `artifact validate --machine` entrypoints. Validation uses a temporary
synthetic definition/answer workspace, verifies its inputs remain unchanged and
requires no output file. No owner-source execution substitutes for these calls.

Each receipt must be one LF-terminated UTF-8 object, without BOM, CR, terminal
controls, stderr or more than 65,536 bytes. The checkout-owned command 0.0.1 schema
enforces required fields, closed nested objects, duplicate-key rejection, types,
enum/constant vocabulary, string/array/numeric bounds and patterns. Additional
checks bind invocation, process/receipt exit, status/completion, diagnostics
accounting, root help commands and ordered artifact bytes/digests to the expected
operation. Restricted artifact installation/verification facts remain separate
from target acceptance (`unassessed`) and result delivery (`attempt`).
The comparison cohort executes `artifact compare`, checks independent assessment
facts and follows all 3,000 retained synthetic findings through `nextOffset` to
the terminal page without changing the input. A stopped inventory has no exact
total. The verifier also checks returned-count progress, byte budgets and
value-free finding coordinates. Self-tests exercise hostile continuation and
assessment envelopes independently of product serialization. Regenerate packages
before qualification: schema refresh distributes only the current schema set,
without compatibility artifacts.

Linux executes protected create and an existing-file conflict, verifies exact
bytes and owner-only mode, and proves the conflict did not overwrite the file.
macOS/Windows must execute create and return I/O exit 74 with a blocked,
not-established, not-attempted receipt and no artifact. This is an executed
unsupported-operation refusal, not a successful protected save. A missing native
package, stale schema, malformed receipt or failed command fails qualification.
All independent cohorts still run after an individual cohort fails.

`CLI_PACKAGE_COHORT` lines report each outcome; `CLI_PACKAGE_RESULT` and the tool
exit report aggregate success/failure. Reports contain bounded facts, not raw
subprocess output or workspace paths. Parser self-tests cover accepted and
hostile envelopes but do not qualify any package or host. The architecture
package test invokes the same tool for the current host through
`ReleaseTestPaths.NativeCliPackage`; `CONFIGURATOR_PACKAGE_ROOT` can select the
package root. Installed maintainer checks also select that native RID and
exercise unsupported create refusal rather than returning silently.

Linux-only CLI source cohorts (PTY stdin, shell redirection, POSIX signals and
native installed-prefix faults) explicitly report `unexecuted` on other hosts.
Their absence is not a pass for those scenarios. Cross-host source tests still
exercise protected-create refusal; actual packaged refusal remains its separate
gate. Do not remove or skip required host-neutral checks to obtain a green lane.

Workflow configuration, local package execution and remote runner results remain
separate evidence. The final local Linux CLI package passed all five validator
cohorts after the CLI authoring changes; the final architecture/package suite
is recorded separately in [Operations](OPERATIONS.md#cross-platform-quality-program-qualification-2026-10-27).
An older local package can establish only its printed identity and observed
cohort outcomes; a stale failure is not repaired by relabelling it current or by
running source tests. Republish through the release owner and rerun the tool
against the final inputs before making a final package claim.

The five lanes are **configured, awaiting actual-host results**. No CI run was
dispatched as part of this increment. Musl x64/Arm64 execution, separate
Arch/Debian execution, and Windows 10/11 execution remain pending. Packaging
admission is not proof that every workflow works on a host; Windows Server cannot
qualify Windows clients, and a glibc result cannot qualify musl. No wider
Terminal/Desktop native matrix or cross-platform protected-save claim is added.

The [public operating-system boundaries](../public/packaging.md#2-verify-your-host-environment)
record .NET 10 OS/lifecycle and libc eligibility, Windows 7 exclusion and native
dependency limits. The Linux writer additionally requires kernel/filesystem
anonymous-file support, procfs descriptor linking, atomic create-only publication
and verified owner-only permissions. libc or kernel version alone proves none
of those filesystem guarantees. Unsupported hosts/filesystems reject; macOS and
Windows protected writers remain unimplemented and fail closed.

### Platform fact provenance

Official documentation was consulted read-only on 2026-10-16:

- [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners):
  exact standard runner labels and native architectures used above.
- [.NET 10 supported OS matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md):
  current OS release/edition lifecycle constraints and x64/Arm64 libc floors
  (glibc 2.27, musl 1.2.3).
- [Microsoft Linux installation guidance](https://learn.microsoft.com/en-us/dotnet/core/install/linux):
  distribution-specific native dependency guidance.

Only functional platform facts and required interoperability identifiers inform
this increment. No external implementation, snippets, prose, images or assets
are copied into the deliverable; no dependency decision changes. AFC/SSO review:
runner spellings and platform floors are constrained facts; surface admission,
tests and CI structure are independently derived from existing Configurator
owners. The design deliberately reuses the surface RID list as the admission
authority instead of introducing a second host allowlist. Raw research remains
outside the repository; this source register retains only identities and facts.

## Detached support qualification receipts

`eng/configurator/ValidateSupportQualification.cs` admits bounded assertions
about an exact payload; it does not run the claimed scenario or authenticate
its producer. Supply independently retained product-source, build-recipe and
payload SHA-256 identities, rather than trusting the receipt's own claims:

```bash
dotnet run --file eng/configurator/ValidateSupportQualification.cs -- \
  --receipt "$RECEIPT" --source "$SOURCE_SHA256" \
  --build "$BUILD_SHA256" --payload "$PAYLOAD_SHA256"
```

The receipt records full commit identity, optional dirty-input digest and tool
version. Its records distinguish client, package RID, actual runner RID,
host identity, capability, English/Arabic locale, scenario and UTC observation
time. Native target admission comes from the generated release capabilities.
Executed native receipts require matching runner/target RIDs; a glibc process
does not establish musl execution. Browser records use the static WASM target
and a separately identified native runner host.

Statuses remain `passed`, `failed`, `verified-unsupported` and `unexecuted`.
A passed protected save requires a Linux host and observed artifact creation.
Verified unsupported macOS/Windows save requires observed refusal and no
artifact. Unexecuted records cannot contain execution identities, counts or
artifacts. Missing, unknown or duplicate fields, duplicate claims, mismatched
identities and contradictory outcome/count/exit combinations reject.

Input is limited to 1 MiB, JSON depth eight and 512 records. Exit 0 means
structural/semantic acceptance, including accepted failed or unexecuted
evidence; it does not mean every scenario passed. Exit 1 emits only
`SUPPORT_QUALIFICATION_REJECTED`; malformed operands return 64. Status counts
remain separate in successful output.

Run the tool with `--self-test`, `--schema-write` or `--schema-check` from the
repository root to exercise synthetic specifications, generate
`schemas/configurator-support-qualification.schema.json`, or reject schema
drift. The native validator enforces cross-field and identity constraints beyond
the structural schema. Keep receipts outside the payload and retain independent
expected digests; neither unsigned integrity nor parser acceptance activates
support, signing, host, participant or accessibility claims.

`--projection --source SHA256 --build SHA256 --payload SHA256` exposes the
same validated receipt records together with the current admitted
client/RID/capability/locale/scenario vocabulary. Add one `--receipt FILE`
operand per receipt, or supply none to inspect missing coverage. Duplicate
coverage across receipts rejects; no second parser or RID registry determines
acceptance.

Render detached evidence with:

```bash
dotnet run --file eng/configurator/RenderSupportMatrix.cs -- \
  --source "$SOURCE_SHA256" --build "$BUILD_SHA256" \
  --payload "$PAYLOAD_SHA256" --receipt "$RECEIPT"
```

The renderer calls that native projection and sorts rows deterministically.
Every admitted scenario gets a row; absent evidence stays `missing`, and
`failed`, `verified-unsupported` and `unexecuted` retain their distinct meanings.
A passed row does not qualify sibling scenarios, locales or hosts. Receipt
digests bind original bytes and each observed row retains its record. Keep
this matrix outside the candidate payload so later qualification does not
change the bytes it describes. `--self-test` tests rendering with synthetic
projections; it does not execute support scenarios.

## Detached package provenance

`eng/configurator/VerifyPackageProvenance.cs` checks local integrity from
retained capture sessions, exact payload bytes and detached evidence. Its
success does not authenticate a publisher, activate trust or establish that
the capture owner observed every consuming task. Capture qualification has its
own actual-graph and consumption-boundary checks.

Rejected factory declarations retain diagnostic fingerprints separately from
admission. These include the expanded assembly-reference digest, a logical
assembly alias and file digest when safely observable, and construction-API
markup digests for the task body and parameter group. Fragment fingerprints
are bounded to 16 KiB; assembly hashing is bounded to 64 MiB and rejects linked
paths, including linked ancestors. Absent, oversized and unavailable observations
remain explicit. An exact top-level type definition can also be observed from
PE metadata in the same bounded byte buffer whose digest is recorded. This
does not inspect method bodies, instantiate a factory or establish interface,
loader-selection or execution semantics. Assembly-name resolution remains
unavailable: diagnostics
neither load factories nor expose raw references, physical paths or body source.
Declared parameter metadata can separately retain up to 128 names, types and
required/output flags, bounded to 16 KiB of identifier/type text. Unsupported,
absent and over-budget groups remain explicit without partial lists being
presented as complete. These declarations are not observed runtime CLR types
or proof that every execution input was reported.
These observations do not identify the selected declaration, prove factory
initialization or setter semantics, or authorize sealing a rejected capture.
Private refused-task parameter records also project the metadata names on each
received `ITaskItem`, without reading or retaining metadata values in that
projection. Each item retains at most 128 names of at most 256 characters,
ordered ordinally; count and size limits have explicit availability states.
Non-item values have no item-metadata observation. The existing record-size and
record-count limits still apply. This describes only the native event
representation: it does not establish that the engine reported every final
task metadata field, expose an independent child process, or admit the task.
Public task-shape discovery records each inspected frozen assembly as a tool
input before loading its metadata, including the native framework/engine and
dependencies resolved during inspection. The optional pinned ILLink task
assembly comes from `microsoft.net.illink.tasks/10.0.10`, selected through the
existing generated lock. Its public shapes join the same native-generated
inventory; no reflected type is added to task or task-host admission by that
observation.

`ReadWasmPropsCaptureFactory` supplies one separate, inert Linux adapter for
the SDK 10.0.302 JSON reader. `ReadWasmPropsProfile` owns the exact SDK recipe,
assembly and parameter-group fingerprints, absent original task body, runtime
type and six-parameter shape: required input `JsonFilePath` and five optional
item-array outputs. The host requires exactly the pinned original declaration
and its generated first-party project and toolset bindings; competing
declarations still reject.
Other SDK tasks retain their native bindings and other factories remain refused.

The toolset binding keeps the reader inert when a child removes the capture
imports. A project-local explicit override can defeat that binding, so the host
also admits reader declarations directly when XML enters its project
collection. This early check uses fixed recipe identity rather than inferred
child globals. A shared factory or variable namespace prefix alone does not
identify the reader; an unrelated fixed task-name leaf retains its separate
admission path. Refusal unloads the rejected XML root before throwing and
invalidates capture: otherwise a parent using `ContinueOnError` can reuse that
cached root without another admission callback. Native experiments separately
cover ordinary dispatch, explicit overrides, imported declarations, repeated
requests and preloaded admitted XML. Effective project/parameter admission and
the task-context receipt remain required after this early boundary.

The adapter retains parameters without activating the SDK reader. Its native
task-context request must receive exactly one synchronous token/context/thread
acknowledgement before loading the reader, initializing its factory, creating
its task or assigning delegated properties. The logger records frozen JSON,
recipe, reader and tool/runtime identities, including host dependencies such
as ICU. The adapter preserves unset output defaults and reports a bound
outcome after execution and cleanup; replayed, missing, duplicate or incomplete
receipts cannot seal. This is an explicit finite semantic assumption about
the pinned reader: it consumes the JSON input, treats output item values as
data rather than additional file reads, and starts no independent processes.
Dependency byte binding is not a purity claim or universal read interception.
Actual complete publication capture remains a separate acceptance requirement;
this adapter does not admit the remaining refused consumers or subprocesses.

Use task-owned, separate payload, capture, evidence and manifest locations.
The manifest and qualification receipts stay outside the published payload.
An independently retained source/build/payload identity is required:

```bash
dotnet run --file eng/configurator/VerifyPackageProvenance.cs -- \
  --verify --package "$PACKAGE" --manifest "$MANIFEST" \
  --capture "$PUBLICATION_SESSION" \
  --source "$SOURCE_SHA256" --build "$BUILD_SHA256" \
  --payload "$PAYLOAD_SHA256" --payload-prefix "$CAPTURED_PAYLOAD_PREFIX" \
  --evidence "$EVIDENCE_DIRECTORY" --evidence-digest "$EVIDENCE_SHA256" \
  --checks "$CHECK_BINDINGS" --required-checks "$REQUIRED_OWNERS" \
  --client "$CLIENT" --package-rid "$PACKAGE_RID" \
  --expected-digest "$PROVENANCE_SHA256"
```

`CAPTURED_PAYLOAD_PREFIX` identifies the generated publication directory in the
capture, beginning with `generated/repo/` and ending with `/`. The package
inventory must match every captured output beneath that prefix. The native
capture executable must already be built in its Release output and its private
sessions retained; public capture JSON alone is insufficient.

`REQUIRED_OWNERS` is an ordinal-sorted, duplicate-free list such as
`Configurator.Artifacts,Configurator.Cli,Configurator.Core`, or `none` when no
owner check is being asserted. The caller's release policy supplies that
requirement independently; it must not copy the manifest's claimed coverage.
The private check-binding array names each owner, its retained test session,
and execution/report paths relative to the evidence directory. Physical session
paths never enter the public manifest. Native controlled-check verification
binds actual executable/dependency identities, the safe report hash, scope and
outcome. Focused, failed, skipped or unbound checks cannot satisfy a required
full owner. Test/publication graphs compare their relevant product-source
projections rather than requiring identical complete graphs.

### Candidate-bound repository namespace

Capture records require `repositoryNamespace`: the sorted, unique eligible
`repo/` aliases frozen before evaluation and build. Verification compares that
set with the private session and actual frozen repository. Missing fields
reject; old records are not silently upgraded.

Standalone local integrity verification can omit candidate context. Candidate
verification additionally invokes the fixed capture tool with
`--verify SESSION --repository-names FILE` for publication and all ten required
test sessions before accepting native package proof. `FILE` is a private,
strict UTF-8 JSON array containing the exact candidate object's complete,
sorted, unique Git leaf names as canonical `repo/` aliases. It is bounded to
16 MiB, 100,000 names and 4,096 UTF-16 characters per alias. The capture owner
validates all aliases before applying its existing eligibility and exclusion
policy; the candidate verifier does not maintain a second classifier.

After native verification, the verified eligible namespace determines which
Git entries must have regular-file modes. Full captured repository inputs,
including test sources outside the product projection, also bind their lengths
and digests to the exact candidate's raw blobs. Name completeness, consumed
bytes and actual check execution are separate requirements. Synthetic attacks
prove rejection behavior, not completeness of an actual publication capture.
The authenticated verifier profile and its independently authorized promotion
remain G-TRUST requirements; this local mechanism activates no signer or key.

### Detached identity chain and local limits

The acyclic chain is verified source/build identities, sorted payload inventory,
typed detached evidence inventory, detached manifest, then an external expected
manifest digest. Inventories contain relative paths, lengths and lowercase
SHA-256; compact declaration-order camelCase UTF-8 has no BOM or trailing
newline. No manifest field contains its own digest, expected digest or
signature. Support receipts retain failed and unexecuted outcomes rather than
turning structural acceptance into qualification.

Replace `--verify` with `--write` and omit `--expected-digest` to create a new
manifest after validation; existing files are not overwritten. Successful
commands print `PACKAGE_PROVENANCE_VALID` or `PACKAGE_PROVENANCE_WRITTEN` with
the detached digest. Rejection returns 1 with `PACKAGE_PROVENANCE_REJECTED`.
`--self-test`, `--schema-write` and `--schema-check` exercise synthetic
specifications and own `schemas/configurator-package-provenance.schema.json`.

This local implementation runs on Linux and rejects unsupported hosts. Static
filesystem admission rejects links, hard links and special entries. Keep inputs
quiescent and task-owned; this is not a hostile-host isolation or filesystem
authenticity guarantee. Limits are 10,000 inventory entries, depth 32,
512 MiB per file, 4 GiB per inventory and 16 MiB per JSON document, with tighter
receipt/report bounds retained. Copied-evidence consistency and synthetic
tamper tests do not supply actual-host, participant, signer, reproducibility
or SLSA claims.

## Generated contracts and package verification

`GenerateConfiguratorRatchets.cs` uses compiled Core definition schema and CLI
command metadata. It evaluates `WriteConfiguratorReleaseContract` for each
native client to derive the native matrix rather than maintaining a second
matrix. The three generated JSON files record browser custody capabilities,
schema digests and native release facts. They contain no Event backend schema,
import session or production trust. Regenerate and check from the repository root:

```bash
dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --write
dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --check
```

Browser capabilities distinguish opt-in `secretEntry` and deliberate
`privateExport` from always-false `persistence`/`answerPersistence`.
`publicAssetCache` and `explicitPublicDefinitionCache` permit public retention
only. `nativeProtectedSave`, target authentication/apply and telemetry remain
false. The generator is the sole source; do not hand-edit capability JSON.

The architecture project is registered in `Configurator.slnx` alongside the
seven product owners. It builds their assemblies and inspects compiled
assembly metadata, not source text. It rejects Event/test dependencies, enforces
Core's narrow closure and Browser's Presentation/Core product boundary, exercises neutral
artifact preparation and validates evaluated release refusal behavior.
Presentation itself must reference Core only. Actual browser `_framework`
package inspection requires Presentation/Core WASM and excludes native UI,
Artifacts, Event and test assemblies. Package tests also require PWA assets and
check generated capability flags; they do not inspect source/CSS text.

The current package tests require the three Linux x64 packages and Browser
package from the public commands first, plus a second browser publication with
explicit store inclusion. Set `CONFIGURATOR_PACKAGE_ROOT` for the store-free root
and `CONFIGURATOR_STORE_PACKAGE_ROOT` for the store-enabled root; the latter
defaults to `artifacts/configurator-with-store`. `BrowserStorePackageTests` runs
independently of native package tests and compares each indexed revision's
family, application, application versions and digest against Core readmission,
then verifies the published bytes against their SDK worker asset hashes.
The CI lane publishes store-enabled content first, then republishes that same
destination without inclusion before validating the store-free package. This
also exercises stale intermediate/output removal.

For a browser-only verification with explicit roots:

```bash
CONFIGURATOR_PACKAGE_ROOT="$PWD/.tmp/browser-without-store" CONFIGURATOR_STORE_PACKAGE_ROOT="$PWD/.tmp/browser-with-store" dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release -- --treenode-filter '/*/*/*BrowserStorePackageTests/*' --minimum-expected-tests 2
```

Full package and engineering checks:

```bash
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release -- --minimum-expected-tests 1
dotnet run .ci/scripts/validate-action-pins.cs -- .github/workflows
dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .
dotnet run .ci/scripts/write-artifact-checksums.cs -- artifacts/configurator artifacts/configurator/checksums.sha256
```

The dependency gate reads actual runtime/tool/probe locks and restored NuGet metadata.
Test-only runners and coverage tools are excluded because they are not distribution
inputs; compiled and packaged closure tests separately reject test dependencies.
Unknown metadata or license expressions reject. Exact version-and-nuspec-hash
approvals in `.ci/policy/dependency-licenses.json` represent reviewed metadata,
not a blanket licensing override. Copyleft and unreviewed source-available terms
are not admitted. An optional second argument to the dependency gate selects
the restored package-cache directory explicitly; its default is `NUGET_PACKAGES`
or the normal user cache. Architecture tests use an isolated metadata cache
without changing the SDK compiler's restore environment. Preserve component
notices with every distribution.

The workflow runs on fork pull requests with `contents: read`, no retained
checkout credentials and pinned Actions. It assembles unsigned validation
artifacts only. It has no deployment/signing credentials or trust activation.
The workstream exit checks include all seven product suites, engineering suites,
retained optional Event fixture conformance and package closure. The Browser
package embeds the same neutral definition file as the native minimal example;
the distributed raw bytes must remain public safe. Package creation alone does
not prove target acceptance or activate publication trust.

## Native Arch development assembly

`eng/configurator/package-desktop-arch.sh` owns local unsigned x86_64 Arch
packages. It invokes the existing `PublishConfigurator` owner with Desktop,
Linux x64 and an empty temporary output root; callers cannot supply a stale or
arbitrary payload. It checks the evaluated package contract, required metadata
and absence of symlinks, validates the desktop entry, and retains every published
file. The first-party Browser SVG is reused for the desktop icon. No new asset
license or shipped build tool is introduced.

`packaging/linux/arch/PKGBUILD.in` installs the closure under
`/usr/lib/islamu-configurator-desktop`, a relative symlink under `/usr/bin`, the
standard desktop entry, hicolor icon and MIT license. Native `makepkg` verifies
the staging archive's SHA-256 and generates package metadata, BUILDINFO, MTREE
and SRCINFO. `!strip` and `!debug` prevent rewriting the appended .NET bundle.
Native libraries remain distro dependencies; `xorg-xwayland` is required for
the same X11 backend to run under a Wayland compositor. No installer scripts,
user-directory writes, target apply, answer migration or root build is involved.

Version admission accepts only `0.0.0.dev` plus a fourteen-digit UTC timestamp.
The default timestamp identifies a local development build; this route does not
admit stable releases or bypass promoted-release/signing authority. It is separate
from future qualified installer assembly and its readiness evidence gate.
See the [operator commands](../public/packaging.md#3-arch-linux-pacman-package).

The package-specific native integration check takes two successive development
archives and verifies real pacman metadata, declared host dependencies, standalone
closure, launcher integration, installation, upgrade, complete installed payload
equivalence and removal. An owner-only synthetic operator file must survive
upgrade/removal byte-for-byte with mode `0600`. Bubblewrap gives pacman root
inside a user namespace, makes the host read-only and disconnects the network;
all mutations occur under its temporary root. `--nodeps --nodeps` is used only for that
empty test root, after actual host dependencies pass `pacman -T`. No host package
database is changed. Run in a Wayland session with XWayland enabled on Arch,
with `bubblewrap`, `strace` and `xorg-server-xvfb` installed:

```bash
bash eng/configurator/test-desktop-arch-package.sh /absolute/older.pkg.tar.zst /absolute/newer.pkg.tar.zst
```

The same check launches the installed executable on the host's Wayland/XWayland
display and on a dedicated Xvfb X11 display with the .NET SDK hidden and network
disabled. Each must connect to its display and remain running for the bounded
eight-second smoke check; timeout terminates it, without claiming graceful
closure. Package checks and bounded launch checks do not establish assistive-technology support,
all workflow scenarios or full release qualification.
