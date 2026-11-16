# ADR: Self-hosted connected Browser and the public static boundary

> **Audience:** Adopters | Contributors | Integrators | AI agents
> **Kind:** Accepted decision
> **Status:** Current product direction; the connected package is planned and unimplemented
> **Owner:** Browser
> **Last Verified:** 2026-11-16
> **Source Anchors:** `src/Configurator.Browser.Host/Program.cs`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/internal/adr/ADR-secret-store-integration-and-credential-sources.md`

Date: 2026-11-16  
Reviewers: Product Steward, Browser, native client, Artifacts/Security and Packaging maintainers.  
Implementation status: **Decision and documentation only.** No connector API, connected
host, provider credential flow or target apply exists. The working name
`Configurator.Browser.ConnectedHost` below does not create a project, package,
publish target or support commitment.

## Context

The previous secret-store decision excluded every Browser secret-manager route
permanently and rejected a server unconditionally. That protected the public
static application, but also excluded a materially different deployment: an
operator can run both the Browser and a narrowly scoped target connector inside
infrastructure the operator already controls. Self-hosting can make a private
Coolify or vault endpoint reachable and can keep provider credentials with the
operator rather than ISLAMU.

That opportunity does not make a hosted proxy harmless. Applying configuration
sends protected values across a process and network boundary, creates an SSRF
capable target client, and introduces partially completed remote changes. The
new decision therefore distinguishes who operates the connector, which package
contains it and which request the operator authorizes. It does not treat
"self-hosted" as a security guarantee.

## Current state that remains unchanged

The current Browser is standalone Blazor WebAssembly. Editing, validation,
secret generation, `.cfgvault` cryptography and artifact composition execute in
the browser. Public definitions and application assets may be downloaded, but
answers and answer-derived artifacts are absent from application requests,
public caches and persistence. Secret entry requires consent per workspace.

`Configurator.Browser.Host` is only a static file host. It has no product
project reference, answer model, upload endpoint, credential store or target
proxy. It serves admitted public assets with GET/HEAD and rejects other methods.
Its current CSP and capability qualification apply only to that static package.

The following decisions are unchanged:

- native secret-manager operations, when implemented, use ISLAMU SecretKit;
- vendor CLIs never carry secret values, mutations or protected coordinates;
- a vendor CLI can only be an explicitly selected credential source;
- target applications retain semantic validation, authentication and apply
  authorization;
- preparation alone never grants target administration or deployment authority.

## Decision

### Three distinct deployment modes

| Mode | Local editing and composition | Target connectivity | Disposition |
| --- | --- | --- | --- |
| **ISLAMU public instance**, `configurator.islamu.org` | Entirely in standalone WebAssembly | None. No ISLAMU-operated SecretKit, vault/PaaS proxy, provider credential intake or target apply | **Permanent boundary.** Use deliberate file export or the planned native `configurator://` handoff |
| **Current self-hosted static package** | The same standalone WebAssembly, served by any static server or the existing GET/HEAD-only Host | None. Self-hosting static assets alone does not add integration | **Current behavior.** There is no feature flag that turns this package into a connector |
| **Self-hosted connected package** | The same local WebAssembly editor and browser-safe Core/Presentation semantics | A separate operator-owned authenticated connector performs only explicit reviewed target operations | **Accepted future direction, not implemented or qualified** |

The connected package must be a separately named host, image and qualification
boundary, with `Configurator.Browser.ConnectedHost` as its working name. It may
reuse the published static assets, but must not add API behavior, dependencies
or a runtime mode switch to `Configurator.Browser.Host`. An operator must be
able to select the static-only image and inherit its minimal attack surface by
default. A connected deployment must visibly disclose its mode and configured
operator/target boundary; it must not silently make the public or static
experience network-connected.

No ISLAMU deployment setting may enable the connected service on the public
origin. Offering an ISLAMU-operated connector would require a new explicit
product-owner and adversarial security decision, not a configuration change or
a reinterpretation of this ADR. This decision does not authorize one.

### Local WebAssembly remains the editor in every mode

Do not replace the editor with Blazor Server or Interactive Auto. Those models
place component state, input events and potentially answers/keystrokes in a
server-side SignalR circuit, require connectivity during editing and make the
processing location dependent on runtime selection and connection state. They
would undermine offline work and the predictable custody offered by static
hosting.

In the future connected package, Core/Presentation admission, form state,
validation, secret generation, encryption and file composition still execute
client-side. Only a deliberate reviewed target-delta operation crosses the
boundary. The connector does not become a second definition interpreter,
workspace store, cryptographic profile service or general application backend.
This explicit target request is a proposed exception to the current static
"no answer-bearing requests" invariant; implementing it requires its own
contract amendment, capability review and security qualification. It is not
permitted in either current static mode.

### Public proxy rejection rationale

An ISLAMU-operated public connector is permanently rejected under this product
direction because it would:

- transfer provider credentials or protected delta values into ISLAMU custody,
  even if retention were disabled;
- expose usable values to the proxy process and any ordinary request bodies,
  crash dumps, APM, debugging, shared heaps or operational mistakes;
- create a multi-tenant SSRF service able to reach cloud metadata, loopback or
  private addresses unless every request were perfectly constrained;
- ask users to trust ISLAMU host administration, routing and provider blast
  radius rather than their own infrastructure;
- still fail to reach many users' private LAN Coolify and vault instances.

TLS protects transport, not the processing endpoint's access to plaintext.
Self-hosting changes those custody and reachability conditions: the operator
selects the host, credentials, egress policy and reachable private targets. It
reduces third-party custody but does not remove server, browser, extension,
reverse-proxy or provider risk.

## Planned connected data flow

1. The operator opens the connected package, authenticates to its host and
   chooses one explicitly configured target alias.
2. The connector returns only bounded capability and value-free inventory
   metadata. It does not return secret values merely to make the browser's
   comparison possible.
3. Browser WebAssembly compares that inventory with the admitted definition and
   the operator's local snapshot, then renders additions, changes, omissions,
   conflicts and unavailable/unknown outcomes for review. Target inventory is
   not proof that retained values are current or complete.
4. After explicit confirmation, the Browser sends the bounded reviewed delta to
   the same-origin connector. Prefer a server-side target alias and credentials
   held in an operator-managed environment or Docker-secret facility, so raw
   provider credentials need not enter Browser memory. Server-side credential
   custody is preferred, not a promise that no credential-bearing flow can ever
   be proposed.
5. The connector resolves the target and credentials server-side, applies each
   authorized item through the provider-specific owner and returns a sanitized
   per-item receipt. SecretKit remains the secret-manager operation boundary.
6. The Browser displays applied, rejected, conflicted and unknown outcomes
   independently. Cancellation stops local work and requests server
   cancellation, but cannot undo a provider operation already accepted.

A multi-item request is not a distributed transaction. Providers can apply a
prefix before a later item fails. Conflict detection must use provider-native
versions/preconditions where available. An unknown result requires inventory
or operator reconciliation; automatic replay is forbidden unless a separately
reviewed provider idempotency contract proves it safe.

No answers, deltas, credentials, artifacts or receipts containing protected
values may be persisted in a connector database, queue, cache, browser storage
or product telemetry. Request-scoped handling does not mean guaranteed memory
erasure.

## Required connector gates before implementation

These are admission requirements, not claims about current code:

| Boundary | Required behavior |
| --- | --- |
| Package/origin | Separate connected image and capability contract; static package remains dependency-free and GET/HEAD-only. Never silently fall back between public, static and connected modes |
| Operator access | Mandatory host authentication and per-action authorization; CSRF/antiforgery and strict Origin validation for browser mutations. The authentication mechanism remains a separate implementation choice |
| Browser endpoint | Bounded same-origin API surface and matching CSP `connect-src`; no arbitrary user-entered connector URL or broad provider-origin allowance |
| Target admission | Exact operator-configured scheme/host/port allowlists and least-privilege target aliases. Block cloud metadata and unapproved loopback, link-local and private destinations |
| SSRF/transport | Defend against DNS rebinding, redirect-based escapes and address changes between validation and connection; use resolved-address pinning or an equivalent vetted mechanism and restrictive egress. Do not assume HTTPS alone prevents SSRF |
| Resource bounds | Bounded headers, payload/items/value sizes, concurrency, rate limits and provider response sizes; bounded connect/request timeouts and cancellation propagation |
| Workspace fencing | Bind each operation to the current definition identity, revision/digest, workspace generation and reviewed delta. Reject stale replies after edit, replacement, revocation, cancellation or disposal |
| Credentials | Prefer server-resident aliases and least-privilege credentials. Never expose provider credentials in ordinary logs, URLs, receipts or browser-readable error details |
| Confidential memory | Stream bounded protected fields into owned buffers rather than ordinary immutable `string` JSON models where practical; clear owned buffers in `finally`. Disclose residual runtime/TLS/Kestrel/GC copies instead of promising whole-process erasure |
| Diagnostics | No answer/request/response bodies, credential material or protected target coordinates in product logs or telemetry. Sanitize provider errors without hiding outcome class |
| Results | Per-item conflict, partial and unknown outcomes; no fake success, automatic unknown-outcome retry or unsupported atomicity claim |
| Target authority | Revalidate authorization and target semantics at the provider. Configurator does not restart services or acquire host administration merely by applying configuration |

A detailed threat model, failing adversarial tests and actual-host qualification
must cover forged origins, cross-user target access, malicious DNS/redirects,
private-address encoding variants, oversized/chunked/slow requests, cancellation
races, stale generations, secret-bearing diagnostics and partial provider
outcomes. Passing a static Browser suite or inspecting an HTTP client is not
qualification of this boundary.

## Alternatives considered

| Alternative | Disposition and rationale |
| --- | --- |
| Static Browser plus native `configurator://` handoff | **Chosen for the ISLAMU public instance and retained everywhere.** The public origin performs only local preparation; native handoff is planned, not current provider support |
| ISLAMU-operated public SecretKit/PaaS proxy | **Permanently rejected by this decision.** It centralizes credential/plaintext custody and multi-tenant SSRF risk and cannot reliably reach private operator networks |
| Direct WebAssembly-to-provider API calls | **Rejected.** They expose provider tokens to delivered JavaScript/WASM, XSS and extensions, face provider CORS and browser local-network-access restrictions, and cannot offer one consistent trust boundary across providers |
| Blazor Server or Interactive Auto editor | **Rejected in all modes.** Server circuits retain interactive workspace state and keystrokes, need connectivity and remove the static/offline custody model |
| Dual-mode `Configurator.Browser.Host` with an integration switch | **Rejected.** The existing static image must remain the small dependency-free default; a separate package prevents accidental exposure and independent qualification |
| Public Browser calling a localhost companion daemon | **Rejected for this direction.** It reintroduces native installation and adds local-network-access, pairing, origin authentication and DNS-rebinding complexity without a clearer custody advantage over native handoff |
| Separate self-hosted, authenticated, request-scoped connector | **Accepted as a future package.** It matches operator sovereignty and private-network reachability, subject to every gate above |
| Direct browser OAuth/OIDC token acquisition for each provider | **Not the default connected design.** Server-side credentials are preferred; a browser token still needs XSS/extension/CORS and audience-scope analysis. Any exception requires a provider-specific decision |

## Provider-specific design inputs

These notes were reviewed as functional design inputs on **2026-11-16**. They
are not live support, dependency approval, a security audit or a substitute for
rechecking official contracts during implementation. The older
[secret-store and credential decision](ADR-secret-store-integration-and-credential-sources.md)
still owns SecretKit transport and native sign-in choices.

- **Infisical:** native and future server-side operations use SecretKit.
  Universal Auth is the first native credential route; OIDC is a documented
  expansion. OAuth/OIDC browser token acquisition is not selected by default.
  Secret versions are append-only, so compare-then-apply needs a
  revision/precondition check rather than blind overwrite.
- **Coolify:** prove the native REST workflow first, using least-privilege
  read/write access without `read:sensitive` where the target contract permits,
  value-free inventory and `is_shown_once` handling. The connector must retain
  the same conflict, partial-result and receipt semantics rather than inventing
  a second API interpretation.
- **Azure Key Vault:** direct Browser calls cannot assume provider CORS support.
  Future connector credentials may consider Managed Identity or an
  operator-registered Entra application through the admitted .NET credential
  chain. `AzureCliCredential` remains an explicitly selected local native
  option, not the default identity for a shared connected server.
- **OpenBao:** a possible SecretKit/OIDC/BFF candidate only; provider admission,
  protocol and tenancy semantics remain unapproved.
- **Portainer and Dokploy:** candidate targets only. No connector, provider
  client or live support is promised by this ADR.

## Sequence and ownership

1. **Native provider pilots first.** Implement and qualify one Infisical
   SecretKit path and the separately approved Coolify path: value-free
   inventory, local-snapshot comparison, conflict detection, bounded material,
   per-item outcomes, cancellation and recovery. Do not start with a generic
   provider abstraction.
2. **Native handoff.** Implement `configurator://` through its own reviewed
   protocol, profile identity and privacy contract. It remains the public web
   route to native provisioning.
3. **Connected host.** Only after native semantics are proven, specify the
   exact request/receipt contract, authentication choice, deployment topology,
   dependency/legal review and threat tests for the separate connected package.
   Reuse concrete native/provider adapters only where their contracts fit;
   extract a shared abstraction only when at least two real consumers require
   it.
4. **Further providers.** Add candidates individually after conflict,
   credential, least-privilege and redaction review. Provider breadth does not
   relax any static-host boundary.

Core and browser-safe Presentation do not acquire HTTP or provider dependencies
from this direction. The connector is an adapter/host concern. Presentation and
any future client adapter retain generation fencing and visible outcome truth;
the target retains final semantic and authorization decisions.

## Amendments and relationship to earlier decisions

This ADR supersedes only the universal Browser/server exclusion in decision
item 6 and the alternatives table of the
[secret-store integration ADR](ADR-secret-store-integration-and-credential-sources.md).
It does **not** reopen direct provider access inside WebAssembly, static-host
provider credentials, vendor CLI data paths or an ISLAMU public connector. The
permanent public/static exclusion remains.

The [Browser session trust ADR](ADR-browser-session-trust-and-export.md)
continues to own current transient local editing and deliberate export. Its
future hosted-processing caveat is resolved for design purposes by this
operator-owned target-only connector; it is not resolved for arbitrary server
side workspaces or current runtime behavior. The
[encrypted profiles and target-apply ADR](ADR-encrypted-local-profiles-and-target-apply.md)
continues to defer actual target apply and owns local profile semantics.

## Values and honest communication

- **Amanah (custody):** do not make ISLAMU the custodian of users' provider
  credentials. Let operators choose their own connector and target boundary,
  with static-only operation remaining the default.
- **Sidq and Bayan (truthfulness and clarity):** distinguish implemented static
  behavior from the planned package; disclose every network/custody transition,
  partial outcome and memory limit. Never label a self-hosted deployment safe
  merely because it is self-hosted.
- **La Darar (avoid harm):** use least privilege, strict target admission,
  bounded request handling and no persistence; fail closed on ambiguous target
  identity, capability discovery and unknown apply outcomes.
- **Adl (fairness):** offer the same local configuration vocabulary without
  requiring a cloud account, and let organizations use their own private
  infrastructure rather than reserving connected operation for ISLAMU-hosted
  customers.

## Consequences and verification

- Public guides explain the public/static limitation and the planned separate
  self-hosted option without presenting unavailable commands or packages.
- Internal architecture, launch, packaging and provider documents link here
  instead of repeating the complete connector threat model.
- Static package behavior, generated release capabilities and existing tests
  remain unchanged. No runtime or provider implementation claim follows from
  this documentation change.
- Implementation requires new source, API/schema ownership or registration as
  applicable, adversarial tests, dependency/notice review, actual connected-host
  qualification and updates to public/internal operational guides.
