# Browser launch, custody and source contract

## Owners and dependency closure

Browser is static Blazor WebAssembly over browser-safe Presentation/Core.
It has no native Artifacts, Event, target API, authentication or apply closure.

`Configurator.Browser.csproj` selects the Blazor WebAssembly SDK and `Program.cs`
boots `WebAssemblyHostBuilder`. Standalone WASM is already interactive; there is
no per-component `InteractiveWebAssembly` directive or server-rendered workspace.
This is deliberate: editing, validation and composition stay client-side, with
no answer-bearing connection to a server. The larger initial download and
client resource use are accepted for stable local custody and offline operation.
Server/Auto hosting would change that boundary, even with secret consent or no
durable server storage. See the [hosting decision and alternatives](adr/ADR-browser-session-trust-and-export.md#hosting-alternatives-and-trade-offs)
and [public trust guidance](../public/browser.md#choose-a-client-and-trust-level).

`BrowserDefinitionRegistry` admits embedded definitions with Core's codec and
registry. Public distribution includes complete definition metadata, never
answers or private defaults. The neutral definition is shared with native
examples; Event remains optional generated data, not a runtime dependency.

`BrowserLaunchSelection` resolves the closed bundled route. Publisher `/open`
uses `BrowserDefinitionPointer` and `BrowserDefinitionSource` to fetch one
bounded public JSON definition before answer collection. The
[public launch guide](../public/launch-integration.md) owns operator syntax and
limits; the implementation owns parsing/admission. Family/revision is bound to
canonical digest and conflicting admitted identities remain rejected even
after removal. `BrowserLaunchSession` retains its selected document: registry
changes cannot upgrade a running workspace.

## Definition source boundary

Optional `DefinitionStoreCatalog` discovery is browser-owned public metadata,
loaded only on Store pages without an active workspace. Packaged same-origin
snapshots use a separate bounded asset reader; Core admission, exact indexed
identity/digest checks and `BrowserDefinitionRegistry` history still gate entry.
No store index is required by startup, independent sources, Core or native
clients. Store metadata cannot authenticate publishers or infer application
compatibility beyond declared versions. See [store ownership](DEFINITION_STORE.md).

The worker inventories optional store URLs separately from required app assets.
Only required assets determine installation and readiness. Explicit Store reads
may cache the exact SDK-hashed public asset before answer custody; unavailable
or corrupt store content cannot disable an otherwise ready local workspace.
Offline uncached revisions fail as unavailable discovery, with local-file entry
still usable. No private answers or output enter this optional read/cache path.

Bundled query selectors are closed, unique and bounded. Remote `/open` metadata
is fragment-only: `src` plus optional digest/version/entry/locale, never answers.
Remote URLs admit public credential-free HTTPS without query, userinfo,
fragment or redirect. `BrowserDefinitionPointer.ValidateSource` admits public
hostnames and IP literals that pass its explicit address filter. Local/private
literal addresses, single-label names and local suffixes reject; browser DNS
remains outside the utility's authority. This filter is not DNS-resolution or
server-side network-isolation proof.
Fetch omits credentials/referrer, uses no-store, has a 10-second timeout and
streams at most 4 MiB regardless of Content-Length. Admission, digest and
selector checks precede workspace publication. Failures expose stable codes,
not source URLs, raw exceptions or values. Generation/cancellation fencing
rejects late loads. No remote source fetch is allowed during live answers or
offline operation.

Local JSON/YAML upload selects syntax from the file extension and delegates to
`SetupDefinitionSourceCodec` before opening an answer workspace. Closed content
discriminators select compact authoring compilation or expanded runtime
admission; remote retrieval continues to use `SetupDefinitionCodec.ParseJson`
and rejects authoring sources. Local admission shares the 4 MiB byte budget,
owns and clears its input buffer, replaces the file control and restores focus.
No source comments are retained in canonical definitions, and local compilation
introduces no includes, schema-reference resolver or network authority. It is the fallback
for CORS/offline/source failures and starts a fresh no-secret workspace. There
is no proxy, target login or executable definition loader.

## Transient answer and output boundary

`BrowserNavigationState` retains only current-editor callbacks, never answers.
`WorkspaceNavigationGuard` uses Blazor `NavigationLock` and a native browser
confirmation. Cancel preserves the editor. Confirm invalidates pending work
synchronously, awaits editor discard and releases custody before route loading.
Generation checks reject late confirmations aimed at replacement editors;
old-editor disposal cannot release a newer owner's lease. Group navigation
stays within the same mounted editor and does not pass through this route guard.

`ConfigurationEditor` uses `DefinitionFormModel` snapshots and Core validation
rather than a second browser evaluator. New workspaces use
`DefinitionSessionPrivacy.BrowserNoSecrets`; explicit consent enables secret
leaves. Nested imports/defaults/row edits follow that same shared policy.
Revocation removes secret leaves recursively, increments the answer revision
and invalidates output; clear, edits, navigation and disposal fence old work.
Typed fields and object-list rows follow admitted activation, validation and
effective sensitivity. Display modes affect disclosure only.
The shared form owns typed presence separately from display text. Explicit empty
strings, admitted empty enum choices and empty containers survive import and
preparation; omission uses a distinct editing action. Invalid or rejected edit
intent invalidates output/import authority even if accepted JSON is unchanged.
Refresh and untouched navigation do not count as edits.

Consent denial and recursive secret clearing happen before the first cleanup
await. The editor detaches and synchronously invalidates every old artifact's
workspace and export owner before awaiting any retirement. Cleanup touches only
captured old resources, preserving public edits and replacement output through
late completion or failure. Unexpected cleanup failure is observed as a stable
value-free diagnostic; expected JS denial cannot restore authority.

Strict `DefinitionArtifactComposer` still requires complete valid answers.
`DefinitionDraftArtifactComposer` offers distinct secret-deferred preparation
with completion locations and Restricted output. Ordinary missing/invalid
values and unresolved secret-dependent conditions fail; draft mode never
changes strict readiness or fabricates secrets. See the
[Core contract](CORE_DEFINITION_CONTRACT.md) for owning APIs and bounds.

`BrowserArtifactExport` owns current bytes. Reveal/copy/download are explicit
actions; the product JS module owns only current transient export handles and
short-lived Blob URLs. Invalidation clears bytes and revokes capabilities;
generation checks prevent late interop from reopening invalidated output.
Clipboard denial remains value-free with download available. No output is a
private data URL, navigation payload, cache entry or implicit download.
Browser export transfers custody without native owner-only/create-only proof.
The download attribute is a filename hint: actual published Chrome testing
normalized `.env` to `env.txt`. Operator guidance requires checking/renaming the
saved name to the target's declared filename; the adapter cannot guarantee
browser/OS filename preservation.

## PWA and hosting boundary

`BrowserOfflinePolicy` mediates pre-answer asset preparation, explicit admitted
definition retention, offline state and workspace lifecycle. Published workers
cache public SDK-manifest app assets and selected public definitions only.
Answers/output never enter worker messages, storage or requests. Offline fetch
is cache-only; active answer work blocks source loading. An incomplete cache
does not imply offline readiness. Definition retention is separately bounded to
eight canonical documents and re-admits/checks each digest on read. Updates wait for clean lifecycle across tabs
and cannot reload an active workspace. Browser-managed worker update checks
remain a platform limitation; physical disconnection provides stronger silence.

Serve the entire published `wwwroot` via a secure context, with SPA fallback
for `/launch/*` and `/open`, correct WASM MIME types and asset failures distinct
from HTML navigation. Base path, worker scope and manifest must agree. CSP
allows reviewed WASM boot/same-origin assets and the bounded HTTPS source path;
it is not target authentication or a network-isolation certificate. Packaging
and capabilities are generator-owned; see [Packaging](PACKAGING.md).

## Verification ownership

Browser tests use real Core admission, registries, shared forms and output
contracts; fake only genuine HTTP/JS boundaries. They cover malicious sources,
consent/nested import and stale output, while bUnit checks rendered semantics.
Compiled/package tests verify Presentation/Core closure and exclude native,
Event and test dependencies. Separate published-browser QA must inspect
requests, stores, caches, worker updates, keyboard/mobile/zoom and downloads.
Unit/package passes cannot substitute for actual browser platform evidence.
Executed evidence and remaining limitations live in
[Operations](OPERATIONS.md#browser-custody-and-offline-verification).
