# Configurator packaging ownership

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
owner/probe restores are locked. The downstream Terminal.Gui verifier independently
checks the approved package, assembly, patch and dependency hashes.

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

The current release project admits CLI and Terminal packages for `linux-x64`,
`linux-arm64`, `osx-arm64` and `win-x64`; Desktop is limited to `linux-x64`.
CI publishes and checks the three Linux x64 packages and static Browser only.
The four-RID native host/surface matrix is **Planned**: cross-publication does
not establish that a package runs or saves restricted output on that host.
Windows/macOS protected save and Linux arm64 host execution require actual-host
evidence before those capabilities are claimed.

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
