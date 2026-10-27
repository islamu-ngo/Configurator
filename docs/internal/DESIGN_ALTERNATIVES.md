# Design Alternatives and Rejected Approaches

> **Audience:** Contributors | AI agents
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-27
> **Source Anchors:** `docs/internal/universal-configurator.md`, `docs/internal/adr/`, `docs/internal/QUICK_REFERENCE.md`

Configurator’s architecture prioritizes operator privacy, deterministic outputs, offline independence, and maintainer sovereignty. Every design choice balances usability with strict security boundaries.

This document records the major design alternatives evaluated during system design, their status (**Adopted**, **Rejected**, or **Partially Used**), and the architectural rationale and evidence justifying each outcome.

---

## 1. Executable Plugins vs Data-Only Definitions

| Option | Status | Trade-offs & Rationale | Evidence / Decision Anchor |
|---|---|---|---|
| **Executable Plugins / Dynamic Scripts** | **Rejected** | Scaffolding tools like Backstage and YunoHost allow executable actions and lifecycle scripts. Because Configurator handles database passwords, secret keys, and deployment configs, loading arbitrary third-party code introduces severe supply-chain risks. | [universal-configurator.md](universal-configurator.md)<br/>[QUICK_REFERENCE.md](QUICK_REFERENCE.md) (Rule 6) |
| **Declarative Data-Only Definitions** | **Adopted** | Definitions are bounded, typed JSON/YAML documents admitted by `Configurator.Core`. The engine provides built-in projections for `.env`, JSON, and YAML formats without runtime plugins. | [ADR: Definition Boundary](adr/ADR-definition-runtime-boundary.md)<br/>[CORE_DEFINITION_CONTRACT.md](CORE_DEFINITION_CONTRACT.md) |

---

## 2. Server-Side Processing vs Client-Side WebAssembly

| Option | Status | Trade-offs & Rationale | Evidence / Decision Anchor |
|---|---|---|---|
| **Centralized Backend Processing** | **Rejected** | Transmitting operator answers to a hosted service exposes entered credentials, database topologies, and network layouts over the network to a central server. | [universal-configurator.md](universal-configurator.md)<br/>[ADR: Browser Session Trust](adr/ADR-browser-session-trust-and-export.md) |
| **Client-Side WebAssembly** | **Adopted** | The browser experience runs static Blazor WebAssembly entirely inside the operator's browser sandbox. All answers, state mutations, and artifact compilations execute on the client device. | [ADR: Browser Session Trust](adr/ADR-browser-session-trust-and-export.md)<br/>[LAUNCH_CONTRACT.md](LAUNCH_CONTRACT.md) |

---

## 3. JSON Schema Direct Rendering vs Custom Definition Vocabulary

| Option | Status | Trade-offs & Rationale | Evidence / Decision Anchor |
|---|---|---|---|
| **Direct JSON Schema Form Generation** | **Rejected** | Generic JSON Schema renderers lack essential configuration domain semantics: multi-artifact projections, sensitivity floors, secret-masking hints, experience modes (Essential/Standard/All), and exact application version bounds. | [universal-configurator.md](universal-configurator.md)<br/>[ADR: Maintainer Authoring](adr/ADR-maintainer-authoring-and-canonical-distribution.md) |
| **JSON Schema for Structural Validation** | **Partially Used** | JSON Schema is used as a meta-schema to validate handwritten maintainer definitions and machine CLI envelopes, but not as the direct runtime UI driver. | [eng/configurator/](../../eng/configurator/)<br/>[schemas/](../../schemas/) |
| **Custom Declarative Definition Vocabulary** | **Adopted** | A purpose-built, flat envelope (`SetupDefinition`) specifically models configuration concerns: questions, conditions, collections, and deterministic native file projections. | [CORE_DEFINITION_CONTRACT.md](CORE_DEFINITION_CONTRACT.md)<br/>[ADR: Definition Boundary](adr/ADR-definition-runtime-boundary.md) |

---

## 4. Mandatory Catalog vs Optional Store

| Option | Status | Trade-offs & Rationale | Evidence / Decision Anchor |
|---|---|---|---|
| **Mandatory Central App Store** | **Rejected** | Central registries create severe governance bottlenecks, vendor lock-in, and single points of failure. They force upstream maintainers to seek ISLAMU approval for every update. | [universal-configurator.md](universal-configurator.md)<br/>[go-to-market.md](go-to-market.md) |
| **Runtime Repository Crawling** | **Rejected** | Dynamically scraping GitHub/GitLab repositories at runtime creates API rate-limit issues, requires network access, and risks serving unverified definitions. | [ADR: Optional Store](adr/ADR-optional-definition-store.md)<br/>[DEFINITION_STORE.md](DEFINITION_STORE.md) |
| **Optional Static Store with Direct Deep Links** | **Adopted** | Maintainers keep definitions in their own repositories and deep-link directly into Configurator via URL hashes. An optional static catalog index is compiled at release time for discovery. | [ADR: Optional Store](adr/ADR-optional-definition-store.md)<br/>[go-to-market.md](go-to-market.md) |

---

## 5. Application-Specific Runtime vs Deployment-Neutral Utility

| Option | Status | Trade-offs & Rationale | Evidence / Decision Anchor |
|---|---|---|---|
| **Orchestrator-Bound Engine (Docker/Compose-only)** | **Rejected** | Precedents like RunTipi, Umbrel, and CapRover bind configuration directly to Docker Compose. This excludes bare-metal, systemd, Kubernetes, Podman, and standalone service deployments. | [universal-configurator.md](universal-configurator.md)<br/>[go-to-market.md](go-to-market.md) |
| **Deployment-Neutral Configuration Utility** | **Adopted** | Configurator focuses purely on preparing standard configuration files (`.env`, JSON, YAML). It remains agnostic about whether the application runs in Docker, Kubernetes, or native systemd. | [ADR: Definition Boundary](adr/ADR-definition-runtime-boundary.md)<br/>[universal-configurator.md](universal-configurator.md) |

---

## 6. Target Apply Authority vs Preparation-Only Boundary

| Option | Status | Trade-offs & Rationale | Evidence / Decision Anchor |
|---|---|---|---|
| **Direct Deployment & Apply Authority** | **Rejected** | Applying configuration requires elevated host permissions, SSH/daemon access, or API management credentials. Combining preparation with apply authority creates massive attack surfaces. | [universal-configurator.md](universal-configurator.md)<br/>[QUICK_REFERENCE.md](QUICK_REFERENCE.md) (Rule 6) |
| **Preparation-Only Boundary** | **Adopted** | Configurator prepares and stages validated files locally. On Linux, it uses create-only, no-replace protected writes. Applying, restarting, and validating runtime acceptance remains with the target host. | [ADR: Definition Boundary](adr/ADR-definition-runtime-boundary.md)<br/>[ADR: CLI Command Runtime](adr/ADR-cli-command-runtime.md) |

---

## 7. Interactive Server vs WebAssembly Interactivity

| Option | Status | Trade-offs & Rationale | Evidence / Decision Anchor |
|---|---|---|---|
| **Blazor Interactive Server** | **Rejected** | Server-side Blazor transmits every keystroke, focus event, and secret value over a WebSocket circuit to server memory. It requires constant network connectivity and drops state on disconnection. | [ADR: Browser Session Trust](adr/ADR-browser-session-trust-and-export.md) |
| **Blazor Interactive Auto** | **Rejected** | Auto mode uses Server interactivity initially while WASM downloads in the background, creating unpredictable shifts in where secrets are processed between visits. | [ADR: Browser Session Trust](adr/ADR-browser-session-trust-and-export.md) |
| **Standalone Interactive WebAssembly** | **Adopted** | The application is published as a standalone static WASM client. Interactivity, state management, and artifact composition occur exclusively within the client browser thread. | [ADR: Browser Session Trust](adr/ADR-browser-session-trust-and-export.md)<br/>[LAUNCH_CONTRACT.md](LAUNCH_CONTRACT.md) |

---

## Summary of Authority Boundaries

> [!IMPORTANT]
> The common thread across all design choices is **containment of authority**:
> - Definitions have **data authority**, not execution authority.
> - Configurator has **preparation authority**, not apply authority.
> - The browser client has **transient local custody**, not persistent secret authority.
> - The catalog has **discovery value**, not gatekeeping authority.
