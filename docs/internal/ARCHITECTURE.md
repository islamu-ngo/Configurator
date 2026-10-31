# Architecture Overview

> **Audience:** Contributors | AI agents
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-27
> **Source Anchors:** `PROJECTS.md`, `src/`, `docs/internal/CORE_DEFINITION_CONTRACT.md`, `docs/internal/PRESENTATION_STATE.md`

ISLAMU Configurator is a layered, offline-first configuration preparation utility.
It evaluates local answers against an admitted data-only definition and prepares
candidate native files. The target application independently decides whether
those files are semantically valid, authorized and safe to apply.

This page explains owners and control flow. [Governance](GOVERNANCE.md#dependency-rules)
owns the compiled dependency policy; [PROJECTS.md](../../PROJECTS.md) owns project
locations and verification commands. [Support and evidence](STATUS.md) separates
implemented mechanisms from actual-host qualification.

## Component Architecture

The eight product projects under `src/` have the following dependency boundaries.
This is an assembly-owner map, not a sequence of calls. Native clients can consume
Core through transitive project references; the compiled-edge verifier described
in Governance is authoritative.

```mermaid
graph TD
    subgraph Clients ["Presentation Clients & Static Transport"]
        CLI["Configurator.Cli<br/>(Machine Automation)"]
        TERM["Configurator.Terminal<br/>(Terminal.Gui v2 TUI)"]
        DESK["Configurator.Desktop<br/>(Avalonia GUI)"]
        BROWSER["Configurator.Browser<br/>(Static Blazor WASM)"]
        HOST["Configurator.Browser.Host<br/>(Static File Container)"]
    end

    subgraph Shared ["Shared Coordination & Protected I/O"]
        PRES["Configurator.Presentation<br/>(Workspaces, Forms & Generations)"]
        ART["Configurator.Artifacts<br/>(Protected Native Writes)"]
    end

    subgraph Engine ["Domain Core"]
        CORE["Configurator.Core<br/>(Definitions, Validation & Composition)"]
    end

    CLI --> CORE
    CLI --> ART

    TERM --> PRES
    TERM --> ART
    TERM --> CORE

    DESK --> PRES
    DESK --> ART

    BROWSER --> PRES
    BROWSER --> CORE

    PRES --> CORE
    ART --> CORE

    classDef host fill:#eee,stroke:#999,stroke-dasharray: 5 5;
    class HOST host;
```

> [!NOTE]
> `Configurator.Browser.Host` is an optional, decoupled static file server. It has zero product dependencies, contains no answer-processing logic, and serves only compiled public assets.

## Data Flow Pipeline

Interactive clients coordinate the following preparation flow through
Presentation. CLI invokes Core and Artifacts directly; it does not instantiate
the interactive workspace or depend on Presentation. Browser export uses its
own consent-gated browser adapter, not the native publisher.

```mermaid
flowchart LR
    DEF["SetupDefinition<br/>(YAML / JSON)"] -->|"Admission & Digest"| CORE["Configurator.Core<br/>(Admission / Validation)"]
    ANS["SetupAnswerSnapshot<br/>(Immutable Answers)"] -->|"Answers"| CORE
    CORE -->|"ValidationFacts & Diagnostics"| PRES["Configurator.Presentation<br/>(Workspace & FormModel)"]
    PRES -->|"Single-Pass Preparation"| COMP["DefinitionBundleComposer<br/>(Composition Pipeline)"]
    COMP -->|"Restricted Bytes"| ART["Configurator.Artifacts<br/>(Protected Publication)"]
    ART -->|"Create-Only File"| DISK[("Native Filesystem<br/>(.env / JSON / YAML)")]
    COMP -.->|"Blob / Clipboard (Transient)"| BROWSER_EXP["Browser Export<br/>(Consented Download)"]

    classDef transient fill:#fef,stroke:#d08;
    class BROWSER_EXP transient;
```

1. **Definition Admission:** `SetupDefinitionCodec` parses JSON/YAML, computes the canonical SHA-256 digest, and snapshots metadata into an immutable `SetupDefinition`.
2. **Answer Snapshotting:** `SetupAnswerSnapshot` captures user inputs into an immutable, clone-isolated structure.
3. **Core Validation:** `DefinitionValidator` evaluates requirements, constraints, and activation conditions, emitting value-free `Diagnostics`.
4. **Presentation Coordination:** `DefinitionFormModel` manages transaction gates, edit revisions, and section projections.
5. **Single-Pass Composition:** `DefinitionBundleComposer` prepares all active artifacts in declared order from one validated snapshot.
6. **Protected Publication:** `ProtectedArtifactBundlePublisher` executes create-only Linux writes, recording per-file outcomes in an immutable receipt.

Preparation and publication are separate operations. A successful preparation
does not establish a filesystem write, and a write does not establish target
acceptance. Bundle publication preflights destinations, then installs files in
order with per-file create-only semantics. A later failure can leave an installed
prefix; there is no whole-bundle rollback. See
[protected output](PROTECTED_ARTIFACTS.md) for receipts and recovery.

## Key Abstractions

| Abstraction | Owning Project | Role & Responsibility |
|---|---|---|
| `SetupDefinition` | `Configurator.Core` | Immutable admitted definition containing fields, conditions, artifact projections, and canonical digest. |
| `SetupAnswerSnapshot` | `Configurator.Core` | Thread-safe, immutable snapshot of user answers; isolates business logic from UI state. |
| `DefinitionValidator` | `Configurator.Core` | Pure evaluation engine returning readiness, field states, and value-free diagnostics. |
| `DefinitionArtifactComposer` | `Configurator.Core` | Generates deterministic native artifact bytes (`.env`, JSON, YAML) from validated answers. |
| `DefinitionDraftArtifactComposer` | `Configurator.Core` | Prepares secret-deferred draft artifacts with explicit completion pointers for local manual completion. |
| `DefinitionFormModel` | `Configurator.Presentation` | Form facade managing edit revisions, typed presence, and section projections under a mutation gate. |
| `SetupPresentationWorkspace` | `Configurator.Presentation` | Manages atomic operation settlement, generation fencing, and subscriber notifications across operations. |
| `ProtectedArtifactBundlePublisher` | `Configurator.Artifacts` | Executes Linux create-only, no-replace file writes with per-file status recording and receipt generation. |
| `BoundedFileInput` | `Configurator.Artifacts` | Asynchronous file reader isolated from UI dispatchers with strict size limits and buffer disposal. |
| `SetupCliApplication` | `Configurator.Cli` | Spectre-based CLI command runner emitting pre-sized JSON receipts with sanitized view digests. |

## Layer Rules & Dependency Constraints

Contributors and automated agents must adhere to strict architectural dependency rules:

- **`Configurator.Core` owns semantics without adapter dependencies:** Admission,
  validation and projection are in-memory mechanisms. One bounded exception is
  the reviewed Linux directory-source acquisition in
  `Configurator.Core.Composition`, implemented by
  `SetupCompositionDirectoryReader`. It does not permit arbitrary ambient I/O,
  network access, an Artifacts dependency or a browser-portability claim.
  [Relocation follow-up](../../dev/backlog/core-directory-source-boundary.md)
  records the intended separation; the exception has not already been removed.
- **`Configurator.Presentation` is Browser-Safe:** It depends only on `Core`. It coordinates in-memory UI state, form indexes, and workspace generations. It must **never** reference `Artifacts` or perform filesystem I/O.
- **`Configurator.Artifacts` Owns Native File Access:** It depends only on `Core`. It contains native file operations and enforces Linux create-only permissions.
- **`Configurator.Browser` Never Touches `Artifacts`:** It references `Presentation` and `Core`. File export occurs via deliberate browser downloads and Blob URLs; it has no access to native file protection.
- **Native Clients Coordinate Shared Layers:** `Configurator.Terminal` and `Configurator.Desktop` reference `Presentation`, `Artifacts`, and `Core` to provide native experiences. `Configurator.Cli` references `Artifacts` and `Core`.
- **`Configurator.Browser.Host` is Standalone:** It has zero project references to any `Configurator.*` library, serving purely as a static asset container.

> [!CAUTION]
> Never introduce circular references, invert layer boundaries, or import target application assemblies (such as Event or Keycloak) into Configurator.

## Build and Verification

For exact build preconditions, targeted test commands, and package validation procedures across all projects, consult [PROJECTS.md](../../PROJECTS.md).
