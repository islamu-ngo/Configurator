# Browser cryptography probe

This test-only Blazor WebAssembly application invokes the real shared Core
engine with synthetic inputs. It is separate from product publication and has
no production diagnostic endpoint. It displays public profile names, fixed
result codes, elapsed milliseconds and managed-heap observations only.
Generated values, plaintext inputs and exceptions never enter its UI or logs.

Publish and serve from repository root after the implementation's Core tests:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
export DOTNET_PROCESSOR_COUNT=2
dotnet publish eng/configurator/probes/cryptography/Configurator.DependencyProbes.Cryptography.csproj --configuration Release -p:RunAOTCompilation=true -o "$TMPDIR/configurator-secret-probe"
dotnet run --file eng/configurator/ServePublishedBrowser.cs -- "$TMPDIR/configurator-secret-probe/wwwroot" 5187
```

Open the served page in an actual browser, select **Run synthetic profiles**,
and retain the completion count, safe rows, browser/runtime identity and network
observations. Every expected profile must pass; a zero or partial run fails
qualification. Capture the publish artifact identity separately. The SHA256
profile additionally compares the published `abc` test vector.

Observe requests during execution to verify no secret traffic. Startup fetches
the static WASM/framework assets. This app has no HttpClient, service worker,
clipboard or persistent browser storage. Heap measurements are before/after
observations, not peak memory or total browser memory; latency on one browser
does not qualify all devices. Passing this probe does not qualify trimming or
controls in the separately published product Browser.
