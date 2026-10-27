# Vision and Approach

> **Audience:** Contributors | AI agents
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-27
> **Source Anchors:** `docs/internal/universal-configurator.md`, `docs/internal/go-to-market.md`, `AGENTS.md`

Welcome to ISLAMU Configurator! This document outlines the core vision, architectural principles, and strategic approach governing the project. Whether you are adding a new feature, maintaining a client surface, or contributing an application definition, this guide provides the context behind our design choices.

## The Configuration Experience Problem

Configuring self-hosted and cloud-native software remains one of the highest friction hurdles in computing. Today, operators must hand-edit complex `.env` files, intricate JSON configurations, and deeply nested YAML manifests. Upstream projects often expose dozens or hundreds of environment variables, implicit dependencies between settings, and delicate security parameters without progressive guidance, unified validation, or safe defaults.

Existing solutions in the ecosystem stop at frustrating boundaries:
- **Platform-Coupled Stores:** Systems like RunTipi, Umbrel, CapRover, and CasaOS offer guided forms, but couple configuration tightly to their specific Docker Compose templates and orchestration platforms.
- **Privileged Scaffolding:** Tools such as Backstage and YunoHost provide flexible parameter workflows, but rely on executable lifecycle scripts, crossing dangerous trust boundaries next to production secrets.
- **Application-Specific Wizards:** Applications such as Home Assistant, Keycloak, and Grafana provide excellent configuration experiences, but restrict their schemas to their own application silos.

Operators are left trapped between brittle, manual text editing and monolithic platforms that dictate where and how their software must run.

## Core Goals

Configurator solves this dilemma through seven concrete, focused goals:

1. **Portable, Declarative Definitions:** Applications publish a single, non-executable definition (`SetupDefinition`) describing questions, constraints, relevance rules, and artifact projections without runtime plugins or scripts.
2. **Multi-Surface Rendering Parity:** Operators experience identical validation semantics, question ordering, and defaults across Machine CLI, Terminal TUI (`Terminal.Gui`), Avalonia Desktop, and static WebAssembly (`Blazor WASM`) browsers.
3. **Strict Target Authority Separation:** Configurator is a preparation-only engine. It renders native configuration artifacts (`.env`, JSON, YAML) with high fidelity while leaving runtime enrollment, host administration, and apply actions to the target application.
4. **Security-First Secret Custody:** Keep secrets transient in client memory with per-workspace operator consent, masked display, cryptographic generation, zero telemetry, and Linux create-only protected file writing.
5. **Upstream Ownership via Deep Linking:** Maintainers keep definitions directly within their own repositories and embed "Configure Visually" deep links in their documentation, bypassing centralized store bottlenecks.
6. **Deterministic Machine & AI Interfaces:** CLI commands output structured JSON envelopes, sanitized-view SHA-256 digests, and value-free diagnostics designed for automated pipelines and AI agents.
7. **Conformance-Backed Version Integrity:** Support exact declared application versions and deterministic artifact generation validated by checked golden fixtures rather than runtime version heuristics.

## The Chosen Approach

Configurator is architected as an **offline-first, deployment-neutral configuration runtime**. Its foundation rests on five architectural pillars:

### 1. Bounded Data-Only Definitions
Definitions are purely declarative JSON or YAML documents admitted by `Configurator.Core`. They declare fields, types, options, validation rules, relevance expressions, and output file projections. Definitions cannot execute code, load external assemblies or WASM modules, call remote endpoints, or invoke shell commands.

### 2. Multi-Surface Rendering
The configuration experience is completely decoupled from the rendering surface:
- **CLI (`Configurator.Cli`):** Headless, scriptable execution with machine receipts and stdin/file piping.
- **Terminal (`Configurator.Terminal`):** Fast, keyboard-driven text user interface built on `Terminal.Gui` v2 for remote shells and headless servers.
- **Desktop (`Configurator.Desktop`):** Full-featured native GUI built on Avalonia for local operator workflows.
- **Browser (`Configurator.Browser`):** Zero-install, static client-side Blazor WebAssembly running entirely in the operator's browser.

> [!NOTE]
> All surfaces share the same core validation logic, state machine (`Configurator.Presentation`), and projection rules. An answer valid in the terminal is equally valid in the browser or CLI.

### 3. Offline-First Architecture
Configurator operates entirely without a backend server or mandatory cloud connectivity. Definitions, validation, and artifact composition run locally in memory. In the browser, Blazor WebAssembly executes within the local sandbox, loading static assets via standard service workers.

### 4. No Executable Plugins
Unlike scaffolding platforms that dynamically load plugins to generate files, Configurator uses a built-in projection compiler capable of emitting `.env`, JSON, and YAML formats directly from declarative mappings.

### 5. Target Authority Separation
Configurator stops strictly at the artifact boundary. It writes files to designated local paths (or provides them for download/copy in the browser) and steps aside. It never restarts containers, invokes `docker compose up`, issues HTTP management calls, or touches systemd units.

## Why This Approach? Competitive Landscape Analysis

Our design decisions directly reflect lessons from existing self-hosting and configuration management platforms:

| Platform | Architectural Model | Limitation Configurator Resolves |
|---|---|---|
| **RunTipi / Umbrel** | Platform-bound app stores with Docker Compose manifests | Configurator remains deployment-neutral; it generates native configuration files for software running anywhere (bare metal, systemd, containers, or Kubernetes). |
| **Backstage / YunoHost** | Scaffolding workflows with executable actions and lifecycle scripts | Executable scripts next to production secrets present severe supply-chain risks. Configurator enforces strictly non-executable data definitions. |
| **CapRover / Portainer** | Container templates with variable substitution | Configurator provides deep structural typing, conditional relevance, multi-file artifact projections, and version compatibility tracking. |
| **Home Assistant Blueprints** | Declarative UI selectors and inputs for smart home automations | Home Assistant controls its own domain; Configurator generalizes this declarative UI model across unrelated software applications. |
| **Authentik / Grafana** | Native provisioning manifests (YAML / JSON blueprints) | Configurator treats these declarative formats as first-class target artifacts rather than trying to invent a competing configuration syntax. |

> [!TIP]
> Configurator's competitive advantage is not merely rendering a form from a schema—it is the combination of **portable application definitions + native-format generation + deterministic CLI + multi-surface UX + confidential secret handling + deployment neutrality**.

## Key Product Boundaries

To maintain focus and avoid architectural rot, Configurator enforces strict scope boundaries across different classes of "configuration":

| Configuration Class | Examples | Configurator Responsibility | Boundary Enforcement |
|---|---|---|---|
| **Bootstrap Configuration** | `.env`, database host/port, root URLs, SMTP host, HCL settings | **First-class core mission.** Guided collection, validation, and native projection. | Pure declarative compilation to target file formats. |
| **Declarative Application State** | Keycloak realms, Authentik blueprints, Grafana data sources | **First-class, version-bounded.** Projected directly into target JSON/YAML structures. | Schema-bound generation only; no live cluster reconciliation. |
| **Secrets & Credentials** | Database passwords, API keys, JWT secrets, TLS private keys | **Collect or generate locally.** Support masked input, local entropy generation, and secret-deferred drafts. | Transient memory custody, per-workspace consent, create-only Linux output, zero telemetry. |
| **Runtime Mutable State** | User accounts, active sessions, uploaded assets, job histories | **Strictly out of scope.** Belongs entirely to the running application. | Refuse runtime state synchronization, database connections, or management APIs. |
| **Backup & Restore State** | Volume dumps, database snapshots, full instance backups | **Explicitly out of scope.** Configuration is not instance state. | Never pretend generated configuration constitutes a system backup. |

> [!WARNING]
> Never blur the line between configuration generation and runtime administration. Configurator generates the exact files a software release expects; the host application owns deployment and semantic acceptance.

## Deeper Reading

For contributors looking to explore the detailed strategic and architectural background:
- [Universal Configurator Strategy](universal-configurator.md): CTO review, comprehensive market research, and external precedent analysis.
- [Go-to-Market Strategy](go-to-market.md): Deep-linking model, maintainer adoption dynamics, and ecosystem positioning.
- [Design Alternatives](DESIGN_ALTERNATIVES.md): Detailed record of considered and rejected technical approaches.
- [Architecture Overview](ARCHITECTURE.md): Component diagrams, data flows, and layering rules.
- [Core Definition Contract](CORE_DEFINITION_CONTRACT.md): The normative specification for definition admission and composition.
- [Quick Reference](QUICK_REFERENCE.md): Invariant rules and critical guidelines for all contributors.
