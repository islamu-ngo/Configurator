# Standalone Packages & Building from Source

ISLAMU Configurator is distributed as self-contained native executables and static web assets. It runs completely offline without background services or database dependencies.

You do not need to install the .NET SDK to run standalone packages—only to build Configurator from source.

---

## Definition contracts and qualification

The product and public contracts stay at `0.0.1` during development, including
breaking changes. Runtime definitions use `bounded-v0.0.1`; command, answer and
Store formats also have one current implementation. Only the current unversioned
schema filenames are packaged. Older local schema copies belong in ignored
`schemas/archive/`; no old envelope reader or migration adapter is retained.
Use `nextOffset`/`returnedCount`,
not the requested page size. Comparison completion is separate from compatibility,
workspace preparation and target acceptance; see the [CLI guide](cli.md).
The optional Store uses fresh families when content identities change; older identities remain historical
Git evidence and are not silently migrated or repinned.

Earlier Linux x64 qualification includes real native create-only writes, X11 controls,
a Terminal process and published Browser desktop/mobile flows. Browser downloads
do not inherit native restricted-save guarantees. Package, rendering and schema
checks do not establish another host's support, publisher authentication, signer
activation or target acceptance. Exact evidence and reproduction commands belong
to the [operations record](../internal/OPERATIONS.md#definition-v2-qualification).
That historical evidence does not qualify the new secret-generation payload.
The [current 0.0.1 baseline record](../internal/OPERATIONS.md#fixed-development-baseline-001-2026-11-02)
qualifies the Linux x64 CLI, Terminal and Desktop candidate and the published
Browser AOT flows, including consent revocation, generated-password custody and
the optional Store. It records package identities and passing checks, including
the repaired Artifacts fixtures. The separate cryptography probe's
measurements are bounded by [cryptography limits](../internal/SECRET_CRYPTOGRAPHY.md).

## Quick Install & Binary Matrix

The matrix below declares package targets. Actual scenario evidence is separate:
the [detached support receipt procedure](../internal/PACKAGING.md#detached-support-qualification-receipts)
binds observations to exact source, recipe and payload identities and renders
each admitted client, target, language and scenario. Missing, failed,
unsupported and unexecuted rows remain explicit; one passing scenario does
not qualify the remaining rows.

Maintainers can also verify [detached package provenance](../internal/PACKAGING.md#detached-package-provenance)
against independently retained identities. A matching unsigned digest proves
local consistency under that procedure; publisher authenticity and actual
host qualification require their separate evidence and authority.

Rejected task-factory declarations can retain bounded diagnostic fingerprints
without admitting the factory. Missing observations remain explicit. These
diagnostics do not prove which declaration executed or permit a rejected
capture to become package provenance.
Refused-task item diagnostics can also retain bounded metadata field names
without their values. They describe the received build event, not a complete
task-input contract; truncated or oversized observations remain explicit.
Task-shape inventories bind the frozen SDK and selected package assemblies
they inspect as consumed tool inputs, including dependencies resolved during
that inspection. An inventoried shape still does not admit the task.

One Linux capture adapter has an explicit contract for the SDK 10.0.302
`ReadWasmProps` JSON reader. It binds the exact declaration, reader bytes,
JSON input and tool/runtime dependencies before delegated SDK activation.
The controlled binding also applies to child projects; early declaration
admission rejects competing definitions and removes rejected XML from the
native cache before a parent can retry it.
This relies on the pinned reader consuming its JSON input without reading
paths contained in its output items or starting independent processes.
It does not admit other task factories or establish arbitrary file-read
mediation. A complete publication capture must still pass every other
consumer boundary before provenance can be sealed.

| Surface | Target Platform / Architecture | Runtime Identifier (RID) | Package Executable / Output Root |
|---|---|---|---|
| **[CLI](cli.md)** | Linux (glibc) x64 / Arm64<br/>Linux (musl) x64 / Arm64<br/>macOS Intel / Apple Silicon<br/>Windows 10/11 x64 | `linux-x64`<br/>`linux-arm64`<br/>`linux-musl-x64`<br/>`linux-musl-arm64`<br/>`osx-x64`, `osx-arm64`<br/>`win-x64` | `artifacts/configurator/cli/<rid>/islamu-configurator`<br/>*(Windows: `.exe`)* |
| **[Terminal](terminal.md)** | Linux x64 / Arm64<br/>macOS Apple Silicon<br/>Windows x64 | `linux-x64`<br/>`linux-arm64`<br/>`osx-arm64`<br/>`win-x64` | `artifacts/configurator/terminal/<rid>/islamu-configurator-terminal`<br/>*(Windows: `.exe`)* |
| **[Desktop](desktop.md)** | Linux x64 (Avalonia GUI) | `linux-x64` | `artifacts/configurator/desktop/linux-x64/islamu-configurator-desktop` |
| **[Browser](browser.md)** | Any modern web browser | Static WASM | `artifacts/configurator/browser/wwwroot/` |

---

## Platform Limitations & Alerts

> [!CAUTION]
> **Windows 7 is strictly unsupported.** Windows CLI and Terminal binaries require 64-bit Windows 10, Windows 11, or Windows Server 2016 or newer.

> [!IMPORTANT]
> **Linux-Only Protected Saves:** The atomic, create-only protected file writer operates only on Linux. While the CLI and Terminal run on macOS and Windows, protected saves on those platforms fail closed to prevent accidental insecure file creation.

> [!WARNING]
> **glibc vs. musl Packages:** Linux packages compiled for glibc (`linux-x64`) and musl (`linux-musl-x64`, such as Alpine Linux) are **not interchangeable**. Ensure you download the correct RID for your Linux distribution.

---

## Using Standalone Packages

Standalone packages are pre-compiled and self-contained. They bundle the required .NET runtime components, so no system-wide runtime installation is needed.

### 1. Extract and Preserve Directory Structure

When extracting a package, keep the entire package folder intact. Each release includes essential validation schemas, license notices, and skill definitions:

```text
islamu-configurator/
├── islamu-configurator        # Executable binary
├── schemas/                   # Public JSON validation schemas
├── contracts/                 # Machine API contracts
├── skills/                    # Coding agent integration skill
├── dependency-notices/        # Third-party attribution
└── LICENSE                    # Project license
```

> [!TIP]
> Do not move the executable out of its folder without copying the `schemas/` directory, as the CLI references local schemas for validation.

For the Browser, preserve the entire `wwwroot/`, including its local editor
bundle, component manifest and original notices under `vendor/`. Do not copy
only `index.html` or replace the editor with a runtime CDN. Ordinary .NET source
builds verify the checked assets; regenerating them uses the exact toolchain and
native recipe in the [editor build guide](../../eng/configurator/editor/README.md).
The npm/Node/Bun build tools are not part of the Browser runtime.

### 2. Verify Your Host Environment

Run the built-in diagnostic tool to verify host compatibility and filesystem features:

```bash
# Linux or macOS
./islamu-configurator doctor --machine

# Windows PowerShell
.\islamu-configurator.exe doctor --machine
```

- **Exit code `0`:** Host is fully compatible, including protected save capabilities (Linux).
- **Exit code `4`:** Basic execution is supported, but protected native file saving is unavailable (expected on macOS and Windows).

### 3. Arch Linux Pacman Package

Arch Linux users can build and install a native pacman package for the Desktop interface:

```bash
sudo pacman -S --needed base-devel jq desktop-file-utils
bash eng/configurator/package-desktop-arch.sh

# Install the generated package
sudo pacman -U artifacts/configurator/packages/arch/islamu-configurator-desktop-*.pkg.tar.zst
```

The application will be registered as **ISLAMU Configurator** in your desktop application menu. To uninstall cleanly:

```bash
sudo pacman -R islamu-configurator-desktop
```

---

## Building from Source

To compile ISLAMU Configurator yourself, install the [.NET SDK 10.0.302](https://dotnet.microsoft.com/download/dotnet/10.0).

### 1. Prepare Your Environment

Set your temporary directory and ensure workloads are up to date:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
mkdir -p "$TMPDIR"

dotnet workload update
dotnet build eng/configurator/Configurator.Release.proj --configuration Release --verbosity quiet
```

### 2. Compile Specific Interfaces

Publish only the surfaces and architectures you need:

```bash
# Compile the Machine CLI (replace linux-x64 with your target RID)
dotnet msbuild eng/configurator/Configurator.Release.proj \
  -t:PublishConfigurator \
  -p:ConfiguratorSurface=cli \
  -p:ConfiguratorRid=linux-x64

# Compile the Terminal TUI
dotnet msbuild eng/configurator/Configurator.Release.proj \
  -t:PublishConfigurator \
  -p:ConfiguratorSurface=terminal \
  -p:ConfiguratorRid=linux-x64

# Compile the Desktop GUI (Linux x64 only)
dotnet msbuild eng/configurator/Configurator.Release.proj \
  -t:PublishConfigurator \
  -p:ConfiguratorSurface=desktop \
  -p:ConfiguratorRid=linux-x64

# Compile the Static Browser Package (WASM)
dotnet msbuild eng/configurator/Configurator.Release.proj \
  -t:PublishConfiguratorBrowser
```

Compiled binaries will appear under the corresponding paths in `artifacts/configurator/`.

Release packaging rejects enabled import-comparison measurement probes before
changing output. Contributors producing diagnostic candidates must use a separate
output directory and the [measurement build procedure](../internal/IMPORT_COMPARISON.md#instrumentation-ownership).

---

## Bundling the Public Definition Store

By default, the browser package compiles without a bundled definition store. To package the reviewed public store catalog into the static web assets:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj \
  -t:PublishConfiguratorBrowser \
  -p:ConfiguratorOutputRoot="$PWD/artifacts/configurator-with-store" \
  -p:DefinitionStoreSourceRoot="$PWD/store"
```

The build process validates every definition revision in `store/` and generates static `index.json` manifests under `wwwroot/definition-store/`.

---

## Hosting the Browser Package

The browser output in `artifacts/configurator/browser/wwwroot/` consists entirely of static HTML, CSS, JavaScript, and WebAssembly files.

- It can be served by any static file server (Nginx, Caddy, Apache, GitHub Pages, Cloudflare Pages, AWS S3).
- No backend API, application server, or database is required.
- Detailed hosting guidance, Content Security Policy (CSP) headers, and Docker container instructions are documented in the [Browser Guide](browser.md).

Container builds use the Dockerfile-specific context filter. It includes the
first-party capture project's source needed by ratchet verification, while
excluding local capture sessions and build outputs. The final image contains
the static Host and published Browser assets.

The optional .NET Host denies framing (`frame-ancestors 'none'`) and accepts
only GET and HEAD. It serves application assets named in the published SDK
`service-worker-assets.js` inventory, the worker and its inventory, root license
and notices, and the fixed public schemas and capability contracts added by
packaging. Adding a file to `wwwroot` does not publish it over HTTP. Files with
unknown MIME types are refused, including inventory entries with unknown types.
The extensionless root `LICENSE` is explicitly served as plain text.

Navigation fallback is limited to `/`, `/open`, `/help`, `/store/{slug}` and
`/launch/{id}` (one extensionless segment). Missing assets and unknown routes
return 404; other methods return 405. Public responses require revalidation.
Optional Store assets retain their manifest membership; a Store-free package
does not gain Store content. Dependency notice trees remain in the distributed
package but are not exposed by this Host. Compressed sidecars remain packaging
outputs; the Host compresses admitted canonical assets during transport.

---

## Where to Go Next

- **Ready to run?** Follow the [Getting Started](getting-started.md) walkthrough.
- **Using the CLI?** See the full command reference in the [CLI Guide](cli.md).
- **Understanding file security?** Read about [Native Output Protections](native-output.md).
