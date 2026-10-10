# ADR: Browser session trust, definition loading and deliberate export

Date: 2026-10-09
Status: Accepted design; implementation and verification evidence tracked in
[Operations](../OPERATIONS.md#browser-custody-and-offline-verification).
Owners: Core, Presentation, Browser and packaging.

## Context

A partial public-choice browser forced self-hosters to install another client
to prepare configuration. The intended browser interface must collect ordinary
settings, support bounded tenant editing and produce files from an application
README link. Some operators will voluntarily trust a hosted tab with secrets;
others need a draft they can complete locally. Preparation must remain separate
from deployment, target authorization and protected native writing.

## Decision

Keep static Blazor WebAssembly and reuse browser-safe Presentation/Core.
Presentation owns immutable answers and operation generations; Core owns closed
data admission, strict validation and bounded typed projection. Browser has no
native Artifacts, Event or target API/authentication dependency.

Every new workspace denies secret custody. Explicit consent enables secret
leaves for that workspace only; revocation recursively removes secret leaves,
retains ordinary settings and invalidates output and late work. Shared editing
and import gates enforce this below UI controls, including nested object lists.
All answers/output are transient and excluded from requests, storage, cache
payloads and worker messages. No persisted opt-in or answer synchronization is
introduced.

Strict complete composition remains strict. A distinct draft composer may
defer missing secrets with explicit completion paths and visibly incomplete
status. Ordinary errors and unresolved secret-dependent conditions still fail.
Drafts never fabricate credentials, claim readiness or confer target acceptance.
All answer-derived artifacts stay Restricted, regardless of field labels.

Admit public bundled, bounded credential-free HTTPS or local JSON definitions
before answers. Pin family/revision/canonical digest, check selectors and retain
conflict history. Direct public source links contain metadata only in a fragment;
no query credentials, redirects, proxy, executable plugin or target launch
authority. Browser DNS/transport and CORS remain platform limitations.

Published PWA retention includes reviewed public app assets and explicitly
selected admitted public definitions only. Asset readiness and definition
retention precede answers; reopening starts empty. Application remote fetch is
disabled during live work/offline operation. Updates wait for a clean lifecycle
across tabs and cannot reload/replace a live workspace.

Reveal, copy and download are deliberate current-output actions. The adapter
owns transient bytes and short-lived Blob URLs, clearing/revoking them on every
invalidation. Download transfers custody to browser/OS/operator: it cannot
establish native owner-only modes, create-only publication or forensic erase.
The native protected writer remains unchanged and the target retains final
semantic validation, authentication and apply authorization.

## Hosting alternatives and trade-offs

Standalone WebAssembly is the chosen interactive hosting model, selected by
`Configurator.Browser.csproj` and `WebAssemblyHostBuilder` in `Program.cs`.
It needs no `@rendermode InteractiveWebAssembly` directive. Static distribution
does not disable interactivity: Browser and browser-safe Presentation/Core
execute editing, validation and composition on the client.

| Alternative | Benefit | Custody and availability consequence | Decision |
| --- | --- | --- | --- |
| Standalone WebAssembly | Local event handling and offline operation after asset/definition preparation; static hosting | Larger initial runtime/app download and client CPU/memory use; delivered code and browser still require trust | Chosen for consistent client-side answers and output |
| Interactive Server | Smaller initial download, server compute and direct server-side .NET execution | Bound input values reach server memory; component interactions need a live connection; latency, circuit retention and server failures affect work | Excluded from this browser contract |
| Interactive Auto | Server interactivity initially, with WebAssembly available on subsequent visits | Processing location can vary by visit; an active component does not switch runtime when its download completes | Excluded to keep custody predictable |

MudBlazor supports WebAssembly as well as Server; component-library choice does
not require server custody. Configurator currently uses its own browser controls;
this decision does not add MudBlazor or a new host. Server interactivity can be
pleasant on a stable nearby connection, but it is not universally faster: local
WASM interactions avoid network round trips. No comparative performance claim
is made without measurement.

Per-workspace secret consent authorizes local secret entry, not transmission to
a server. A future hosted-processing interface would need its own explicitly
documented custody contract, consent, circuit/workspace isolation, bounded state
lifetime and diagnostic/persistence controls. TLS would protect transport while
still exposing usable answers to the processing server. Secret-free server
processing would also receive potentially confidential ordinary settings.

For high-value secrets, operator guidance recommends reviewed native clients
on a trusted machine and treats the hosted web app as the least safe interface
choice due to origin/browser trust and weaker export protection. This is an
exposure-based recommendation, not a universal ranking: native software and the
operating system still require trust, and protected save is verified on Linux
only. Offline browser work reduces live network exposure but does not establish
that previously delivered code or the browser environment is benign.

## Consequences and limits

The browser becomes sufficient for preparation while native clients remain
alternatives for protected local writing. Consent describes hosted-code trust,
not publisher certification or administrative permission. A compromised origin,
extension or browser can observe entered secrets; managed memory, clipboard and
downloads have lifetimes beyond clearing the application's owned buffers.
Browser-managed service-worker update checks and public app-asset installation
can generate traffic while an older workspace remains pinned. These requests
carry no answers, but the page's remote-definition guard cannot control all
worker/browser traffic. Physical disconnection is stronger than application-local
no-fetch assertions.

Public-safe definition metadata is a distribution responsibility. Core rejects
private defaults/recommendations, but cannot infer whether arbitrary descriptive
text is confidential. Explicit definition retention must not cache private
operational data. Draft files require operator completion and target validation.

## Source register and independent design

Official-interface research on 2026-10-09 used Firecrawl searches, with no copied
external implementation or assets:

| Source | Functional fact used | Design consequence |
| --- | --- | --- |
| [Microsoft: Blazor hosting models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0) | Server interactivity trades a smaller download and server execution for connection dependence; standalone WASM executes locally and can support offline work. | Prefer local custody and offline operation over initial payload size. |
| [Microsoft: Render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0) | Auto can choose Server initially and WASM on subsequent visits; it does not migrate an active component after download. | Exclude visit-dependent custody from the editor. |
| [MudBlazor: Installation](https://mudblazor.com/getting-started/installation) | Both Server and WebAssembly support interactive components. | Do not infer a server requirement from component-library choice. |
| [Microsoft: Blazor PWA](https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app/?view=aspnetcore-10.0) | Published offline behavior and worker updates are separate from development hosting. | Verify the actual published package; do not count dev/bUnit behavior as offline proof. |
| [MDN: Using Service Workers](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API/Using_Service_Workers) | A replacement worker waits while the previous worker controls clients. | Keep clean-lifecycle update admission across tabs; do not force replacement over answers. |
| [MDN: Content Security Policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CSP) | Resource-loading restrictions are distinct from application trust and authorization. | Review hosting CSP against WASM/worker/source requirements without claiming target authority. |

Sanitized handoff: published public assets may be cached; runtime answers must
not be cached or transmitted; source loading occurs before custody; update must
not replace active work. Naming, modules, snapshots, generation fences, row
controls, projections, tests and documentation use repository-owned patterns.
No dependency or external image/font is added by this decision. Platform-required
worker/manifest identifiers are interoperability elements. The AFC/SSO review
filters those platform constraints and common privacy practices; the remaining
discretionary organization follows Configurator owners, not a third-party
implementation. This is engineering provenance evidence, not a legal opinion.

## Verification

Core draft/collection invariants, shared consent/import/generation tests,
rendered Browser behavior and compiled/package closure are separate gates.
Actual published-browser network/cache/update, keyboard/mobile/zoom and output
inspection is required for platform claims. Exact executed evidence and omitted
lanes belong in Operations; an accepted design does not certify implementation,
hosting, target acceptance or release authority.
