# Standalone packages

Configurator prepares local drafts. It does not apply them to a target or grant
administrator authority. The utility needs no Event checkout or running service.

Choose the interface and platform below. If you already have a native package,
[run it directly](#run-a-native-package); the .NET SDK is only needed to
[build from source](#build-from-source). Browser packages need a
[static web host](#host-the-browser-package), not a native executable.

Native outputs are self-contained, untrimmed single-file executables:

| Surface | Default executable or static root | Packaging RIDs |
| --- | --- | --- |
| CLI | `artifacts/configurator/cli/<rid>/islamu-configurator` | `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, `osx-arm64`, `win-x64` |
| Terminal | `artifacts/configurator/terminal/<rid>/islamu-configurator-terminal` | `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64` |
| Desktop | `artifacts/configurator/desktop/linux-x64/islamu-configurator-desktop` | `linux-x64` only |
| Browser | `artifacts/configurator/browser/wwwroot/` | Independent static WASM, no native RID |

Windows executable names end in `.exe`. Packaging a RID is not a claim that
protected exports work on that host. Protected native writes are Linux-only and
still reject filesystems where create-only, owner-only protection cannot be
established. macOS and Windows exports fail closed; Desktop packaging refuses
unverified hosts. GUI packages still need the host's display and platform libraries.

## Run a native package

Copy the **whole package directory**, not just the executable. Keep its schemas,
contracts, license and dependency notices beside it. No separately installed .NET
runtime or SDK is needed, but the operating-system dependencies below still apply.

For the CLI, open a shell in the copied package directory:

```bash
# Linux or macOS
./islamu-configurator --help
./islamu-configurator doctor --machine
```

```powershell
# Windows PowerShell
.\islamu-configurator.exe --help
.\islamu-configurator.exe doctor --machine
```

Help needs no definition or answers. `doctor` is read-only: exit `0` reports the
available protected host path; exit `4` reports an unavailable protected writer,
as expected on macOS and Windows today. It does not test every destination or
qualify every workflow. Continue with the [CLI guide](cli.md) or
[synthetic first run](getting-started.md).

## CLI operating-system boundaries

The CLI packaging scope is modern Linux (including Arch, Debian and Ubuntu),
Intel/Arm64 macOS, and x64 Windows 10/11. **Windows 7 is unsupported.**
Select `linux-*` for glibc distributions and `linux-musl-*` for musl distributions;
these packages are not interchangeable. Terminal, Desktop and Browser admission
is unchanged by this CLI expansion.

Self-contained packages include .NET, not every operating-system library. Hosts
still need the native dependencies required by [.NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
As checked on 2026-10-16, its x64/Arm64 libc floors are glibc 2.27 and musl 1.2.3;
these are runtime compatibility floors, not supported-distribution guarantees.
The current upstream list includes Debian 13, Ubuntu 22.04/24.04/26.04 and
macOS 15 or newer listed releases. Arch is a rolling-distribution target, not an
upstream Microsoft support promise. Windows 10/11 eligibility depends on the
specific release/edition and its upstream lifecycle; a `win-x64` package does not
make every Windows 10 installation supported. Follow the linked upstream matrix
for current versions and required native libraries.

Linux protected output additionally needs a compatible kernel, accessible
`/proc/self/fd`, filesystem support for anonymous-file preparation and atomic
create-only linking, and verifiable owner-only permissions through a safe directory
chain. Meeting a libc floor or running a recent kernel alone is insufficient.
Unsupported filesystems, existing-file conflicts and unsafe destinations reject
without overwriting or falling back to an ordinary write. macOS/Windows still
have no protected writer and reject restricted output.

CI now declares native CLI-owner tests and actual self-contained package
`doctor --machine` / `--help --machine` smoke checks for Linux x64/Arm64,
macOS Intel/Arm64 and Windows Server x64. Inspect the passing results for the
exact commit and runner being qualified; configuring a gate is not execution
evidence, and one passing build does not qualify every operating-system variant.
Ubuntu execution does not establish Arch or Debian coverage, Windows Server is
not Windows 10/11 evidence, and glibc execution does not establish musl coverage.
Both musl hosts and Windows 10/11 remain explicit pending actual-host checks;
no emulation or unreviewed container image substitutes for them. Package smoke
checks alone do not qualify protected export or every CLI workflow.

## Package contents

Native package directories include `schemas/`, `contracts/`, `dependency-notices/`,
`LICENSE` and `THIRD-PARTY-NOTICES.md`. The dependency directory retains original
package metadata and license/notice files; keep it with the executable.
Terminal packages also retain reviewed supplemental notices for the syntax
grammars and themes embedded in their dependencies. Those assets keep their
own terms even though the Configurator utility is MIT licensed.
Native publication replaces the generated notice tree, removing notices for
retired dependencies when publishing again into the same package destination.
It also replaces only the package's owned `schemas/` directory with the current
public schemas, removing retired schema versions while preserving unrelated
package files outside that directory. Do not store your own files in `schemas/`.
Browser has the same public metadata under `wwwroot/`.
The CLI package also includes `skills/configurator-integrate/SKILL.md` and its
routed resources. Copy that complete skill directory into an Agent Skills-aware
tool's supported skill location; no agent framework or installer is required.
The included authoring schema and native `definition` commands supply the actual
contract. The workflow never needs private answer discovery or target startup.

## Host the browser package

Serve the entire browser `wwwroot/` tree from a static server with appropriate
WASM MIME types and route fallback to `index.html`; there is no application
server, authentication, target apply or answer persistence. Browser private
answers are transient and secret entry requires workspace consent. Public PWA
caches and explicit public-definition retention are separate from answer
custody. Serve the manifest, worker, SDK asset manifest, lifecycle module and
icons together under HTTPS (localhost for local tests); `/open` and `/launch/*`
need navigation fallback, while missing assets must remain errors. See
[browser hosting](browser.md#hosting) for base/scope/CSP/update requirements.
Serving is an operator action, not performed by the build.

The Browser project also supplies a non-root static-host container image.
See [container build and run](browser.md#container-image) for repository-root
Docker commands, read-only operation, explicit optional Store inclusion and
HTTPS deployment boundaries. The image invokes the same release owner rather
than introducing a second publication pipeline.

`ConfiguratorOutputRoot` selects another package destination. Do not publish into
a directory containing unrelated files. The CI artifact is unsigned validation
output, not a promoted release. Signed publication remains disabled pending a
separately established Configurator trust authority.

The browser package is store-free by default and builds even when the `store/`
source directory is absent. Home, Open and Help remain available; local files and
public publisher links use the same configuration workflow. The Store page
explains when no catalogue is packaged.

Each publication replaces the generated `<ConfiguratorOutputRoot>/browser/`
directory, so keep your own files outside that package destination. Distribute
the complete output, including its SDK-generated compressed asset sidecars.

To include the optional reviewed public catalogue, pass its source explicitly:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser -p:ConfiguratorOutputRoot="$PWD/artifacts/configurator-with-store" -p:DefinitionStoreSourceRoot="$PWD/store"
```

The builder validates contributor metadata and canonical definition revisions
before generating `wwwroot/definition-store/index.json` and public snapshots.
These files join the SDK asset inventory before publication, so their exact
bytes can be cached with the app for offline Store browsing. They contain no
answers or prepared output. Deliberately retaining a separately opened public
definition still requires its own user action. Store membership and publisher
labels do not authenticate a publisher or grant target acceptance. See
[definition store contributions](definition-store.md).

Publishing again without `DefinitionStoreSourceRoot` removes previously packaged
catalogue assets from that browser destination and its worker inventory. Keep
different output roots when retaining both variants for distribution.

Terminal.Gui is restored from NuGet.org using an exact centrally managed
version and generated dependency locks. No custom feed or upstream rebuild is
required. See the [dependency decision and history](../internal/TERMINAL_GUI_DEPENDENCY.md)
and [internal packaging anchor](../internal/PACKAGING.md) for verification.

## Build from source

Install .NET SDK **10.0.302** and run from the repository root. These examples use
Bash; each publication line is independent, so run only the surface you need.
Replace `linux-x64` with an admitted RID from the table above when appropriate.

Initialize the workload manifests selected by `global.json` even for native-only
builds. Installing the SDK alone does not install that pinned workload set.
Browser publication additionally needs `wasm-tools`, as described in the
[browser hosting guide](browser.md#hosting).

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
mkdir -p "$TMPDIR"
dotnet workload update
dotnet build eng/configurator/Configurator.Release.proj --configuration Release --verbosity quiet

# Choose one native surface:
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=cli -p:ConfiguratorRid=linux-x64
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=terminal -p:ConfiguratorRid=linux-x64
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=desktop -p:ConfiguratorRid=linux-x64

# Or build the static browser package:
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser
```

The default output directories are in the table above. Building creates packages;
it does not install them, start a server or establish release-signing authority.
The first restore may need network access for build dependencies.

## Arch Linux development package

On x86_64 Arch Linux or an Arch derivative, build a native pacman package as an
ordinary user. Install the build tools and .NET SDK version specified above:

```bash
sudo pacman -S --needed base-devel jq desktop-file-utils
bash eng/configurator/package-desktop-arch.sh
```

The command freshly publishes Desktop and prints the resulting
`artifacts/configurator/packages/arch/islamu-configurator-desktop-0.0.0.dev<UTC-timestamp>-1-x86_64.pkg.tar.zst`
path. Install that exact file, then start the application:

```bash
sudo pacman -U "/absolute/path/to/the/generated.pkg.tar.zst"
islamu-configurator-desktop
```

Replace the placeholder path with the printed path. The application also appears
as **ISLAMU Configurator** in the desktop menu. No .NET runtime is needed on the
installed host. The package declares its native libraries and XWayland dependency;
it runs in X11 sessions and in Wayland sessions through XWayland. The Wayland
compositor must enable XWayland. This package does not select Avalonia's native
Wayland backend. Optional `noto-fonts` improves Unicode coverage.

The builder accepts an explicit `0.0.0.devYYYYMMDDhhmmss` version for repeatable
identification, refuses to overwrite an existing package, and writes a SHA-256
sidecar, original payload inventory, generated PKGBUILD and native-generated
SRCINFO beside the archive.
These are unsigned local development packages, without a qualified release or
AUR publication claim. Install newer builds with `pacman -U`; remove with:

```bash
sudo pacman -R islamu-configurator-desktop
```

Removal deletes package-owned application, menu and icon files. Your selected
definitions, prepared files and output directories are not package-owned and
remain intact.
