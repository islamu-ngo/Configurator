# Public browser

The Blazor WebAssembly client prepares a **partial public choice template** in
memory. It does not prepare full native configuration, enter secrets, connect to
an application, authenticate an operator, or apply configuration to a target.
Use the CLI, terminal or desktop client with the same definition to finish
native-only answers and prepare restricted configuration artifacts.

## Use

Open the static site's root and choose a bundled definition. The neutral
minimal self-hosted entry appears first. It uses the same checked definition as
the [native sample](../../examples/minimal-self-hosted/setup-definition.json).
The catalogue link includes its exact digest. The workspace displays the
selected revision and SHA-256 definition digest. It retains that admitted
document for the session
even if the registry changes; navigation to another launch starts a new session
and clears choices, uploads and generated previews.

Only active fields declared **public enum or boolean** have answer controls.
Public free-text and other public question metadata can be reviewed, but their
answers belong in native clients. Sensitive and secret questions have no entry
control or public preview. Activation follows Core's field states. Deactivating
a public choice removes its previous selection. Native-only required answers
are counted separately; a public template never means full configuration is
ready.

For the neutral sample, select **extended** deployment mode to make **Enable
metrics** available. The required service credential must be entered in a
native client; the Browser cannot complete native readiness. The definition
embedded in the Browser contains no credential answer.

Select public choices, then choose **Prepare public template** and download the
JSON. Importing a public template restores only supported choices for the exact
pinned family, revision and digest. Import is at most **64 KiB**; extra or
duplicate properties, unknown/private fields, inactive selections, arbitrary
text and unsupported values reject without preview or download. Rejection
clears an earlier preview. **Clear choices** removes all selections and output.

The closed envelope is:

```json
{
  "schemaVersion": "configurator-public-template/v1",
  "familyId": "69cdb122-41d2-4fab-9b82-7929a889c726",
  "revision": 1,
  "definitionDigest": "<the displayed 64-character lowercase SHA-256>",
  "selections": {
    "service.mode": "extended",
    "metrics.enabled": true
  }
}
```

Copy the actual displayed digest; the placeholder above is not a valid import.
Selections may be partial or empty. Enum values must match a declared choice
exactly; booleans are JSON `true`/`false`, not strings. The envelope has exactly
these five properties. It is a browser-specific public transfer format, not a
Core composed artifact and not an application configuration manifest.

No input is sent to a target API. No cookies, application persistence,
telemetry, service worker, remote definition download, native filesystem access
or secret entry is added. Downloads use a transient data URL and an explicit
browser download action. Uploaded byte buffers are zeroed on invalidation and
completion, and file controls are replaced after processing. The browser and
user-controlled downloads still have their own lifecycle; this is not a
guarantee of forensic erasure.

## Hosting

Build the static client with the repository SDK:

```bash
dotnet publish src/Configurator.Browser/Configurator.Browser.csproj --configuration Release
```

Serve its published `wwwroot` using an ordinary static host. Keep `_framework`
and other asset requests distinct from navigation. Configure a SPA fallback to
`index.html` for `/launch/*` and other application navigations, not missing
framework assets. The shipped `<base href="/">` assumes origin-root hosting;
for a subpath, set the base to that directory with a trailing slash and serve
fallback navigation within it.

Initial boot requires the static site and framework assets. Definitions are
embedded in the application assembly, not fetched from arbitrary URLs. Work
after boot is local; disconnected relaunch is **not** promised because there
is no service worker or offline installation cache. No deployed public origin
is claimed.

See [launch integration](launch-integration.md) for bounded selectors and
application-owned deep links.
