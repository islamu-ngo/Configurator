# Configurator packaging ownership

`eng/configurator/Configurator.Release.proj` owns native surface/RID admission,
standalone publication and independent static browser publication. Its default
`Build` target restores locked development dependencies and builds the selected
native client. `PublishConfigurator` requires an explicit RID, embeds native
dependencies, disables trimming, renames the apphost to the documented operator
name and includes notices, schemas and generated contract data.
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
package from the public commands first. Set `CONFIGURATOR_PACKAGE_ROOT` for a
nondefault output root:

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
