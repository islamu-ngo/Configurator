<!-- ABOUTME: Journal entrypoint: how to record, read, and promote durable findings for ISLAMU Configurator. -->
<!-- ABOUTME: Complements dev/_journal/journal.md + MAJOR_DECISIONS.md with operational policy. -->

# Dev Journal

> **Purpose**: Capture durable findings — bug root causes, non-obvious patterns, system quirks, and major decisions — that should outlive a single session.
>
> **Not for**: Short-term TODOs, tentative ideas, or anything you would not want to read again in six months.

Last Updated: 2026-09-08

---

## 1. Structure & Domain Ledgers

Findings are organized by domain under `dev/_journal/domains/` to minimize agent context overhead:

| Domain | File Path | Focus Area |
|---|---|---|
| **Core & Definitions** | `domains/core-and-definitions.md` (create when an observed finding belongs here) | Admission, canonical identity, validation and composition. |
| **Presentation & Clients** | `domains/presentation-and-clients.md` (create when needed) | Workspace generations, Blazor/Avalonia/terminal controls and private input lifecycle. |
| **Artifacts & Engineering** | `domains/artifacts-and-engineering.md` (create when needed) | Protected native output, packages and repository-native tooling. |
| **Index & Recent** | [`journal.md`](journal.md) | Central index and recent cross-cutting stream (last 30 days). |

---

## 2. When to Record a Finding

Open the matching domain ledger under `domains/` (or `journal.md` for cross-cutting insights) and append an entry when you:

1. Discover a **non-obvious behavior** (e.g., registry refresh identity conflicts, protected-write race behavior, native control viewport quirks).
2. Fix a **bug whose root cause is not visible from the fix alone** (the "what you really needed to know" insight).
3. Make a **design decision** that future contributors will want to understand, but that is not yet mature enough to promote to `docs/internal/GOVERNANCE.md`.
4. Confirm or refute an **assumption from a skill/agent/rule** that was ambiguous.

If your finding is a **system-wide decision** (changes owner boundaries, affects all clients, sets a new invariant), record it in `MAJOR_DECISIONS.md` instead.

---

## 3. How to Record a Finding

Append to the relevant domain ledger under `domains/` or [`journal.md`](journal.md) using [`FINDING_TEMPLATE.md`](FINDING_TEMPLATE.md):

- Date prefix in the form `[YYYY-MM-DD Europe/Brussels]`.
- One blank line between entries.
- At least one referenced file path or commit SHA.
- Grounded in testable, observed evidence.

---

## 4. Promotion Rules (Journal → Primary documentation)

A finding is not meant to live in the journal forever. See [`PROMOTION_RULES.md`](PROMOTION_RULES.md) for the promotion policy.

| Signal | Promote To |
|---|---|
| Rule that applies to new code going forward | `docs/internal/QUICK_REFERENCE.md` (non-inferable rule) OR `.agents/rules/*.md` entry |
| Pattern that teaches a way of working | `.agents/skills/*/SKILL.md` |
| Architectural decision | `dev/_journal/MAJOR_DECISIONS.md` + ADR under `docs/internal/adr/` |
| Discovery that informs governance / conventions | `docs/internal/GOVERNANCE.md` |
| One-off debugging lesson | **Stays in domain journal** — no promotion needed |

---

## 5. Related

- [`journal.md`](journal.md) — central index & recent findings.
- [`MAJOR_DECISIONS.md`](MAJOR_DECISIONS.md) — system-wide decisions.
- [`FINDING_TEMPLATE.md`](FINDING_TEMPLATE.md) — authoritative entry format.
- [`PROMOTION_RULES.md`](PROMOTION_RULES.md) — journal-to-docs promotion policy.
- [`AGENTS.md`](../../AGENTS.md) §9 — Memory & Findings.
