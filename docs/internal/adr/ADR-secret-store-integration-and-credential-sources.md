# ADR: Secret-Store Integration Through SecretKit and Explicit Credential Sources

Status: Accepted product-owner decision. Documentation only: no secret-store
client, credential flow, network operation or target apply is implemented.
Remote target apply stays deferred by the
[encrypted profiles and target apply ADR](ADR-encrypted-local-profiles-and-target-apply.md).
The [self-hosted connected Browser ADR](ADR-self-hosted-connected-browser-and-public-static-boundary.md)
(2026-11-16) supersedes only this record's universal Browser/server exclusion;
it does not implement target apply or reopen public/direct-WASM provider access.
Date: 2026-11-14
Owners: Native client (Desktop, Terminal, CLI), Artifacts/Security and Presentation maintainers.
Technical anchors: [Current decision](#current-decision), [Launch contract](../LAUNCH_CONTRACT.md), [Desktop client](../DESKTOP_CLIENT.md), [Terminal client](../TERMINAL_CLIENT.md), [Quick reference](../QUICK_REFERENCE.md).
Companion record: ISLAMU SecretKit `docs/internal/adr/ADR-006-configurator-provisioning-and-credential-sources.md`
and its "Configurator provisioning consumer" roadmap section in `docs/internal/CORE_SCOPE_AND_ROADMAP.md`.

## Current Decision

1. **SecretKit is the only path for secret-store operations.** Future Infisical
   and Azure Key Vault workflows on Desktop, Terminal and CLI use ISLAMU
   SecretKit in-process for every operation:
   - value-free key inventory, used for comparison against a local profile;
   - reads through the identity the target application will use;
   - reviewed writes, one item at a time, each with a write receipt.
   After the native pilots prove these semantics, the separate operator-owned
   connected host described in item 6 must delegate to the same SecretKit
   operation owner rather than inventing a second provider data path.
2. **No vendor CLI carries secret operations.** Configurator never passes a
   secret value, a mutation or protected coordinates to `infisical`, `az` or any
   other vendor CLI. It never parses CLI output for secret data or runs a CLI to
   read, list, write or delete secrets.
3. **A vendor CLI may only be an explicit, opt-in credential source.**
   - **Who chooses:** the operator, by selecting "use my existing CLI sign-in".
     It is never discovered automatically, chained or used as a fallback.
   - **Azure:** Microsoft's Azure.Identity `AzureCliCredential` runs
     `az account get-access-token` and hands back only a short-lived access
     token. Configurator does not launch the CLI itself.
   - **Why it's needed:** enterprise approval. The Azure CLI is a Microsoft
     first-party application already approved in tenants that block consent to
     new applications. Reusing its sign-in lets an operator use Configurator
     before their administrators approve any Configurator application.
   - **Infisical:** no CLI credential source. Its CLI user session has no
     documented third-party contract.
4. **Azure interactive sign-in: bring your own client ID only.** The operator
   (or their organization) registers an Entra public-client application with
   delegated Key Vault access. They enter its client ID and tenant as
   non-secret profile settings. Azure.Identity's browser or device-code
   credential then signs in with it. Configurator ships no client ID and never
   uses Azure.Identity's development default. An ISLAMU-published,
   publisher-verified multi-tenant Entra application is a **possible future
   expansion** that needs its own decision.
5. **Infisical: Universal Auth first.** The operator's organization creates a
   machine identity for Configurator with project-scoped permissions. Pilot
   intake decides client-secret custody, for example the existing OS keyring
   adapters or per-session entry. **OIDC Auth is the documented future
   expansion:**
   - Configurator would obtain an ID token from the organization's identity
     provider (for example Keycloak) through a browser loopback or device-code
     flow.
   - SecretKit already exchanges such a token for an Infisical access token.
   - Infisical then validates issuer, subject, audience and claims for that
     identity.
6. **Public/static Browser exclusion; separate connected host planned.** The
   ISLAMU public instance and the current self-hosted static package never gain
   secret-store access, provider credentials, vault APIs or CLI integration.
   The ISLAMU public exclusion is permanent: vault work hands off to a native
   client through the planned `configurator://` handshake. Direct provider
   access inside WebAssembly remains rejected because:
   - Azure Key Vault offers no CORS support for browser calls.
   - Self-hosted Infisical would need every operator to trust the web origin.
   - Tokens in WebAssembly memory are exposed to XSS and extensions. That is
     the same reason the encrypted profiles ADR rejected stored browser keys.
   - Browser publishes trimmed AOT WebAssembly, which SecretKit does not target.
   - It breaks the static target-zero-network promise.

   A **separate future self-hosted connected host** is accepted by the
   [Browser hosting ADR](ADR-self-hosted-connected-browser-and-public-static-boundary.md).
   It keeps the WebAssembly editor local and puts SecretKit/provider calls in an
   operator-owned authenticated connector, preferably with server-side target
   aliases and credentials. It must follow the native pilots, preserve reviewed
   per-item semantics and satisfy the connector threat gates. This is not an
   ISLAMU proxy, an integration flag in the static Host, current support or
   permission to put provider credentials in Browser storage.
7. **Pilot sequence.** Following the backlog rule that one provider pilot comes
   before any abstraction:
   - **Pilot:** Infisical through SecretKit on Desktop and Terminal, using
     Universal Auth. It compares the value-free inventory against a compare-only
     profile, then makes reviewed per-item writes with operator confirmation.
   - **Then:** Azure Key Vault with bring-your-own client ID and the opt-in Azure
     CLI credential.
   - **Then, separately:** the operator-owned connected Browser host, only
     after native inventory/conflict/partial-outcome semantics are reusable.
   - **Later:** a Configurator-owned provisioning-sink abstraction, only after
     two providers show shared semantics.
8. **Provider breadth tiers.** Configurator aims to support many secret
   managers, in three tiers:
   - **Deep SecretKit providers:** Infisical, Azure Key Vault, and a future
     admitted candidate such as OpenBao.
   - **Thin Configurator sinks** over those providers' typed operations: name
     inventory, write, delete with confirmation.
   - **Providers whose human sign-in exists only through a CLI or desktop
     application with no .NET contract** (1Password today): wait for a
     contract, or take a separate token-only credential-source decision. They
     never get a data path.
9. **Readiness as the application's identity (planned).** After preparing an
   application's own runtime identity, for example Event's role-specific
   Infisical identities, Configurator may confirm through SecretKit that every
   required key is readable *by that identity*. It uses callback-scoped material
   and never reveals values. Administrator visibility is not proof that the
   application can read a value.

Every network action stays operator-confirmed. Network activity remains zero by
default, and preparation still grants no target apply or administration
authority.

## Context

Earlier Configurator documents named two transports for Infisical and Azure Key
Vault. The README, Product Vision, Desktop client contract, marketing FAQ and
the encrypted profiles ADR's historical section 6 said "official provider CLIs
(`infisical-cli`, `az`) or SecretKit". Vision and Approach, the Terminal client
contract and the Terminal framework ADR said "opt-in REST operations". This
record settles that inconsistency; the affected documents now point here.

The initial reasoning for CLIs was that vendor tools are maintained by the
vendor and therefore trustworthy, and that they are faster to adopt. The
concern with CLIs was that a Blazor client could never use them. Analysis showed
that the browser constraint is independent of transport (see the browser item
above). The decision therefore rests on custody, outcome semantics, distribution
and long-term maintenance.

## Findings

### What a vendor CLI actually is

Infisical's documentation describes its REST API as covering "everything the CLI
and SDKs do". A vendor CLI is an API client plus a login flow, a local
credential store and a terminal interface:

| CLI capability | Possible in a library? | Owner after this decision |
| --- | --- | --- |
| Secret read, list, write, delete, versions, folders | Yes; SecretKit implements typed subsets | SecretKit |
| Project, environment, identity, role and vault administration | Yes; SecretKit management packages hold subsets | SecretKit (optional packages) |
| Azure browser, device-code and broker sign-in, MFA, Conditional Access | Yes, through Microsoft's Azure.Identity/MSAL, the stack the Azure CLI uses | Azure.Identity, selected by Configurator |
| Infisical human sign-in | Only through public machine-identity contracts (Universal Auth, OIDC/JWT Auth) | Configurator acquires proofs; SecretKit exchanges them |
| Remembering sign-in between runs | Yes (Azure.Identity persistent cache; Configurator's Credential Manager, Keychain and libsecret adapters) | Configurator |
| Default organization, project, environment and profiles | Yes | Configurator profiles |
| `infisical run`, `.env` export | Yes | Configurator artifacts |
| Offline cache of fetched secrets (Infisical CLI behavior) | Possible, intentionally excluded | Nobody |

Only two things cannot be reproduced by a library:

- **Undocumented vendor-private flows:** the Infisical CLI's user-session login,
  and 1Password desktop-app unlock, whose SDKs are Go, JavaScript and Python
  only.
- **The Azure CLI's pre-approved first-party identity.** The opt-in credential
  source in decision item 3 addresses this.

### Why a CLI data path loses

- **Custody.** CLI output is plaintext that becomes ordinary strings, bypassing
  SecretKit's callback-scoped material. Writes need the value either in command
  arguments, visible in process listings and shell history, or in a temporary
  plaintext file. A temporary file contradicts compare-only's "zero stored
  secret bytes" and the create-only protected writer.
- **Outcome semantics.** An exit code and localized error text cannot separate
  absent, denied and unavailable. They also cannot distinguish "not sent" from
  "possibly sent". Reviewed change apply needs per-item partial outcomes,
  recovery without automatic replay, and honest cancellation. Killing a process
  leaves remote state unknown.
- **"Official" is not stricter custody here.**
  - The Azure CLI stores its MSAL token cache, refresh tokens included, as
    plaintext on Linux and macOS. Azure.Identity's persistent cache refuses
    unprotected storage unless explicitly allowed.
  - The Infisical CLI silently falls back to a file vault whose passphrase is
    stored in its own config file, and keeps fetched secrets for offline use.
  - The Azure CLI collects telemetry by default.
- **Distribution.**
  - Operators must install the CLIs on every machine, including SSH hosts for
    Terminal.
  - Resolving a CLI through `PATH` is an injection point, and CLI versions float
    outside Configurator's lockfiles, notices and provenance.
  - Sandboxed packaging such as Flatpak can only run host binaries through
    `flatpak-spawn --host`, which effectively removes the sandbox. Flatpak, Snap
    and app stores are outside Desktop packaging revision 1, but a CLI
    dependency would close that path.
- **Two transports double the work.** SecretKit was already planned for
  verification, so a second transport would double sign-in flows, error mapping,
  tests and the support matrix.

### Where a CLI genuinely helps, and how this decision answers it

| CLI advantage | Answer |
| --- | --- |
| Pre-approved Microsoft application in enterprise tenants | Opt-in `AzureCliCredential` (decision item 3) |
| Reuses an existing sign-in | Same opt-in for Azure; Infisical uses Universal Auth, later OIDC |
| Vendor tracks its own API changes | Accepted cost: SecretKit maintains adapters and pinned conformance; maintainer capacity is the main long-term risk |
| Faster first demo | Target apply is already deferred, so no deadline favors a CLI |

## Alternatives considered

| Alternative | Disposition |
| --- | --- |
| CLIs for secret operations, SecretKit for verification | Rejected: custody loss, no receipts, two transports |
| SecretKit only, no CLI credential option | Rejected: enterprise tenants may block consent to a new application |
| Configurator launching `az` itself for tokens | Rejected: Azure.Identity already provides `AzureCliCredential` |
| ISLAMU multi-tenant Entra application now | Deferred: needs tenant ownership and publisher verification; possible future expansion |
| Infisical OIDC Auth now | Deferred: Universal Auth first; OIDC is the documented expansion |
| Direct Browser vault access or an ISLAMU public proxy | Rejected permanently for the public/static packages: CORS, token custody, centralized SSRF and target-network boundaries |
| Separate operator-owned connected host after native pilots | Accepted future direction by the [Browser hosting ADR](ADR-self-hosted-connected-browser-and-public-static-boundary.md); no implementation or provider support yet |

## Values and honest communication

- **Amanah (custody):** values stay in Configurator's custody and SecretKit's
  owned material, never in a CLI process, argument list or temporary file.
- **Sidq and Bayan (truthfulness and clarity):**
  - Documents mark provisioning as planned until it is implemented and
    qualified.
  - The UI must state which identity and credential source performs each
    action.
  - Choosing an existing Azure CLI sign-in means trusting that CLI's own token
    storage and telemetry settings. The UI must disclose this when the operator
    selects it.
- **Sovereignty:** self-hostable, open-source providers come first in breadth
  planning. OpenBao (MPL-2.0) fits that goal better than source-available Vault,
  subject to SecretKit's provider admission review.

## Consequences

- **Documents updated to follow this record:**
  - README
  - Product Vision
  - Architecture
  - Vision and Approach
  - Marketing strategy
  - Desktop and Terminal client contracts
  - Launch contract
  - Quick reference
  - Public Browser, Desktop, Terminal and import guides
  - Terminal framework ADR
  - Encrypted profiles ADR (current decision note)
  - Local secret-store planning notes (not distributed with this record)
  - Major decisions
- **SecretKit work Configurator depends on** (proposed there, not implemented):
  - Windows and macOS qualification;
  - multi-folder value-free Infisical inventory;
  - an interactive-operator composition guide;
  - write-conflict honesty for compare-then-apply;
  - Azure subscription discovery.
- **Pilot intake must still settle:**
  - permissions;
  - client-secret custody;
  - conflict-safe writes;
  - partial outcomes, cancellation and recovery;
  - redaction;
  - audit attribution under a machine identity.

## Revisit triggers

- Infisical or another vendor publishes a documented delegated sign-in contract
  for third-party desktop applications.
- Enterprise adopters need a ready-made multi-tenant application (triggers the
  ISLAMU Entra application decision).
- A provider Configurator needs exposes human sign-in only through its CLI or
  desktop application with no .NET SDK or public protocol.

## Sources (retrieved 2026-11-14)

Functional facts only, not Configurator or SecretKit verification evidence:

- [Infisical llms-full](https://infisical.com/llms-full.txt): REST API described as covering
  everything the CLI and SDKs do.
- [Infisical CLI vault reference](https://infisical.com/docs/cli/reference/vault) and
  [CLI FAQ](https://infisical.com/docs/cli/faq): keyring with automatic file fallback whose
  passphrase sits in the CLI config; fetched secrets saved for offline use.
- [Infisical OIDC Auth](https://infisical.com/docs/documentation/platform/identities/oidc-auth/general):
  issuer, subject, audience and claim binding for machine identities.
- [MSAL-based Azure CLI](https://learn.microsoft.com/en-us/cli/azure/msal-based-azure-cli):
  token cache plaintext on Linux and macOS; [azure-cli#23495](https://github.com/Azure/azure-cli/issues/23495).
- [What is the Azure CLI](https://learn.microsoft.com/en-us/cli/azure/what-is-azure-cli):
  telemetry collected by default.
- [Azure.Identity token cache](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/samples/TokenCache.md)
  and [InteractiveBrowserCredentialOptions](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.interactivebrowsercredentialoptions):
  protected persistence by default; the default client is a development
  application not recommended for production.
- [azure-cli#23911](https://github.com/Azure/azure-cli/issues/23911): `AzureCliCredential` runs
  `az account get-access-token` in a subprocess.
- [az keyvault secret](https://learn.microsoft.com/en-us/cli/azure/keyvault/secret): inline
  `--value` versus `--file`.
- [Blazor WASM and Key Vault (Microsoft Q&A community answer)](https://learn.microsoft.com/en-us/answers/questions/883975/net-6-blazor-wasm-getting-cors-errors-when-trying):
  Key Vault does not support CORS.
- [1Password SDKs](https://www.1password.dev/sdks): Go, JavaScript and Python.
- [Flatpak command reference](https://docs.flatpak.org/en/latest/flatpak-command-reference.html):
  `flatpak-spawn --host` runs a command unsandboxed on the host.
