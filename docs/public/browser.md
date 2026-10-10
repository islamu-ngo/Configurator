# Browser configuration

Use the static Blazor WebAssembly app to prepare configuration files locally.
Choose a bundled definition, open a publisher's pinned definition link, or
import a local JSON or YAML definition from your machine. The browser can collect
ordinary settings and tenant rows without installing a native client. It does
not connect to the target application, authenticate you, deploy services or
grant administration authority.

## Choose a client and trust level

For sensitive configuration, treat the hosted web app as the least safe option
among Configurator's interfaces. Its convenience adds trust in the hosting
origin, delivered code, browser and extensions, and its exports cannot enforce
native file protection. Prefer a reviewed local [Desktop](desktop.md),
[TUI](terminal.md) or [CLI](cli.md) build on a trusted machine for high-value
secrets. Native clients still require trusted software and an operating system;
protected saving is verified on Linux only and unsupported hosts refuse it.
This is a recommendation about exposure, not a measured universal security
ranking or a guarantee that any client is immune to compromise.

In the browser, choose between leaving secrets disabled and completing a draft
locally, or explicitly consenting to local secret entry for this workspace.
Either choice can also be used offline after preparing the app and definition.
Ordinary settings may disclose private deployment details even without secrets.
The current browser app has no server-processing mode.

Local processing reduces exposure to server-side collection and compromise; it
does not prove that hosted code is trustworthy. A compromised origin, dependency
or extension could observe input or change behavior. Physically disconnecting
prevents live network transmission during work, but does not establish that the
downloaded software is benign. Clearing work cannot retract exported data or
guarantee forensic erasure. No official release or public hosted deployment is
currently claimed.

## Why WebAssembly interactivity

ISLAMU Configurator deliberately uses standalone Blazor WebAssembly. Forms,
validation and file preparation execute in your browser, keeping answers and
generated files outside application network requests. Static hosting means the
host distributes app files; it does not mean a noninteractive UI. MudBlazor
supports WebAssembly interactivity and does not require Interactive Server.

The trade-off is a larger initial app download and computation using your
device's resources. Once loaded, local interactions need no server round trip,
and a prepared app and retained definition can support offline editing and
export. Interactive Server can start with a smaller download and use server
resources, but bound form values are transmitted to a server-held workspace;
editing depends on the connection, its latency and server availability.
Interactive Auto may use Server initially and WebAssembly on subsequent visits.
That variability does not fit Configurator's consistent local answer boundary.

WebAssembly is therefore a data-custody and offline choice, not a claim that it
always loads faster or is automatically secure. Trusting a provider to deliver
software and trusting its infrastructure to receive secrets are different
decisions. See [Microsoft's hosting comparison](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0)
and [MudBlazor's supported hosting setup](https://mudblazor.com/getting-started/installation).

## Use

Home offers bundled examples and independent file or link entry. The optional
[Store](definition-store.md) lets you search public projects and deliberately
select a definition revision; application versions are separate declared
compatibility metadata. Missing store content does not prevent opening files or
publisher links. Reviewed inclusion does not authenticate a publisher.

Open configuration separates local-file and public-URL entry and shows the
supported format beside each control. In a workspace, choose the field view
and secret-entry preference above the guided sections. **Prepare complete
configuration** validates all required settings; **Prepare draft** defers
missing secrets for local completion. Optional artifact import has a separate
section below preparation. Help and source links remain available in the footer.

The appearance controls offer system, light, dark and high-contrast palettes,
and automatic, left-to-right or right-to-left direction. Only these bounded
preferences are remembered locally. Answers, consent and generated files are
never stored as appearance preferences.

The workspace shows the admitted family, revision and SHA-256 definition digest.
It retains that exact definition for the session. A different launch or definition
starts a fresh workspace; registry changes cannot silently upgrade your running
workspace. Leaving an editor through application navigation asks you to discard
answers and output first. Cancelling keeps editing; confirming clears the old
workspace before the destination can load. Closing or reloading uses the
browser's native confirmation where supported.

A publisher source or local-file launch first shows the admitted metadata.
Local `.json`, `.yaml` and `.yml` files may contain compact maintainer sources
or expanded runtime definitions, with a maximum size of 4 MiB. Compact sources
are compiled locally before answers are collected. Source comments are not
retained; deliberate offline retention stores only the admitted canonical
public definition. Public links still require compiled runtime JSON.
Review it, optionally confirm the definition is safe for public offline
retention, then open the workspace. That confirmation concerns definition
metadata only; it neither retains answers nor enables secret entry.

Complete active public and sensitive nonsecret fields using the typed controls.
Guided, Advanced and All Declared views change disclosure, not validation.
Required fields remain required; active invalid fields remain reachable in
Guided view even when normally disclosed under Advanced. For an object whose
declared properties may be empty, explicitly supply the object when the target
requires its presence; leaving it absent and supplying `{}` are different
answers. Object lists use declared row properties and
bounded add/remove controls; removed rows are excluded from the next output.
For Event, tenant names must be unique and the definition permits at most 256
tenants; Core's aggregate budgets still apply.

Section buttons show field and correction counts. Switching sections keeps
your answers and unfinished input. Review lists active corrections, including
fields in other sections; selecting a correction opens that section and focuses
the field or affected collection row. Review also offers secret-entry recovery
when a required secret inside a supplied object needs consent. Authored labels
remain unchanged, and field details expose the exact configuration key.

Secret entry is **off for every new workspace**. You have two choices:

- Leave secret entry disabled and prepare a **secret-deferred draft**. Complete
  ordinary required fields first. The result identifies secret completion
  locations so you can finish the downloaded files locally. It is incomplete
  and may fail the target's validation until completed.
- Explicitly enable secret entry for this workspace after deciding that you
  trust this origin, its shipped code and your browser environment. Secret
  controls are masked. Complete preparation validates all required answers.

Revoking consent clears secret leaves, including those nested in objects or
rows, and invalidates prepared output while retaining ordinary settings.
Consent is never remembered for another workspace. Imports cannot bypass this
policy; a secret-bearing import is rejected without consent. Malformed values,
invalid nonsecret fields and unresolved secret-dependent conditions do not
become valid merely because draft preparation was selected.

Existing artifact import is limited to 1 MiB and the definition's supported
unconditional top-level copy mappings. Transformed, conditional or repeated
projections do not have a supported inverse; for example, Event's nested
manifest cannot be universally imported. Use the typed controls for those
definitions. Unsupported import is distinct from invalid file contents.

For the [neutral sample](../../examples/minimal-self-hosted/setup-definition.json),
extended deployment mode activates the metrics choice. Supply the service name
and ordinary settings, then defer the required credential or voluntarily enter
it with consent. The bundled definition contains no credential answer.

## Copy and download

Prepare the definition's active artifacts using complete or draft preparation.
Inactive outputs are withheld. Ordinary validation or aggregate-limit failure
clears the prepared set; no active artifacts means no successful output.
Output is
redacted until you deliberately reveal it. **Copy** invokes the clipboard only
on your action; **Download** creates a short-lived Blob download for the current
artifact. Clipboard failures leave download available and do not silently send
data elsewhere. Edits, clear, consent revocation, definition replacement and
disposal invalidate output and revoke download capabilities.

Every answer-derived file is **restricted**, even if its source fields are
labelled public. A browser download cannot guarantee `0600` permissions,
create-only writes or prevention of browser/OS download history. Check the
destination and protect downloaded files yourself. Native clients remain an
alternative when you need the
[protected native save contract](../internal/PROTECTED_ARTIFACTS.md).

Check the actual downloaded filename against the declared name before target
use. Browsers may normalize names: published Chrome testing downloaded the
declared `.env` artifact as `env.txt`, despite the explicit download hint.
Rename that file to `.env` if that is the target's required name; do not assume
the browser preserves a leading dot or extension.

All answers and prepared output are transient application state. They are
excluded from application storage, public caches, worker messages and network
requests. Closing/clearing work clears the application's owned state; browser
extensions, managed runtimes, clipboard and user-controlled downloads have
their own lifetimes. This is not a forensic-erasure guarantee.

## Offline work

The published PWA can prepare its public app assets and explicitly retain an
admitted public definition before answer collection. Only retain definitions
whose metadata is safe to distribute publicly; never upload credentials,
private defaults or confidential explanatory text as definition data.
Reopening a selected definition offline requires both a complete app-asset cache
and that retained definition, not merely a registered worker or an installed
shortcut. Retention is bounded to eight definitions, each within Core's 4 MiB
limit, and each is re-admitted and checked against its canonical digest on read.

After preparing the app and definition online, you can disconnect and reopen
the retained workspace definition, then edit, validate, compose and export
locally. Answers are not retained: reopening starts a new empty workspace.
Offline operation and live answer work block application remote-definition
fetches. Starting another definition deliberately ends the current workspace.
Worker updates wait rather than reload or replace a live workspace; close
active work in all tabs before taking an update.

Browser-managed worker update checks and installation of public app assets can
still generate traffic while an older workspace remains pinned. These requests
carry no answers. The page's remote-definition guard does not control all worker,
extension or hosting-origin traffic, so physical disconnection is the
stronger guarantee. Published-platform validation must inspect actual requests,
caches, update behavior and downloads; unit/package tests alone do not prove
those browser behaviors. See the
[operating evidence](../internal/OPERATIONS.md#browser-custody-and-offline-verification).

## Hosting

Build the standalone static package with the repository SDK:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj -target:PublishConfiguratorBrowser -property:Configuration=Release
```

Serve the complete `artifacts/configurator/browser/wwwroot/` tree using HTTPS
(or localhost for local testing). Keep `_framework`, worker, manifest and other
asset requests distinct from navigation; missing assets must not return HTML.
Configure SPA fallback to `index.html` for `/launch/*`, `/open` and application
navigations. The shipped base path `/` assumes origin-root hosting. For a
subpath, align the base, worker scope, manifest and fallback directory.

Publish the worker, SDK-generated asset manifest, icons, notices, schemas and
capability metadata together. Use correct WASM/JSON/JavaScript MIME types and
avoid long immutable caching of the worker itself. Review the shipped CSP with
your hosting headers: WASM boot, same-origin assets/worker and credential-free
HTTPS definition reads must work without broadening target authority.
No application server, answer persistence, authentication or target API is
required. Building a package does not deploy a public origin.

See [launch integration](launch-integration.md) for pinned links, source limits,
CORS requirements and local-file alternatives, and
[packaging](packaging.md) for distribution boundaries.
