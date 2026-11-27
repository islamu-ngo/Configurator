# Public Browser Trust Boundary and Anti-Conditioning Rationale

> **Audience:** Adopters | Contributors | Integrators | Security Reviewers | AI agents
> **Kind:** Explanation
> **Status:** Current architectural and provider-responsibility rationale; static Browser is implemented, native secret-store integrations and separate self-hosted connected Browser remain planned and unimplemented
> **Owner:** Browser
> **Last Verified:** 2026-11-27
> **Source Anchors:** `docs/internal/adr/ADR-self-hosted-connected-browser-and-public-static-boundary.md`, `docs/internal/adr/ADR-secret-store-integration-and-credential-sources.md`, `docs/internal/adr/ADR-browser-session-trust-and-export.md`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/public/browser.md`, `README.md`
> **Review basis:** Product-owner decision and documentation alignment on 2026-11-27; records the non-technical norm-setting rationale alongside existing technical custody ADRs without changing runtime behavior or qualification claims.

ISLAMU's public hosted Browser instance (`configurator.islamu.org`) permanently excludes direct integrations with secret managers (such as Infisical or Azure Key Vault) and PaaS management APIs (such as Coolify). This exclusion holds **even if every technical obstacle were removed**—for example, if operators enabled CORS on their self-hosted Infisical or Coolify instances, if WebAssembly could call provider SDKs directly, or if cryptographic bundle verification existed.

The reason is not only technical custody. It is **behavioral conditioning and norm-setting**: a trusted organization must not use its own reputation to normalize an unsafe security habit that bad actors can easily imitate.

---

## 1. The Core Problem: Norm-Setting and Security Conditioning

Technical security boundaries can expire or be worked around; human trust habits compound across an entire ecosystem.

Consider the hypothetical best technical case: suppose `configurator.islamu.org` ran direct Infisical, Azure Key Vault, or Coolify API calls 100% locally inside client-side Blazor WebAssembly, with zero ISLAMU server proxy, zero logging, and open-source reproducible builds. Even in that technically clean scenario, hosting live vault or PaaS integrations on ISLAMU's public web origin causes four structural harms:

1. **Brand trust silently replaces verification ("trust-on-every-load").**
   A browser downloads mutable web assets from a remote origin on every fresh visit or service-worker update. Even when technical verification mechanisms exist, ordinary operators—and especially new self-hosters or small community administrators—will not audit delivered WebAssembly payloads before each session. They will enter live credentials simply because they trust ISLAMU. When a malicious clone, compromised lookalike domain, or hostile "100% local WebAssembly DevOps helper" copies the exact same UX, the user's resistance has already been lowered by ISLAMU's precedent.
2. **Outbound secret traffic destroys the one simple verification rule users can actually enforce.**
   The current static public Browser establishes a clean, binary mental model: **zero outbound network requests occur while editing or preparing configuration**. An operator can inspect the browser's Network tab (or disconnect from the network after loading cached assets) and verify zero bytes leave the device. The moment `configurator.islamu.org` legitimately sends authenticated API requests to Infisical, Coolify, or Azure, outbound secret-bearing network activity during a browser session becomes normal and expected. Ordinary users lose their simplest, most reliable alarm bell.
3. **Working around origin restrictions trains actively dangerous sysadmin habits.**
   Connecting a third-party public webpage (`configurator.islamu.org`) to a self-hosted Infisical or Coolify server requires the operator either to add `https://configurator.islamu.org` to their vault or PaaS CORS allowlist, or to use local workarounds/extensions that weaken browser origin isolation. Whitelisting an external public origin on private infrastructure creates a standing exposure that outlives the browser tab—turning any future domain takeover, DNS/CDN compromise, XSS flaw, or dependency supply-chain attack on the public origin into live API reachability against the operator's infrastructure—and conditions operators to grant the same CORS exception to other external web tools.
4. **Live control-plane credentials carry a qualitatively higher blast radius than file preparation.**
   Handing a hosted webpage a machine-identity secret, Entra/Key Vault token, or Coolify API token grants live read/write management-plane authority over running infrastructure and existing secret stores, rather than drafting candidate files for a single deployment.

The principle mirrors an established epistemic discipline: even when an audience deeply trusts a knowledgeable speaker, citing verifiable primary sources remains essential—not because that specific speaker is suspected of dishonesty, but to train the community never to accept unverified authority blindly, thereby denying bad actors an exploitable precedent. In software security, ISLAMU refuses live vault/PaaS authority on its public website for the same reason: **refusing an unnecessary privilege protects users from everyone else who will later ask for it**.

---

## 2. Why Local Secret Entry in the Static Browser Is a Distinct, Bounded Necessity

A fair question follows immediately: if `configurator.islamu.org` refuses Infisical, Azure Key Vault, and Coolify tokens to avoid conditioning users into trusting third-party hosted web apps, why does the static Browser still allow per-workspace opt-in entry and generation of application passwords and secrets?

These two capabilities are **not** morally or architecturally equivalent:

| Dimension | Local secret entry & generation in static Browser | Direct secret-manager / PaaS integration on `configurator.islamu.org` |
| --- | --- | --- |
| **Role in Configurator's core promise** | **Foundational necessity.** Passwords, keys, and secret policies are integral to assisted configuration. Removing local secret handling from Browser would force every operator either to install Desktop anyway or to hand-edit raw files without schema validation, constrained generation, or field guidance—nullifying the zero-install Browser value proposition. | **Optional, advanced workflow.** Operators can complete configuration and export files (or secret-deferred drafts) without live vault/PaaS push. Advanced operators who want direct provisioning already have native clients and the planned self-hosted connected host. |
| **Default posture** | **Locked off by default** on every new workspace (`Secrets locked`). A complete **secret-deferred draft** workflow lets operators skip entering real secrets on the hosted web app entirely. | Would require collecting live provider credentials or authorizing third-party origin access to live infrastructure. |
| **Network boundary** | **Strict zero-network editing.** No answers or secrets leave the browser tab; verifiable in DevTools or offline after asset caching. | **Breaks zero-network isolation.** Normalizes outbound authenticated secret-bearing requests from a third-party hosted tab. |
| **Operator infrastructure impact** | **Zero standing server changes.** Does not ask operators to modify server CORS allowlists, reverse proxies, or browser security controls. | For self-hosted vaults/PaaS, pushes operators toward external-origin CORS allowlisting or origin-isolation workarounds. |
| **Trust disclosure & friction** | Requires explicit per-workspace opt-in consent with an honest disclosure that a hosted browser is the least safe surface for high-value secrets and recommends native clients. | Creates a streamlined third-party control-plane UX that trains muscle memory phishers can copy directly. |

In short, local secret entry in the static Browser is a **bounded necessity of Configurator's primary mission**, tightly constrained by off-by-default locking, secret-deferred drafts, explicit consent friction, and an unbroken zero-network boundary. Direct vault and PaaS integration on ISLAMU's public web origin is an **unnecessary expansion into live infrastructure authority** that breaks those guardrails.

---

## 3. Why Planned Pure-.NET Live Connectivity Probes Follow the Exact Same Boundary

The same surface and anti-conditioning rule governs **declarative live connectivity and endpoint verification probes** (planned and unimplemented; full specification in `dev/backlog/declarative-connectivity-probes.md`)—such as checking a Cerbos gRPC endpoint (HTTP/2 ALPN + `grpc.health.v1.Health` + optional `ServerReflection`), SMTP TLS/STARTTLS handshakes, TCP/TLS certificates, or OIDC discovery:

- **Native clients (`CLI`, `Terminal`, `Desktop`): YES (primary home).** Pure-.NET probes run in a separate native library (zero external CLIs like `grpcurl`, zero `Configurator.Core` network dependencies) on explicit operator click or command (`islamu-configurator verify`), using real sockets from the operator's workstation or SSH/LAN deployment vantage point.
- **ISLAMU Public Static Browser (`configurator.islamu.org`) & Current Static Self-Hosted Browser (`Configurator.Browser.Host`): NEVER (permanent exclusion).** Neither direct WebAssembly live probes—not even narrow secret-free CORS-by-spec probes such as OIDC `/.well-known/openid-configuration`—nor an ISLAMU server-side probe proxy are permitted:
  - *Technical walls:* browsers cannot open raw TCP/TLS/STARTTLS sockets or speak native HTTP/2 gRPC (`fetch()` does not expose HTTP/2 trailing headers or ALPN), CORS turns ordinary HTTPS `/healthz` checks into opaque failures (`no-cors` hides status codes), and Mixed Content / Private Network Access block `http://` and RFC1918/localhost targets.
  - *Proxy hazard:* an ISLAMU probe endpoint would become a public SSRF, port-scanning, and credential-stuffing oracle, and would tempt beginners to expose internal gRPC/DB/SMTP ports to the public internet just to turn a web test button green.
  - *Zero-network editing boundary:* even though static CSP includes `connect-src 'self' https:` strictly for pre-session `/open` definition retrieval (`src/Configurator.Browser/wwwroot/index.html`, `src/Configurator.Browser.Host/BrowserHostApplication.cs`), active workspaces remain strictly zero-network. Admitting narrow OIDC-only probes during editing would break that clean inspectable rule, leak operator staging hostnames and `Origin` headers during a live session, create jarring partial coverage (1 of 6 services works while gRPC/SMTP/DB cannot), and encourage CORS hole-punching on private targets.
  - *Safe static alternative ("Verify locally"):* static Browser may offer zero-network handoff via planned `configurator://` to Desktop/Terminal, or copyable, strictly **secret-free** local commands (`islamu-configurator verify ...` or diagnostic hints like `grpcurl <host>:<port> list`, never interpolating passwords into shell strings).
- **Future Self-Hosted Connected Browser (`Configurator.Browser.ConnectedHost`): OPTIONAL & LATER.** Deferred until native pilots prove the probe contract and `ConnectedHost` satisfies its independent authentication, explicit host/CIDR allowlist, cloud-metadata denial, DNS-rebinding pinning, bounded timeout/response, and secret-redaction qualification gates.

---

## 4. How the Deployment Architecture Teaches the Right Boundary

Configurator's multi-surface and multi-mode design turns this boundary into active security pedagogy rather than an arbitrary omission:

| Surface / Deployment Mode | Status | Secret & Target Boundary | Habit It Reinforces |
| --- | --- | --- | --- |
| **ISLAMU Public Static Browser** (`configurator.islamu.org`) | Implemented (static preparation only; `configurator://` handoff planned) | Strictly static Blazor WASM; zero target connectivity; secrets locked off by default with secret-deferred drafts; permanent exclusion of vault/PaaS credentials and proxies. | *"Never give a third-party website live vault/PaaS tokens or CORS exceptions; check that zero-network static tools stay offline, and move to local software for live infrastructure."* |
| **Self-Hosted Static Browser** (`Configurator.Browser.Host`) | Implemented | Identical GET/HEAD-only static package on the operator's own origin; zero target connectivity and no integration switch. | *"Eliminate third-party web-origin delivery trust while keeping the minimal, air-gapped static attack surface."* |
| **Native Clients** (Desktop, Terminal, CLI) | Local preparation implemented; SecretKit / Coolify provisioning planned | Locally installed, version-pinned executables using OS keyrings (`libsecret`, macOS Keychain, Windows Credential Manager) or explicit local credentials; direct SecretKit / provider APIs only on confirmed operator action. | *"Live infrastructure credentials belong in local, verifiable native tools on trusted machines—not third-party web pages."* |
| **Self-Hosted Connected Browser** (`Configurator.Browser.ConnectedHost`) | Accepted future direction; **not implemented or qualified** | Separate operator-owned container image inside the operator's own infrastructure and authentication boundary; local WASM editor sends only explicitly reviewed deltas to a same-origin connector; **prefers server-side target aliases and credentials** (via env / Docker secrets) so raw provider tokens do not enter browser inputs. | *"Web-based provisioning belongs only on your own authenticated origin backed by server-resident aliases—never on a third-party site and never by pasting raw vault tokens into browser JS/WASM."* |

When an operator on `configurator.islamu.org` wants to sync with Infisical, Azure Key Vault, or Coolify, the planned `configurator://` handoff to Desktop/Terminal (and link to self-hosting documentation) should **state this reason plainly**: ISLAMU refuses live infrastructure credentials on its public instance not because ISLAMU distrusts its own code, but so operators never have to trust *any* third-party website with them.

---

## 5. Provider-Responsibility Alignment (I-VSD)

> [!NOTE]
> This section captures **provider-responsibility design reasoning** under Islamic Value-Sensitive Design (I-VSD). It is engineering and ethical analysis of what maintainers control—not a religious-legal ruling (*fatwa*), Sharia certification, or empirical proof of user behavior.

- **Trust (*Amanah*) across time and succession:** Earned institutional trust is a stewardship duty, not capital to spend on normalizing fragile patterns. Even if ISLAMU's maintainers act with integrity today, domains expire, CDNs and supply chains face compromise, governance evolves over decades, and lookalike phishing domains appear whenever an open-source tool gains adoption. Structurally refusing live vault/PaaS custody on the public origin bounds future failure modes.
- **Non-Harm (*La Darar*) beyond ISLAMU's own servers:** Provider responsibility includes foreseeable second-order harms. Conditioning beginners to paste vault/PaaS tokens into "100% local WASM" websites or to add external domains to their vault CORS allowlists exposes those operators to third-party attackers long after they leave `configurator.islamu.org`.
- **Truthfulness and Clarity (*Sidq* and *Bayan*):** Marketing or UX must never imply that "runs in client-side WebAssembly on a third-party origin" equals native local custody. Keeping secrets locked by default, offering secret-deferred drafts, and visibly refusing live target connections on the public instance make the real trust boundary unmistakable.
- **Fairness (*Adl*) and Sovereignty:** Reserving direct vault/PaaS provisioning for open-source native clients and a self-hosted operator-owned connected package ensures organizations keep full control over their infrastructure without depending on an ISLAMU-operated service.

---

## 6. Governance and Revisit Boundary

This rationale operates independently of the technical findings in [ADR-secret-store-integration-and-credential-sources.md](adr/ADR-secret-store-integration-and-credential-sources.md) and [ADR-self-hosted-connected-browser-and-public-static-boundary.md](adr/ADR-self-hosted-connected-browser-and-public-static-boundary.md):

- **Not revisit triggers for `configurator.islamu.org`:** provider CORS support, operator-configured CORS on self-hosted Infisical/Coolify, browser-compatible SecretKit builds, OAuth/OIDC browser token flows, or browser extensions that relay local requests. None of these resolves the norm-setting and anti-conditioning harm on a third-party public origin.
- **What changing this boundary would require:** an explicit new product-owner decision, adversarial security and I-VSD re-evaluation addressing third-party phishing mimicry and origin-trust conditioning, and a new ADR superseding [ADR-self-hosted-connected-browser-and-public-static-boundary.md](adr/ADR-self-hosted-connected-browser-and-public-static-boundary.md). No configuration flag or silent policy drift may enable live vault/PaaS connections on the public instance.
