# Standalone Packages & Building from Source

ISLAMU Configurator is distributed as self-contained native executables and static web assets. It runs completely offline without background services or database dependencies.

You do not need to install the .NET SDK to run standalone packages—only to build Configurator from source.

---

## Quick Install & Binary Matrix

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
- Detailed hosting guidance, Content Security Policy (CSP) headers, and Docker container instructions are documented in the [Browser Guide](browser.md#hosting).

---

## Where to Go Next

- **Ready to run?** Follow the [Getting Started](getting-started.md) walkthrough.
- **Using the CLI?** See the full command reference in the [CLI Guide](cli.md).
- **Understanding file security?** Read about [Native Output Protections](native-output.md).
