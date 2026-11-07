# Browser launch, custody and source contract

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Implemented browser boundary; target acceptance is separate
> **Owner:** Browser
> **Last Verified:** 2026-11-01
> **Source Anchors:** `src/Configurator.Browser/`, `src/Configurator.Presentation/`

## V3 typed workspace admission and callbacks

`BrowserLaunchSelection` admits an explicit closed editing-capability set before
answers and resolves the declared localization default, not the first locale in
sorted order. Launch/Open pass that initial locale into `ConfigurationEditor`.
Resolution also retains whether `entry` was explicit. Launch/Open pass only an
explicit entry to the new form: if it matches an admitted output-selector option,
`DefinitionFormModel.SelectOutput` supplies that enum value through the ordinary
typed edit exactly once at workspace creation. A resolver's first-entry fallback
does not overwrite Core initialization, and rerendering does not reapply entry
selection over later edits or omissions. Definitions without a matching selector
continue to use their entry as metadata, not a guessed answer path.
The shared form checks the same capability set before allocating answer editors.
Admitted root maps and discriminated root variants now enter the same workspace
as ordinary property roots. `ConfigurationRoot` renders them at the empty literal
path; admission no longer rejects those placements. The renderer does not create
a wrapper answer, another workspace or additional launch authority.
The Browser capability set includes `schema.secret`. Admitted secret metadata
establishes a sensitivity floor even without an explicit sensitivity string.
Local generation uses the same per-workspace consent and edit eligibility as
manual entry. `DefinitionSecretOperation` owns revision and editor-identity
fencing; browser controls do not apply results directly. Revocation retires
temporary derivation inputs, generated originals, reveal state and pending
clipboard capabilities alongside ordinary secret answers.
It owns subsequent locale changes as view state. Public display metadata and
localized sections/journeys come from Presentation; Core remains the schema,
effective-value, predicate and branch authority.

`ConfigurationField` uses typed enum ordinals, map-value schemas and the
Core-selected variant. Provider changes delegate to the shared explicit branch
action. Null, reset, omission and suggestion adoption remain distinct commands.
Map-key drafts exist only in the component, clear on privacy changes and
disposal, and never enter transport, storage or worker messages.

Scalar native callbacks retain their existing form/path/revision/control
generation fence. Compound actions additionally capture a mount generation;
retired callbacks cannot supply/remove entries or edit a remounted workspace.
Actions refresh their rendered callbacks when the input revision changes.
Help consumes only admitted public display metadata and demand-mounts its
encoded-text inspector; changing that metadata closes stale dialogs.

Core validation and local draft diagnostics expose typed safe correction
locations. The renderer explicitly resolves current private coordinates through
the shared form before focusing a map entry; it does not reinterpret ordinals as
private keys or infer coordinates after a revision change.

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
and [public trust guidance](../public/browser.md#data-custody--security-architecture).

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

`Open` owns and disposes its `BrowserDefinitionSource`. A JavaScript module
returned after source disposal is released before any fetch or registry
admission. Concurrent initialization retains one code-module handle and releases
redundant handles; definition transport bytes retain their existing clearing and
generation fences.

Optional `DefinitionStoreCatalog` discovery is browser-owned public metadata,
loaded only on catalogue/source pages without an active workspace. Packaged same-origin
snapshots use a separate bounded asset reader; Core admission, exact indexed
identity/digest checks and `BrowserDefinitionRegistry` history still gate entry.
No store index is required by startup, independent sources, Core or native
clients. Store metadata cannot authenticate publishers or infer application
compatibility beyond declared versions. See [store ownership](DEFINITION_STORE.md).

The home catalogue groups public candidates by exact application identifier,
retains canonical source alternatives, and opens an exact directory candidate
with one action through `DefinitionStoreCatalog.OpenAsync`. Its read/workspace
generation gates still precede digest-pinned navigation. The optional project
inspector performs the same admission when Configure is selected; it does not
add a second confirmation. Missing explicit revisions never fall back to latest.
Unavailable session-only launch identities explain how to reopen the original
definition file or public link, without restoring answers or fetching silently.
An `/open` pointer carrying an explicit entry opens its admitted selection
directly after fetch/admission and the existing component generation check.
Pointers without an explicit entry retain source inspection. No answer controls
mount while source admission is pending.

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
`DefinitionSourceCodec` before opening an answer workspace. Closed content
discriminators select compact authoring compilation or expanded runtime
admission; remote retrieval continues to use `DefinitionCodec.ParseJson`
and rejects authoring sources. Local admission shares the 4 MiB byte budget,
owns and clears its input buffer, replaces the file control and restores focus.
No source comments are retained in canonical definitions, and local compilation
introduces no includes, schema-reference resolver or network authority. It is the fallback
for CORS/offline/source failures and starts a fresh no-secret workspace. There
is no proxy, target login or executable definition loader.

## Transient answer and output boundary

Deployment context controls adapt the admitted Presentation recipe/scope
projection and use its atomic `SelectConfigurationContext` mutation. They
resolve pending Developer edits first and recheck form/view ownership after
the dialog. Import and Developer destinations come from current eligible
artifacts; clients cannot expand scope by selecting an artifact. InputChanged
retires prepared per-file and aggregate capabilities synchronously before
awaiting JavaScript cleanup.

`BrowserArtifactBundleExport` builds a private clipboard document or standard
ZIP from one captured prepared-file list. It applies the existing 64-artifact
ceiling and separate 4,194,304-byte source/delivery bounds, checks portable
unique names, counts clipboard labels and ZIP overhead, and adopts no partial
aggregate. ZIP entry timestamps are fixed and host attributes are empty.
Assembly wraps a nonexpandable owned byte array: clipboard capacity is its
preflight byte count and ZIP capacity is the 4 MiB delivery ceiling. This avoids
untracked private arrays from stream growth; source, export and framework
allocations remain separate and require actual WASM peak-memory measurement.
Owned assembly buffers are cleared on completion/failure; exposure uses
`BrowserArtifactExport` generation fencing. Per-file recovery remains available
if delivery overhead exceeds the aggregate ceiling. Managed strings and bytes
already transferred to an external clipboard/download cannot be recalled.

`ConfigurationEditor` requires the registered `BrowserOfflinePolicy`; missing
wiring fails component construction rather than granting readiness. Public app
preparation and the current workspace's custody acknowledgment must both succeed
before answer entry. Starting again revokes older operation eligibility. Import
and preparation handlers also refuse unready work before consuming input or
composing output, including queued callbacks; disabled markup is not their only
authority check. Ordinary component fixtures use the real policy with explicit
deterministic public-module responses, not an absent-policy success path.

`BrowserNavigationState` retains only current-editor callbacks, never answers.
`WorkspaceNavigationGuard` uses Blazor `NavigationLock` and a native browser
confirmation. Before that separate accepted-workspace loss confirmation,
`IBrowserPendingChanges.ResolvePendingAsync` resolves the current Developer
document through Apply/Discard/Stay. Stay rejects navigation; invalid Apply
retains text and cannot release custody. Successful Apply changes the current
accepted workspace, not the navigation destination. The guard rechecks the
registration lease after this asynchronous resolution, then captures the updated
work generation for the loss confirmation.
Cancel preserves the editor. Confirm invalidates pending work
synchronously, awaits editor discard and releases custody before route loading.
Each registration returns an owner-bound lease. Confirmation and post-discard
checks retain that exact lease, including when the same component starts a new
workspace. Discard and disposal capture their lease before awaiting retirement
and release only that registration after cleanup. A replacement workspace
cancels the older navigation; an old completion cannot release its custody.
Generation checks also reject late confirmations. Group navigation
stays within the same mounted editor and does not pass through this route guard.

`ConfigurationEditor` uses `DefinitionFormModel` snapshots and Core validation
rather than a second browser evaluator. New workspaces use
`DefinitionSessionPrivacy.BrowserNoSecrets`; explicit consent enables secret
leaves. Nested imports/defaults/row edits follow that same shared policy.
Revocation removes secret leaves recursively, increments the answer revision
and invalidates output; clear, edits, navigation and disposal fence old work.
Typed fields and object-list rows follow admitted activation, validation and
effective sensitivity. Authored experiences and shared local editor expansion
affect disclosure only. Configure/Review/Prepare navigation stays within the
same mounted editor; it changes focus, not answer revision or prepared authority.
Only the selected authored section mounts answer controls. Shared form drafts,
origins and row tokens survive section unmounting; required/error ancestors
override obstructing optional disclosure. Required-answer and section progress
consume the shared obligation projection, not an independent Browser counter.
Missing containers are not misreported as deferrable secret leaves.
Guidance renders admitted plaintext and deliberately opened public HTTPS links
with `noopener noreferrer` and `no-referrer`; it adds no fetch or execution path.
The contextual inspector receives only that public schema, descriptions and
value-free definition/output keys. It never receives answer or prepared bytes.
Revocation, workspace replacement and disposal close the owning inspector and
invalidate late opens. Global search shortcuts yield to an active dialog.
The shared form owns typed presence separately from display text. Explicit empty
strings, admitted empty enum choices and empty containers survive import and
preparation; omission uses a distinct editing action. Invalid or rejected edit
intent invalidates output/import authority even if accepted JSON is unchanged.
Refresh and untouched navigation do not count as edits.

Closed discovery does not construct its controls/results. Its query and facets
remain form-owned, and full matching/ranking precedes 50-entry rendering pages.
Review mounts its effects on request, selecting active obligations/corrections,
deferred and supplied/defaulted entries by default. Omitted optional and inactive
inspection are explicit view-only choices. Diagnostics remain reachable outside
the paged effects. Neither paging nor mounting grants consent or output authority.

Local configuration reuse defaults to **Fill fields** and the current output's
single declared artifact; ambiguous output sets require a format choice.
**Compare with this definition** is an explicit read-only purpose.
`ConfigurationEditor` obtains the existing public-asset preparation and
workspace custody acknowledgment, then checks explicit workspace secret consent
before opening or consuming any raw configuration input. This applies to
public-only definitions, unknown/obsolete members and malformed input alike.
Definition labels cannot prove an old file safe. The Browser acquisition limit
remains 1 MiB; a bounded local stream is passed through
`DefinitionFormModel.CompareAsync(artifact, read, yield, token)`. The adapter
uses `js/configuration-rendering.js`'s `nextTask` boundary before
acquisition and between bounded Core traversal batches. Its local MessageChannel
closes both ports before resuming; comparison yields to the event loop without
waiting one display frame per batch. Incremental form mounting retains the
separate `nextFrame` boundary. No raw content or
report enters requests, worker messages, URLs, public caches or persistent
storage.

Presentation owns the one current private comparison proposal and publishes its
detached safe Core report through `ImportComparisonChanged`, not `ViewChanged`
or `InputChanged`. `ConfigurationImportReview` subscribes to that dedicated event;
comparison publication/retirement must not refresh ordinary answer editors,
commit valid/invalid drafts or retire prepared output. The editor keeps the
navigation lease active for retained comparisons even when no answers exist.
Artifact/purpose changes, cancel, edits, other operations, consent revocation,
replacement, navigation, clear and disposal invalidate pending generations and
retire comparison ownership. Physical read cleanup retains its captured lifetime
until completion; late results cannot reacquire authority.

The comparison panel separates execution, compatibility, coverage and inventory
completion, file-level autofill eligibility, current-workspace adoption refusal
and unassessed target acceptance. Known blockers have error color plus severity
text, not workspace `aria-invalid` state. Findings use bounded rendering pages;
a stopped inventory exposes a lower bound. `ExplainImportFinding(finding)`
returns declared schema guidance only, without focus or mutation. Source
ordinals and imported dynamic names are never passed to current workspace
correction/focus APIs. Deliberate `AdoptImportComparisonAsync(yield, token)` rechecks current
consent/revision/conflicts and applies eligible answers atomically. Structural,
unknown-content and invalid-input refusals offer separate-copy repair and
re-import, never partial adoption.

Denied secret leaves have no answer-bearing inputs, even when disclosure is
expanded. Consented controls use family/literal-path identities and stable row
tokens for both IDs and names. Admitted password purposes map `create` to
section-qualified `new-password` and `existing` to `current-password`; issuer
tokens retain `off` and acquisition/custom-field guidance. No fake username,
submitting form or vendor vault API is introduced. Password managers have
independent custody, outside workspace clearing.

Each rendered scalar input callback captures its form, copied literal path and
input revision. The same rendered handler advances its captured revision only
after its own accepted event, so queued keystrokes do not discard one another.
An input event is authoritative; the corresponding unchanged blur/change event
does not repeat that edit. A change-only password-manager fill still enters
through the same fenced edit method. Deliberate same-value input still advances
the revision. The adapter retains no extra copy of the input text.
A callback held across intervening edits, row replacement, revocation,
reconsent, form replacement or disposal cannot publish into a newer workspace
state. Current callbacks still pass through `Form.CanEdit` and shared typed
editing; autocomplete hints never confer consent.

Explicit secret generation uses the same per-workspace consent. The shared
`DefinitionFormModel.GenerateSecretAsync` captures the current editor identity,
input revision and operation generation; it rechecks eligibility before applying
Core-validated output. A shifted array row, variant change, revocation or disposed
form cannot receive late output. Generated hash inputs remain a separate transient
result, cleared on edit, dismissal, revocation and disposal. Copying an original
password is deliberate external clipboard custody; clearing the form cannot erase
that external copy. Neither generation nor format validation makes a network
request or grants issuer/target acceptance.

Consent denial and recursive secret clearing happen before the first cleanup
await. The editor detaches and synchronously invalidates every old artifact's
workspace and export owner before awaiting any retirement. Cleanup touches only
captured old resources, preserving public edits and replacement output through
late completion or failure. Unexpected cleanup failure is observed as a stable
value-free diagnostic; expected JS denial cannot restore authority.
Revocation also retires the comparison proposal synchronously and clears owned
input/output byte buffers. Retiring managed JSON/string references does not
guarantee physical erasure of managed strings, runtime copies or browser memory.
Exported files, clipboard contents and password-manager storage remain outside
this boundary.

Strict `DefinitionArtifactComposer` still requires complete valid answers.
`DefinitionDraftArtifactComposer` offers distinct secret-deferred preparation
with completion locations and Restricted output. Ordinary missing/invalid
values and unresolved secret-dependent conditions fail; draft mode never
changes strict readiness or fabricates secrets. See the
[Core contract](CORE_DEFINITION_CONTRACT.md) for owning APIs and bounds.

Browser prepares one Core bundle and stages disposable Presentation adoptions
before publishing any current output. Each emitted artifact is composed once;
adoption retains no answers and does not recompose it. A fatal/aggregate failure
or stale generation disposes the staged set. Draft completion/withholding
diagnostics remain incomplete-readiness signals even for independently usable
files.

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

Export sessions register explicitly on the first deliberate exposure. Unknown
IDs cannot open output; invalidation retains a revoked generation while exposure
may still be in flight. Managed retirement clears bytes immediately, observes
those exposure tasks and releases the JS session only after a delayed open/copy
cannot reopen it. Terminal release removes its map entry instead of retaining
unbounded tombstones. An import arriving after disposal never registers and
disposes its own module reference. Abandonment clears all session bytes and
entries. This does not retract data already handed to the clipboard or a download.

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

The public lifecycle module subscribes to browser online/offline events and
notifies `BrowserOfflinePolicy` with a boolean only. Subscription immediately
resamples connectivity after cached initialization; queued callbacks cannot
mutate a disposed policy, and disposal removes even a late-returning subscription.
Reconnection permits clean-entry source retrieval without a reload, but never
overrides live workspace custody. Browser online status is eligibility, not proof
that a remote host is reachable; normal bounded-fetch diagnostics still apply.

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
