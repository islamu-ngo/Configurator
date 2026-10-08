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
Serve the entire browser `wwwroot/` tree from a static server with appropriate
WASM MIME types and route fallback to `index.html`; there is no application
server, private input, authentication, target apply or persistence. Serving is
an operator action, not performed by the build.

`ConfiguratorOutputRoot` selects another package destination. Do not publish into
a directory containing unrelated files. The CI artifact is unsigned validation
output, not a promoted release. Signed publication remains disabled pending a
separately established Configurator trust authority.

The audited Terminal.Gui feed is repository-owned; do not replace it with an
upstream package or change approved hashes. See the
[internal packaging anchor](../internal/PACKAGING.md) for verification.
