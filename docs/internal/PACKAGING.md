# Configurator packaging ownership

`global.json` selects both the SDK and workload-set versions. Before the first
restore on a clean SDK installation, `dotnet workload update` initializes the
pinned manifests, including on native-only and public-data CI jobs. The command
runs from the repository root so the pin remains authoritative. Browser builders
add `wasm-tools`; native-only jobs do not install that workload.

`eng/configurator/Configurator.Release.proj` owns native surface/RID admission,
standalone publication and independent static browser publication. Its default
`Build` target restores locked development dependencies and builds the selected
native client. `PublishConfigurator` requires an explicit RID, embeds native
dependencies, disables trimming, renames the apphost to the documented operator
name and includes notices, schemas and generated contract data.
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
`ReleaseContractTests` seeds command v1 and a retired schema in a temporary package,
executes this actual MSBuild target for each native surface, and checks exact
current-schema names/bytes plus preservation of unrelated package files. It uses
the repository's current schema set, so retiring command v1 requires no test alias.

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
The worker caches only allowlisted public SDK-manifest app assets; explicit
definition retention is separate. No answers or output enter either cache.
Publish all assets as one static tree with correct MIME types, HTTPS/localhost
secure context and SPA fallback for `/launch/*` and `/open`. Missing asset
requests must not return `index.html`. Base path, worker scope and manifest
must agree. Worker activation waits for a clean lifecycle across tabs; a package
does not prove real browser update/network/cache behavior.

## Browser container

`src/Configurator.Browser/Dockerfile` builds from the repository root and invokes
`PublishConfiguratorBrowser`; it does not bypass ratchet checks, locked project
restores, public schemas/contracts, dependency notices or the SDK's PWA asset
inventory. The Dockerfile-specific `Dockerfile.dockerignore` admits only build
inputs and public definitions, excluding `.git`, task workspaces, environment
files and host `bin`/`obj`/artifact output.

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
[public guide](../public/browser.md#container-image) owns operator commands.

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
package's doctor and root-help machine entrypoints. Smoke assertions require
successful exits, nonempty stdout and empty stderr; they do not parse JSON with
shell text matching or claim full package-schema verification. The CLI-owner
suite covers machine JSON semantics separately. Existing pinned checkout,
setup-dotnet and upload-artifact Actions are reused, with no new dependency,
Action, container image, publication credential or dispatch.

The five lanes are **configured, awaiting actual-host results**. No CI run was
dispatched as part of this increment. Musl x64/Arm64 execution, separate
Arch/Debian execution, and Windows 10/11 execution remain pending. Packaging
admission is not proof that every workflow works on a host; Windows Server cannot
qualify Windows clients, and a glibc result cannot qualify musl. No wider
Terminal/Desktop native matrix or cross-platform protected-save claim is added.

The [public operating-system boundaries](../public/packaging.md#cli-operating-system-boundaries)
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
See the [operator commands](../public/packaging.md#arch-linux-development-package).

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
