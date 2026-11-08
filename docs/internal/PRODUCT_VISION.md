# ISLAMU Configurator — Product Vision and Sovereign Infrastructure Strategy

> **Audience:** Contributors | Adopters | Integrators | AI agents
> **Kind:** Explanation
> **Status:** Current strategic product vision
> **Owner:** Contributor Experience
> **Last Verified:** 2026-11-08
> **Source Anchors:** `docs/internal/universal-configurator.md`, `docs/internal/go-to-market.md`, `docs/internal/CORE_DEFINITION_CONTRACT.md`, `docs/internal/PRESENTATION_STATE.md`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/internal/BROWSER_UI.md`, `docs/internal/DESKTOP_CLIENT.md`, `docs/internal/TERMINAL_CLIENT.md`, `docs/internal/CLI_CONTRACT.md`, `docs/internal/DEFINITION_STORE.md`

---

## 1. Executive Summary & Purpose

**ISLAMU Configurator** is an offline-first, client-side configuration preparation engine and portable setup-definition runtime. It bridges the chasm between software authors who publish complex configuration requirements and operators who must safely deploy, update, and manage that software across diverse environments.

Rather than forcing operators to hand-craft brittle, unvalidated `.env` files or navigate monolithic, platform-locked deployment portals, Configurator reads a declarative, non-executable, bounded schema and renders an intuitive, validated configuration experience across four client surfaces: **WebAssembly (Browser)**, **Desktop (Avalonia)**, **Terminal (Terminal.Gui)**, and **Headless CLI (`islamu-configurator`)**.

This document captures the holistic product vision, its architectural pillars, its integration into the broader ISLAMU non-profit ecosystem, its lifecycle upgrade capabilities, its native secret manager integrations, and its role as a foundation for autonomous AI agent quality assurance.

---

## 2. The Organizational Mission: The ISLAMU Non-Profit

ISLAMU is a non-profit organization dedicated to building and maintaining sovereign, ethical, and high-integrity digital infrastructure. While rooted in Islamic values, the mission extends beyond religious ritual software into critical, general-purpose digital systems that Muslim communities and values-aligned organizations require for independence and security:

1. **Digital Sovereignty & Resilience:** Ensuring that community institutions, mosques, schools, and non-profit organizations do not depend entirely on proprietary infrastructure controlled by foreign, potentially hostile states or monopolistic corporate entities that subject users to arbitrary deplatforming, invasive surveillance, or sudden sanctions.
2. **Ethical Alternatives to Rent-Seeking SaaS:** Providing production-ready, fully free and open-source alternatives to critical tools that are currently locked behind expensive subscriptions or extractive monetization models.
3. **Broad Scope Beyond Religious Software:** ISLAMU produces both community-specific software (such as **ISLAMU Event**, a comprehensive event and attendee management platform) and foundational technical infrastructure (such as **ISLAMU Configurator** and **ISLAMU SecretKit**), with long-term horizons encompassing sovereign communications and productivity tooling.

ISLAMU Configurator serves as the configuration foundation of this entire ecosystem, guaranteeing that every application released by the organization can be self-hosted with zero friction, zero telemetry, and verifiable security.

---

## 3. The Core Problem: The Agony of Self-Hosting Configuration

Self-hosted software is paralyzed by the configuration barrier:

* **The 400-Variable `.env.example` Trap:** Upstream projects often supply massive sample configuration files with minimal inline commentary. Operators have no programmatic way of knowing which variables are strictly mandatory, which are optional, what ranges or formats are expected, or which settings become active only when another setting is chosen.
* **The Sprawl of Fragmented `docker-compose.x.yaml` Files:** Complex applications (including platforms like ISLAMU Event) frequently attempt to accommodate different deployment topologies by maintaining dozens of separate compose variants (`docker-compose.sqlite.yml`, `docker-compose.postgres.yml`, `docker-compose.traefik.yml`, `docker-compose.prod.yml`). This creates a combinatorial explosion of maintenance overhead for authors and sheer confusion for operators. Configurator collapses this sprawl into a single declarative definition that conditionally outputs the exact configuration files needed for the chosen topology.
* **Silent Runtime Failures:** A typo in a database host, an invalid URL format, or an insufficient secret key length is frequently discovered only after an application crashes on boot, leading to frustrating trial-and-error debugging cycles.
* **The Upgrade Hell (Day-2 Drift):** When software updates from `v1.0` to `v2.0`, maintainers introduce breaking environment changes, rename keys, and deprecate older configurations. Operators are forced to manually comb through release notes and perform diffs by hand, leading many to freeze updates and run insecure, outdated versions.
* **The Privacy Paradox:** Web-based deployment portals often require pasting live database credentials, SMTP passwords, and API keys into a remote server, converting the configuration process into a security vulnerability.

### The Historical Paradox: Why Hasn't This Existed Before?

Given the universality and severity of configuration pain across the self-hosting ecosystem, why hasn't a universal, open, client-side configuration engine existed until now? Five historical and economic barriers converged to stall previous efforts:

1. **The "Tragedy of the Commons" in DevOps:** Application authors view deployment as an operations problem outside their scope, while sysadmins write bespoke Bash scripts or Ansible playbooks tailored strictly to their own infrastructure. Neither side had the incentive to construct a universal, application-agnostic configuration abstraction layer.
2. **The Commercial Walled-Garden Trap:** Companies that attempted to streamline setup (such as Unraid, TrueNAS, Portainer, and CasaOS) deliberately designed proprietary template systems locked into their own operating systems and daemons to protect their platform moats, rather than creating a neutral, portable open-source standard.
3. **The Web SaaS Security Paradox:** In the cloud era, visual configuration tools took the form of web applications. However, security-conscious operators rightly refuse to paste database passwords, encryption keys, and private tokens into third-party servers. Only the recent maturity of static, client-side WebAssembly (WASM) and zero-network execution allows a web browser to serve as an air-gapped, zero-trust configuration engine.
4. **The Robotic Failure of Pure JSON Schema UIs:** Previous attempts to generate interfaces from JSON Schema or OpenAPI produced rigid, visually unappealing enterprise forms that lacked humanized guidance, wire-token toggles, conditional defaults, or multi-target format emission.
5. **The Pre-AI Authoring Bottleneck:** Hand-crafting a 5,000-line schema covering 300 variables, validation rules, and inter-field dependencies required 40+ hours of tedious manual engineering per application. Maintainers abandoned the effort and defaulted to `.env.example`. Modern agentic engineering permanently breaks this economic barrier.

ISLAMU Configurator eliminates these failure modes by treating configuration as a formal, versioned, declarative data contract evaluated locally and deterministically.

---

## 4. Multi-Surface Delivery Architecture

Configurator delivers a unified configuration definition across four purpose-built surfaces, sharing a single stateless core engine ([`Configurator.Core`](CORE_DEFINITION_CONTRACT.md)):

```text
               ┌────────────────────────────────────────────────────────┐
               │                SetupDefinition Schema                  │
               │         (Declarative, Bounded, Non-Executable)         │
               └───────────────────────────┬────────────────────────────┘
                                           │
                                           ▼
               ┌────────────────────────────────────────────────────────┐
               │                   Configurator.Core                    │
               │   (Validation, 23 O(n) Formats, Canonical JSON AST)    │
               └───────────────────────────┬────────────────────────────┘
                                           │
       ┌───────────────────┬───────────────┴───────────────┬───────────────────┐
       ▼                   ▼                               ▼                   ▼
┌──────────────┐    ┌──────────────┐                ┌──────────────┐    ┌──────────────┐
│ WebAssembly  │    │   Desktop    │                │   Terminal   │    │ Headless CLI │
│   Browser    │    │  (Avalonia)  │                │(Terminal.Gui)│    │(Spectre/Core)│
├──────────────┤    ├──────────────┤                ├──────────────┤    ├──────────────┤
│ 100% Client  │    │ Linux, Win,  │                │ SSH / Server │    │ CI/CD, Agent │
│ Zero Network │    │ macOS Native │                │  80x24 TUI   │    │  Automations │
└──────────────┘    └──────────────┘                └──────────────┘    └──────────────┘
```

1. **Static WebAssembly Browser Client ([`Configurator.Browser`](BROWSER_UI.md)):**
   * Hosted statically at `configurator.islamu.org` (and easily self-hosted as static HTML/WASM).
   * Executes entirely in-memory within the operator's browser.
   * **Absolute Air-Gapped Custody ([`LAUNCH_CONTRACT.md`](LAUNCH_CONTRACT.md)):** Zero telemetry, zero network tracking, and zero backend answer storage. Operators can inspect their browser's Network tab and verify zero bytes transmitted during the entire configuration session.
   * Direct deep-linking allows immediate loading of definitions via URL parameter or store hash without prior installation.
2. **Cross-Platform Native Desktop Client ([`Configurator.Desktop`](DESKTOP_CLIENT.md)):**
   * Built on Avalonia 12 with compiled data-binding and native host qualification across Linux (X11), Windows, and macOS.
   * Supports native create-only protected file publishing directly to disk.
   * Material Design 3 tonal surfaces, 4-step density scaling, and native high-contrast accessibility.
3. **Terminal Workbench ([`Configurator.Terminal`](TERMINAL_CLIENT.md)):**
   * Built on Terminal.Gui v2, optimized for system administrators working over SSH or in head-less Linux environments.
   * Compact 3-stage workbench (`Configure`, `Review & Import`, `Files & Save`) fitting cleanly within standard `80×24` terminals, with an automatic split section navigator on terminals $\ge 100$ columns.
4. **Headless Machine CLI ([`Configurator.Cli`](CLI_CONTRACT.md)):**
   * The standalone `islamu-configurator` command-line utility.
   * Enables offline validation, definition compilation, linting, secret generation, and artifact composition for shell scripts, CI/CD pipelines, and autonomous AI agents.

---

## 5. Dual-UX Strategy: Guided User vs. Power-User Developer

Configurator rejects the false dichotomy between "dumbing down software for beginners" and "imposing raw complexity on experts." It delivers a first-class **Dual-UX Architecture**:

### Guided User Experience
* **Deterministic Identifier Humanization:** Raw environment tokens (`SCREAMING_SNAKE_CASE`) and enum values (`OPENAI_COMPATIBLE`) automatically humanize to Title Case with canonical acronym preservation (`Screaming Snake Case`, `OpenAI Compatible`) when explicit titles are omitted.
* **Rich Visual Widgets:** Expressive sliders for bounded numbers, multi-state segmented toggles for booleans, inline unit adornments, and collapsible section groups.
* **Non-Materializing Explanations:** Non-editable `"If omitted: <defaultText>"` helper callouts explain target application runtime behavior without silently injecting unreviewed defaults.
* **Schema-Bounded Modal Secret Generator:** Interactive KeePass-style modal dialog allowing operators to customize length, character sets, and exclusions while mathematically enforcing admitted schema constraints and a 128-bit free-draw entropy floor.

### Power-User / Developer Experience
* **Instant Naming Mode Toggle (`Ctrl+Shift+K`):** Switches the entire interface instantly between humanized labels and raw technical wire tokens (`SCREAMING_SNAKE_CASE`, JSON pointers, and environment bindings) with explicit bidirectional isolation (`dir="ltr"`) in Arabic (`rtl`) mode.
* **Consent-Safe Compact Property Table (`BrowserNoSecrets`):** A dense, virtualized, single-row property table (`about:config` / VS Code Settings style) capable of smoothly filtering and navigating 600+ settings. Crucially, public settings can be inspected and edited immediately without prematurely granting secret custody; secret rows remain masked and locked until deliberate consent is given.
* **`DefinitionDocumentSession` Editors:** Full raw text buffer editing (JSON / dotenv) in Desktop and Terminal, protected by `OperationGeneration` stale-concurrency rejection and automatic memory zeroing.

### Desktop Managed Profile Vault & Protocol Handshake
* **The `configurator://` Protocol Handshake:** Like Microsoft Teams, VS Code, or Zoom, deep links and release note links can trigger a protocol handshake. Web visitors receive a non-intrusive prompt: *"Open in Configurator Desktop or continue in Web?"*
* **Managed Local Profile Store:** The Desktop application maintains an encrypted, user-owned local directory (`~/.config/islamu-configurator/profiles/<app-id>/`) that automatically catalogs active configurations across environments:
  * `development.env` / `answers.json`
  * `staging.env` / `answers.json`
  * `production.env` / `answers.json`
* **Zero-Touch Profile Matching:** When a definition is opened, Desktop matches existing local profiles by definition identity and metadata fingerprints, enabling single-click environment switching without file dialog hunting.

---

## 6. Configuration Lifecycle: Version Migration & Breaking Change Diffing

The value of Configurator extends far beyond the initial installation (`Day-1`). Its greatest long-term operational leverage lies in **Day-2 Maintenance and Application Upgrades**:

```text
   [Existing answers.json] (v1.0.0)
            │
            ▼
   ┌────────────────────────────────────────────────────────┐
   │         Configurator Version Migration Engine          │
   │      (Semantic Schema AST Diff: v1.0.0 vs v2.0.0)      │
   └───────────────────────────┬────────────────────────────┘
                               │
                               ▼
   ┌────────────────────────────────────────────────────────┐
   │                   Actionable Report                    │
   │  • 2 Obsolete settings safely retired                  │
   │  • 1 Setting renamed (automatic mapping suggested)     │
   │  • 3 NEW required settings must be answered            │
   └────────────────────────────────────────────────────────┘
```

1. **The Upgrade Problem:** When an application updates, its configuration schema evolves. Operators currently have no structured way to evaluate the impact on their running instance without reading dense changelogs.
2. **Declarative Version Diffing:** Because Configurator definitions are formal, versioned ASTs, Configurator can compare an active configuration snapshot against a newer schema release:
   * **Newly Required Obligations:** Identifies required settings added in the new version that lack defaults.
   * **Renamed or Restructured Keys:** Maps old keys to new paths automatically based on schema evolution metadata.
   * **Deprecated & Obsolete Settings:** Flags configuration entries that the new version will ignore or reject.
   * **Changed Default Behaviors:** Highlights where an omitted setting's dynamic default behavior has shifted.
3. **The Release Notes 1-Click Upgrade Diff Workflow:**
   * Upstream maintainers include a direct link in their GitHub release notes:
     ```markdown
     [⚡ Check Configuration Breaking Changes](https://configurator.islamu.org/diff?id=immich&from=v2.3.0&to=v2.4.0)
     ```
   * **Desktop Surface:** Automatically loads the operator's saved local profile for that application and displays the exact visual delta with zero manual input.
   * **Web Surface:** Prompts the operator with a drop zone: *"Drop your current `docker-compose.env` here to see your diff."*
   * **Direct Secret Vault Update:** If Infisical or Azure Key Vault sync is enabled, the operator can click **"Apply Upgrade Diff to Staging"** or **"Production"** directly, resolving all new obligations in seconds.
   * **Eliminating Upgrade Paralysis:** Operators no longer fear running `docker compose pull && docker compose up -d`, because breaking configuration changes are identified, resolved, and verified before the container restarts.

---

## 7. Secret Management Integration: `ISLAMU SecretKit` and Native Vault Sinks

### The Role of `ISLAMU SecretKit`
`ISLAMU SecretKit` is an independent .NET 10 library suite developed under the ISLAMU non-profit umbrella. Its primary purpose is to provide **runtime secret consumption** for applications like ISLAMU Event:
* It connects directly to enterprise secret managers—principally **Infisical** and **Azure Key Vault**, with **HashiCorp Vault** planned.
* It provides explicit, disposable secret access in memory: secrets are borrowed inside scoped lambda callbacks (`SecretMaterial.Use(...)`) using stack/span memory that is wiped via `CryptographicOperations.ZeroMemory` immediately upon exit.
* It eliminates plaintext configuration strings from persistent application configuration models.

### Configurator as the Provisioning Bridge
While SecretKit serves the **runtime consumption plane** inside the application, Configurator serves the **setup and provisioning plane**:
* When an operator configures an application that uses Infisical or Azure Key Vault, Configurator allows them to choose an **optional export sink** to apply those validated secrets directly into the target vault.
* **Native Runtime Boundary (Desktop & Terminal):** The vault push functionality is strictly confined to native desktop and terminal environments where local machine credentials and secure keyrings exist. The Browser WebAssembly client remains 100% offline, static, and air-gapped, retaining its strict zero-network promise.
* **Implementation Strategy:** Configurator utilizes official provider CLIs (`infisical-cli`, `az`) for standard authentication workflows where robust and available, and leverages native `SecretKit.Management` APIs for fine-grained verification and diffing.
* **Explicit Permission & Local Defaults:** By default, Configurator executes zero network requests. The connection to a secret vault occurs solely when the operator explicitly clicks an action in the export phase, providing full visibility and control.

---

## 8. The Interconnected ISLAMU Ecosystem

ISLAMU Configurator does not exist in isolation. It forms a symbiotic triad with ISLAMU’s other strategic initiatives:

```text
 ┌─────────────────────────────────────────────────────────────────────────────┐
 │                         ISLAMU Non-Profit Ecosystem                         │
 └───────┬───────────────────────────────┬─────────────────────────────┬───────┘
         │                               │                             │
         ▼                               ▼                             ▼
┌──────────────────┐           ┌──────────────────┐          ┌──────────────────┐
│   ISLAMU Event   │           │ISLAMU App Store /│          │ISLAMU SecretKit  │
│(Flagship Service)│           │  Link Directory  │          │(Runtime Security)│
└────────┬─────────┘           └─────────┬────────┘          └─────────┬────────┘
         │                               │                             │
         │ Guides self-hosters to        │ Direct "Configure" links    │ Runtime client  │
         │ Configurator for setup        │ for all self-hostable apps  │ for configured  │
         │ and version upgrades          │ in the curated directory    │ secret vaults   │
         │                               │                             │                 │
         └───────────────────────► ┌─────┴───────────────────────┐ ◄───┘
                                   │   ISLAMU Configurator       │
                                   │(Unified Setup & Definition) │
                                   └─────────────────────────────┘
```

1. **Flagship Dogfooding with ISLAMU Event:**
   * ISLAMU Event is the primary consumer and testbed of Configurator.
   * Every deployment profile of Event (standalone single-server, split API/Blazor containers, multi-tenant instances) is defined as a canonical Configurator schema.
   * Event documentation links directly to Configurator, guaranteeing that every Event administrator experiences a seamless, error-free setup.
2. **The ISLAMU App Directory (Link Repository):**
   * A curated, high-integrity directory indexing sovereign, ethical, and Islamic applications across web, mobile, and desktop.
   * Crucially, the directory is an authoritative **link and discovery repository**, deliberately avoiding the massive packaging overhead of binary build farms (like F-Droid or Google Play).
   * Every self-hostable project cataloged in the directory includes a direct deep link: **"Configure with ISLAMU Configurator"**, driving immediate adoption.
3. **Embeddable Configuration Quickstart Widgets:**
   * Documentation sites and project landing pages can embed lightweight interactive configuration widgets (via sandboxed iframes or embeddable web components).
   * New users can configure their deployment directly inside the documentation page without leaving the site, lowering the adoption barrier to near zero.

---

## 9. AI Agent-Assisted Authoring & Autonomous QA Testing

Configurator's formal schema model provides an exceptionally powerful foundation for autonomous AI agents:

### 1. Reverse-Engineering Schemas via Deep Agentic Synthesis & Docker Boot Verification
Upstream open-source maintainers often lack the time to hand-craft configuration schemas. ISLAMU maintains specialized **Agentic Engineering Skills** that execute a 4-stage automated extraction pipeline:

1. **Deep Multimodal Context Ingestion:** The AI agent does not stop at `.env.example`. It crawls the repository's internal documentation markdown, parses Dockerfiles and compose declarations, and follows external documentation links (such as hosted project documentation sites) to capture parameter behaviors, data constraints, and conditional requirements omitted from example files.
2. **Grounded Non-Hallucinating Clarification:** Grounded in *Sidq* (truthfulness), the agent is strictly prohibited from guessing or hallucinating ambiguous defaults. When a variable's runtime behavior is unrecorded or indeterminate, the agent halts and prompts the human operator with structured multiple-choice questions, cataloging missing facts explicitly rather than inventing them.
3. **Physical Localhost Docker Boot Verification Loop:** Before certifying any generated schema, the agent initiates a live local container test loop. It synthesizes a minimal test configuration satisfying only the declared mandatory fields, launches the application container locally (`docker run` or `docker compose up`), and monitors container exit codes, startup logs, and health-check endpoints to physically prove that the application successfully boots and accepts the schema without crashing.
4. **Certified Catalog Contribution:** The physically proven schema is linted via `islamu-configurator definition lint --quality` and contributed to the public catalog via automated pull requests, bootstrapping ecosystem coverage without upstream friction.

### 2. Autonomous Combinatorial QA Matrix Testing
* LLMs and autonomous agents frequently fail when attempting to configure complex multi-service software because they hallucinate invalid environment variable combinations or malformed parameters.
* The CLI command `islamu-configurator definition answer-schema` exports a target-pinned, Draft 2020-12 JSON Schema directly from any definition.
* **Automated Persona Testing:** AI testing agents use this schema to generate dozens of distinct, mathematically valid configuration manifests representing diverse operational personas (e.g., "Minimal SQLite", "High-Security OIDC + Keycloak", "Air-Gapped Local Storage", "High-Concurrency Redis Cluster").
* Agents inject these generated manifests into test portals and automated container suites, verifying that software like ISLAMU Event behaves correctly across all supported configuration matrices before any release is deployed to human operators.

---

## 10. The Ethical Framework: Islamic Value-Sensitive Design (I-VSD)

ISLAMU Configurator is constructed under the rigorous discipline of **Islamic Value-Sensitive Design (I-VSD)**, grounding technical architecture in universal moral principles drawn from Sunni ethical philosophy:

* **Sidq (صِدْق — Absolute Truthfulness):** The system never misleads an operator. Initialized defaults are clearly distinguished from operator-reviewed settings; unvisited optional sections are never painted as completed; and validation states reflect only facts mathematically proven by the engine.
* **Amanah (أَمَانَة — Sacred Custody and Trust):** User secrets are treated with ultimate custody discipline. Candidate secrets in generators are zeroed upon disposal; browser memory is purged upon consent revocation; zero analytics or tracking exist; and the browser runtime remains strictly offline.
* **Bayan (بَيَان — Clarity and Non-Deception):** All hidden defaults, dynamic behaviors, and runtime consequences are explicitly declared (`"If omitted: <defaultText>"`). Software must not obscure its true requirements behind technical jargon.
* **La Darar (لَا ضَرَر — Avoidance of Harm):** Denial-of-service immunity through hand-written, linear-time $O(n)$ format validators; arbitrary backtracking regular expressions (`pattern`) are strictly forbidden to prevent ReDoS vulnerabilities.
* **Adl (عَدْل — Justice and Equitable Access):** Uncompromising commitment to accessibility: full English and Arabic (RTL) parity with left-to-right bidi isolation for technical wire tokens; WCAG 2.2 contrast compliance across all themes; and four explicit density levels that never drop interactive hit targets below operable minimums.
* **Itqan (إِتْقَان — Rigorous Mastery and Excellence):** Delivering well-tested, robust, deterministic software that fulfills its stated mandate completely, rather than deploying fragile, half-finished prototypes.

---

## 11. Conclusion

ISLAMU Configurator is not a mere form generator. It is a **sovereign configuration lifecycle platform** designed to dismantle the barrier to self-hosting, protect community privacy through air-gapped custody, automate complex application upgrades, bridge native secret vaults, and empower autonomous AI testing—all anchored in uncompromising ethical craftsmanship.
