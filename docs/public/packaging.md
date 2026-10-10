# Standalone packages

Configurator prepares local drafts. It does not apply them to a target or grant
administrator authority. The utility needs no Event checkout or running service.

Install .NET SDK 10.0.302 to build packages. Run these commands from Configurator:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build eng/configurator/Configurator.Release.proj --configuration Release --verbosity quiet
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=cli -p:ConfiguratorRid=linux-x64
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=terminal -p:ConfiguratorRid=linux-x64
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=desktop -p:ConfiguratorRid=linux-x64
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser
```

Native outputs are self-contained, untrimmed single-file executables. Copy the
whole package directory, including MIT ownership and third-party notices:

| Surface | Default executable or static root | Packaging RIDs |
| --- | --- | --- |
| CLI | `artifacts/configurator/cli/<rid>/islamu-configurator` | `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64` |
| Terminal | `artifacts/configurator/terminal/<rid>/islamu-configurator-terminal` | Same as CLI |
| Desktop | `artifacts/configurator/desktop/linux-x64/islamu-configurator-desktop` | `linux-x64` only |
| Browser | `artifacts/configurator/browser/wwwroot/` | Independent static WASM, no native RID |

Windows executable names end in `.exe`. Packaging a RID is not a claim that
protected exports work on that host. Protected native writes are Linux-only and
still reject filesystems where create-only, owner-only protection cannot be
established. macOS and Windows exports fail closed; Desktop packaging refuses
unverified hosts. GUI packages still need the host's display and platform libraries.

Native package directories include `schemas/`, `contracts/`, `dependency-notices/`,
`LICENSE` and `THIRD-PARTY-NOTICES.md`. The dependency directory retains original
package metadata and license/notice files; keep it with the executable.
Browser has the same public metadata under `wwwroot/`.
The CLI package also includes `skills/configurator-integrate/SKILL.md` and its
routed resources. Copy that complete skill directory into an Agent Skills-aware
tool's supported skill location; no agent framework or installer is required.
The included authoring schema and native `definition` commands supply the actual
contract. The workflow never needs private answer discovery or target startup.
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

`ConfiguratorOutputRoot` selects another package destination. Do not publish into
a directory containing unrelated files. The CI artifact is unsigned validation
output, not a promoted release. Signed publication remains disabled pending a
separately established Configurator trust authority.

The browser package is store-free by default and builds even when the `store/`
source directory is absent. Home, Open and Help remain available; local files and
public publisher links use the same configuration workflow. The Store page
explains when no catalogue is packaged.

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

The audited Terminal.Gui feed is repository-owned; do not replace it with an
upstream package or change approved hashes. See the
[internal packaging anchor](../internal/PACKAGING.md) for verification.

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
