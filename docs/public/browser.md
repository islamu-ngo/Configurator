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
protected saving has actual-host evidence on Linux x64 only; Arm/musl host
qualification remains pending, and macOS/Windows protected saves fail closed.
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

Release packages use ahead-of-time (AOT) compilation to reduce local interaction
cost. This increases the initial download and public offline-cache footprint.
Development builds remain interpreted.

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
If a new workspace starts while an earlier discard is finishing, the earlier
navigation is cancelled rather than clearing or leaving the new workspace.
Navigate again to confirm discarding the workspace that is now current.

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
The definition's authored experiences and optional-editor expansion change
disclosure, not answers or validation. Without authored journeys, use Configure.
Changing the experience keeps keyboard focus on its selector; use the section
or workflow controls to move to the next task.
Required work and active errors remain reachable; corrected editors stay open
until you deliberately collapse or navigate. Optional secrets do not open
merely because they are secret, and disclosure never grants secret consent.
For an object whose
declared properties may be empty, explicitly supply the object when the target
requires its presence; leaving it absent and supplying `{}` are different
answers. Object lists use declared row properties and
bounded add/remove controls; removed rows are excluded from the next output.
An explicitly supplied empty string, an admitted empty enum choice, `false`,
zero, `{}` and `[]` are values, not omitted answers. Use the field's explicit
omission action in **Actions for...** to remove an explicit answer without
clearing its siblings. Opening that menu does not change answers. Escape closes
it and returns focus to its Actions button. Boolean choices distinguish Not supplied, Yes and No;
an admitted empty enum string has its own Empty string choice.
An admitted definition's default still applies when an answer is absent.
An untouched blank control or abandoned new list item supplies nothing.
Changing an answer, even to invalid text or the same value, discards old output
and prevents a pending import from replacing that edit. Changing sections or
refreshing the view alone does not change answers.
For Event, tenant names must be unique and the definition permits at most 256
tenants; Core's aggregate budgets still apply.

Configure, Review and Prepare move between real sections of this workspace;
they do not apply configuration. Required-answer progress counts active required
work, not scrolling or deployment. A definition without required answers shows
that fact instead of a percentage; invalid optional input still needs correction.

Section buttons show required answers ready and work needing attention.
Only the selected section mounts its editors. Switching sections keeps
your answers, unfinished input and repeated-row identity. Large sections load
their controls in small batches, with a loading message while settings remain
to be mounted. Visible controls stay available, and search or correction can
bring its requested setting into view before unrelated controls finish loading.
Loading progress does not determine answer readiness or enable secret entry.
Use local **Show optional
settings** controls inside a structured field, or choose **Detail in this profile**:
Follow experience, Essential, Standard or All active. **Density** selects
Comfortable or Compact spacing for this workspace only. Neither selector changes
answers, consent, unfinished numeric text or current prepared files.
Collapsing does not delete supplied values or hide an active
error. Short help stays near the question; **Help with...** opens the author's
local instructions and deliberate public documentation links. The help dialog
also exposes the definition path and output bindings without showing answers.
Close or Escape returns focus to its help button without expanding the form.

Use **Find a setting** in the application bar to find exact definition or output keys,
question titles, concepts and authored guidance. All search words must match;
private answers and prepared contents are never searched. **Filter settings**
offers sections, requirement, applicability, answer state, sensitivity and
declared outputs. Options within a group combine with OR; groups combine with
AND. Counts ignore only their own group's selections, so alternative options
stay understandable. Selected filters can be removed individually.
Ctrl-K or Command-K opens search; on a phone, **Search settings** opens the same
workspace search. Escape or **Close** dismisses results and returns focus to the
connected control that opened keyboard search. Pointer-opened search returns
to its query on desktop or **Search settings** on a phone.
On short viewports, including high browser zoom, the application bar scrolls
with the document so it cannot cover your question. Keyboard search still opens
within the viewport. On a phone the query replaces the search button without
changing the application bar's height or shifting the workspace.
Clicking outside dismisses results without moving your destination focus.
The closed result panel does not mount its filters or results. Reopening keeps
the current query and filters and uses current answers for readiness. Results
are shown 50 at a time; counts and ranking describe the complete selection.
Changing the query, filters or experience returns to the first page.

**Clear search** keeps filters; **Clear filters** keeps the query; **Reset search
and filters** clears both. No-match recovery offers these same deliberate
choices. Inactive settings are excluded by default. Including them explains
their applicability without enabling anything; **Review activation** opens an
active controlling question. A result's **Configure** action opens its exact
question or routes a denied secret to consent. Selecting a result closes the
panel and focuses the destination while preserving the query and filters.
Searching, filtering and navigation preserve answers,
unfinished input, consent and prepared files; none is saved in links or storage.

Review lists active corrections, including
fields in other sections; selecting a correction opens that section and focuses
the field or affected collection row, scrolling it into view. Review also offers secret-entry recovery
when a required secret inside a supplied object needs consent. Authored labels
remain unchanged, and field details expose the exact configuration key.
Opening Review shows active required work, corrections, deferred secrets and
supplied or defaulted settings, including collapsed optional settings. Use the
separate inspection controls to include omitted optional or inactive settings;
inspection does not enable them. Effects are shown 50 at a time, while correction
actions remain independently reachable. Values stay masked; provenance, readiness and
declared output bindings are separate facts. **Change** opens the exact active
question, and **Return to review** returns directly without resetting answers.
Output filenames and declared apply/restart consequences do not prove that
the target will accept the files.

Secret entry is **off for every new workspace**. You have two choices:

- Leave secret entry disabled and prepare a **secret-deferred draft**. Complete
  ordinary required fields first. The result identifies secret completion
  locations so you can finish the downloaded files locally. It is incomplete
  and may fail the target's validation until completed.
- Explicitly enable secret entry for this workspace after deciding that you
  trust this origin, its shipped code and your browser environment. Secret
  controls are masked. Complete preparation validates all required answers.

Denied secrets have no entry control until you consent, even after showing
optional settings. Password fields declare whether you are creating a new
password or supplying an existing one, with stable identities for optional
password-manager matching. Your manager may offer generation or fill; support
varies, so ordinary paste remains available. Issuer tokens must come from their
issuer; use paste or an explicit custom-field mapping rather than substituting a
generated password. Configurator does not add a login form or read your vault.
Your manager retains independent custody: clearing this tab does not clear
its saved entries, and clearing its vault does not clear this workspace.

Revoking consent clears secret leaves, including those nested in objects or
rows, and invalidates prepared output while retaining ordinary settings.
Denial takes effect before asynchronous export cleanup starts. Ordinary edits
made while cleanup is pending remain intact; all old artifact capabilities are
retired together, and a late cleanup cannot erase newly prepared output.
Cleanup failures are reported without answer values and cannot restore consent.
Consent is never remembered for another workspace. Imports cannot bypass this
policy; a secret-bearing import is rejected without consent. Malformed values,
invalid nonsecret fields and unresolved secret-dependent conditions do not
become valid merely because draft preparation was selected.

Existing artifact import is limited to 1 MiB and the definition's supported
unconditional top-level copy mappings. Eligible imports appear before manual
questions under **Start from an existing configuration**. Import replaces only
untouched authored defaults. Conflicting manual answers reject the whole file;
unmentioned answers, origins and invalid drafts remain intact. An equal-value
import still records imported provenance. Transformed, conditional or repeated
projections do not have a supported inverse; for example, Event's nested
manifest cannot be universally imported. Use the typed controls for those
definitions. Unsupported import is distinct from invalid file contents.
When rejection identifies a declared field, the import panel offers **Review**
for that setting. It opens the exact field or its active controlling question;
secret-consent rejection instead focuses the workspace consent control without
enabling it. Your answers remain unchanged. Correct the source file or make a
deliberate workspace edit, then choose the file again. An edit clears stale
import recovery; unknown file keys are not treated as trusted field identities.

For the [neutral sample](../../examples/minimal-self-hosted/setup-definition.json),
**Recommended** follows authored sections; **Custom** starts with advanced
disclosure. Neither changes answers. Extended deployment mode activates metrics
and optional nested operations settings. Review the service name and ordinary
settings, then defer the required local service password or voluntarily enter
it with consent. The optional issuer token has no invented provider or issuance
endpoint. The bundled definition contains no credential answer.

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

Before disconnecting:

1. Load and review the public definition without opening its answer workspace.
2. Confirm **I confirm this definition is safe to retain as public metadata**,
   then select **Retain definition for offline use** and wait for confirmation.
3. Make sure the app's public assets are available offline as well. Retaining a
   definition alone does not complete the app-asset cache.

After disconnecting, go to **Open configuration**, choose the entry under
**Retained public definitions**, then open its workspace. You can edit, validate,
compose and export locally. Answers are not retained: reopening starts a new
empty workspace.
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

Build the standalone static package with the repository-pinned SDK and workload
set. Install the WebAssembly build tools once; Linux builders also need Python 3
available on `PATH`:

```bash
dotnet workload install wasm-tools
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

### Container image

The Browser project includes a deployment-neutral multi-stage `Dockerfile`.
A small ASP.NET Core host serves the published files; the application still
runs as client-side WebAssembly. No external web-server configuration file
is required or copied from the repository.
The image listens on internal port 8080; your deployment platform owns domains,
TLS, reverse proxying, host-port publication and Docker networks.

For local testing, build from the repository root, not the project directory:

```bash
docker build -f src/Configurator.Browser/Dockerfile -t islamu-configurator-browser .
docker run --rm --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m \
  --cap-drop ALL --security-opt no-new-privileges \
  -p 127.0.0.1:8080:8080 islamu-configurator-browser
```

Open `http://localhost:8080`. The image builds the complete static package
through the existing release target and serves it with a non-root .NET process on
port 8080. The host processes no configuration answers. Root-filesystem
writes are unnecessary; the temporary filesystem holds server process data,
not configuration answers.

The default image has no Store catalogue. To deliberately package the local
public `store/` tree, add `--build-arg INCLUDE_DEFINITION_STORE=true` to the build
command. Only `true` and `false` are accepted. This packages the selected Store
content; it does not update definitions or make their identities compatible
with a different bundled revision. Retained revisions must not be rewritten.
Local files, bundled examples and publisher links do not require the Store.

The Dockerfile-specific build-context allowlist excludes repository history,
private workspaces, environment files and host build output. The final stage
copies only the published static tree and hosting configuration. Keep the
included notices with the image.

For a public deployment, configure HTTPS and the trusted origin in your
deployment platform. The local `-p` example above is not a required production
port mapping. Origin-root hosting is the supported image layout; mounting it
under a URL subpath needs the base/worker/manifest changes described above.
The static server distinguishes application navigation from missing assets,
revalidates update files and disables request-access logging. It adds no
authentication, target transport or private-data persistence.
Container isolation does not protect answers from compromised delivered code,
the browser or extensions, and does not turn downloads into protected saves.

#### Coolify

Use the Git-based **Dockerfile** build pack with these settings:

| Setting | Value |
| --- | --- |
| Base Directory | `/` |
| Dockerfile Location | `/src/Configurator.Browser/Dockerfile` |
| Docker Build Stage Target | Leave empty to use the final runtime stage |
| Ports Exposes | `8080` |
| Ports Mappings | Leave empty for domain/proxy access |
| Domain and HTTPS | Configure in Coolify |

Keep the build context at the repository root: shared projects, central
configuration and release tooling live outside the Browser directory.
Coolify manages the reverse proxy, certificates and container networks.
No Coolify-specific labels, network names, public hostname, certificate files
or port bindings belong in this Dockerfile. No persistent volume or private
runtime environment variables are needed for configuration answers.

See [launch integration](launch-integration.md) for pinned links, source limits,
CORS requirements and local-file alternatives, and
[packaging](packaging.md) for distribution boundaries.
