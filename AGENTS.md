# AGENTS.md - Agent Contribution Contract

> **This is the required entrypoint for every AI tool contributing to this repository.**
> Last Updated: 2026-10-09

---

## 1. The Contribution Contract (Read First)

Every change must answer these eight questions **before editing any file**:

| # | Question | Source of Truth |
|---|---|---|
| 1 | What kind of change is this? (the *intent*) | [`.agents/contract/intents.yaml`](.agents/contract/intents.yaml) |
| 2 | Which rules are authoritative? | [`docs/internal/QUICK_REFERENCE.md`](docs/internal/QUICK_REFERENCE.md) + [`.agents/rules/`](.agents/rules/) |
| 3 | Which files must be read first? | The intent's `must_read_docs` field |
| 4 | Which files may be changed? | The intent's `paths_in_scope` field |
| 5 | Which tests must run at minimum? | The intent's `minimum_tests` field and `PROJECTS.md` |
| 6 | Which docs must be updated? | The intent's `docs_to_update` field |
| 7 | Which PR checklist applies? | The intent's `pr_checklist` field and `AI_REVIEW.md` |
| 8 | What is forbidden? | The intent's `forbidden_without_approval` + target invariants |

Explicit user instructions override repository guidance. Work directly in this
checkout when instructed; never create a branch, worktree or commit without
authorization. Preserve unrelated changes and the user-owned design/strategy
documents `docs/internal/universal-configurator.md` and
`docs/internal/go-to-market.md`.

---

## 2. Authoritative Artifacts (Single Source of Truth)

| Concern | Authoritative File | Purpose |
|---|---|---|
| AI agent contract | `AGENTS.md` (this) | Every agent starts here |
| Project map & bounds | `PROJECTS.md` | Actual project roles, protected paths and targeted commands |
| Pre-submission review | `AI_REVIEW.md` | Structured self-review before submission |
| Invariant reference | `docs/internal/QUICK_REFERENCE.md` | Offline product and engineering hard constraints |
| Governance | `docs/internal/GOVERNANCE.md` | Conventions, naming and metadata policy |
| Intent registry | `.agents/contract/intents.yaml` | Machine-readable task mapping |
| Operations | `docs/internal/OPERATIONS.md` | Native execution and verification evidence |
| Documentation ownership | `docs/internal/DOCUMENTATION_ARCHITECTURE.md` | Public guidance versus technical source anchors |
| Durable findings | `dev/_journal/` | Decisions and non-obvious reusable patterns |
| Transfer boundary | `docs/internal/extraction/FOUNDATION.md` | First-party transfer and source-system authority map |

Configurator is an MIT-licensed offline configuration preparation utility.
Copied Event backend examples remain reference-only: no Event database, API,
HAL, BFF, tenancy, Aspire, AGPL covenant or production deployment requirement
governs this utility. Target instructions and actual projects win. Do not
discard reusable source guidance merely because an example belongs to Event.

---

## 3. Cold-Start Flow (Zero-Knowledge Agent)

> **Development Mode Notice:** This repository is in active pre-release
> development. Breaking changes are first-class when they simplify the
> architecture; do not add obsolete aliases, compatibility adapters or legacy
> baggage without an actual contract requirement.

1. **CLASSIFY:** Find the matching intent and resolve its `criticality` block:
   tier, intake mode, exploration protocol, testing strategy and review protocol.
2. **ALIGN:** Fully specified approved tasks continue autonomously. Clarify only
   material ambiguities. Definition admission, public/private projections,
   protected output and authority/trust changes require adversarial invariant
   analysis; use the criticality guardrail for relevant security/privacy work.
3. **LOAD:** Read `must_read_docs` and matching path rules. Load only the skill
   whose description matches the actual work. Backend-only skills do not create
   missing Configurator layers or services.
4. **EDIT:** Work within `paths_in_scope`. Keep Core independent and adapters
   thin. Use native editing tools and repository conventions.
5. **VERIFY & REVIEW:** Establish the required failing invariant before its
   security/privacy fix. Finish coherent source/caller/docs/regression slices
   continuously, then verify the affected owner at phase exit. Preserve passes;
   rerun only failed or newly affected checks.
6. **TEACH:** Explain what changed, its control/data flow and the mechanisms that
   produce the result, with exact executed verification evidence.
7. **ESCALATE:** A real requirement conflict needs a self-contained decision
   brief. Name the file and requirement rather than silently inventing an
   exception. Existing explicit task authorization does not require routine
   reapproval.

---

## 4. Rule Authority Order (Conflict Resolution)

1. Explicit user instructions and the target contribution contract.
2. Critical target invariants below and `QUICK_REFERENCE.md`.
3. `GOVERNANCE.md` for coding/metadata conventions.
4. Matching target path rules; deeper rules win within their scope.

Historical source-system guidance is evidence, not authority over absent target
owners. Harness pointer files must not introduce a second policy.

---

## 5. Critical Rules (Project-Specific Invariants)

1. **Independent Runtime:** All authored projects, assemblies and namespaces use
   `Configurator.*`. Core has no Event backend, domain, wire or database
   dependency. Preserve third-party identities such as `ISLAMU.Terminal.Gui`.
2. **Data-Only Definitions:** Admit bounded fields, activation, validation and
   output vocabulary before use. No executable definition plugins. Browser
   definition retrieval admits only bounded, credential-free public HTTPS
   sources pinned to canonical content before answer collection; no arbitrary
   remote launch authority or answer-bearing fetch is permitted.
3. **Canonical Identity:** Family/revision identity binds canonical content.
   Publisher labels and GUIDs are not authentication or curation evidence.
   Active sessions retain their admitted revision and digest; conflicting
   previously admitted revisions reject even after registry removal.
4. **Browser Custody Boundary:** Browser configuration answers are transient and
   local to the workspace. Secret entry requires explicit per-workspace consent;
   revocation clears secret leaves and prepared output. Public asset/definition
   caches never contain answers or artifacts. Restricted copy/download is a
   deliberate user action, with no native protected-save guarantee. No telemetry,
   target authentication, native I/O or answer-bearing network requests belong
   in that browser. Secret-deferred drafts never imply complete readiness.
5. **Protected Output:** Native answer-derived artifacts remain restricted.
   Verified Linux writes are create-only; existing-file/concurrent conflicts do
   not overwrite. Unsupported protected-save hosts reject without ordinary-file
   fallback.
6. **Shared State Ownership:** Presentation owns immutable workspace snapshots
   and operation generations. Cancellation, edits, replacement and disposal
   invalidate stale preparation/import/save results.
7. **Target Authority:** Preparation does not grant host acceptance,
   administration or apply permission. Target semantic validation and
   authorization remain with the target application; Event live transport
   remains in Event.
8. **Self-Documenting Code & Natural Headers:** Source uses clear names, folder
   structure and standard XML `<summary>` comments where behavior is
   non-inferable. Do not generate new `ABOUTME:` source headers. Documentation
   uses natural Markdown metadata or standard frontmatter.
9. **Generated Artifacts:** Schemas, ratchets and dependency locks are generated
   outputs. Fix their owning source/generator/reference and regenerate natively;
   never hand-edit them to make a gate pass.
10. **IP And Outbound Licensing:** The Steward authorized MIT transfer of
    ISLAMU-owned source only. This does not relicense third-party expression.
    Preserve component identity, terms, approval, patch, hashes, SBOM and
    notices. Externally informed implementation uses a source-free functional
    handoff and independently designed structure. The 154-file unlicensed
    Evolve Demo corpus is excluded.
11. **Tooling Boundary:** Use native read/apply_patch and POSIX Bash/native .NET
    tools. No ad-hoc Python, JavaScript or Node task scripts. Persistent tools
    belong in `eng/` as C# file-based or Bash tools; `.ci/scripts/` is for CI.
12. **Secrets Isolation:** Never embed credentials, real connection strings,
    private keys or actual private answers in source, docs or fixtures.
    Reproduction uses synthetic values or approved injected authorities.
    Diagnostics must not echo secret values, restricted content or private
    paths.
13. **Test Quality Over Quantity:** Guard business/privacy/concurrency
    invariants, not framework internals, mock-call mirroring or source/CSS text.
    Expected values use independent specifications. Do not pin prose or prompts
    with tests; machine-consumed schemas/fields remain testable.
14. **Dual Documentation Parity:** Operator-facing definition, command,
    configuration or packaging changes update the public guide and technical
    source anchor in the same increment, without duplicated raw configuration.
15. **Self-Contained Interaction:** Questions, reports and decisions must be
    actionable without opening the implementation plan. Never use bare
    phase/task IDs instead of descriptive component names.
16. **No Warning Suppressions:** Never suppress compiler/analyzer diagnostics
    with pragmas, attributes or test weakening to force green. Fix attributable
    causes; quarantine unrelated baseline problems.
17. **Execution Continuity:** Approved tasks continue end-to-end without routine
    stage pauses. Pause only for material ambiguity, irreversible actions,
    unexpected scope creep or explicit user direction.
18. **Targeted Evidence:** Physically execute the relevant checks and record
    commands/results. A package build is not proof of every suite. Never invent
    green evidence.
19. **Publication Authority:** Do not transfer Event credentials, production
    signers, trust activation, publication receipts or release history. Generic
    validation remains available; signed publication fails closed until
    Configurator establishes its own promoted authority.

---

## 6. Task-Routing Entrypoints

| Starting Point | Go To |
|---|---|
| New request | `.agents/contract/intents.yaml` triggers |
| Known path | `.agents/rules/` matching paths |
| Pattern/skill | `.agents/skills/` matching description |
| Build/test | `PROJECTS.md` and `docs/internal/TESTING.md` |
| Browser launch | `docs/internal/LAUNCH_CONTRACT.md` |
| Native output | `docs/internal/PROTECTED_ARTIFACTS.md` |
| Agent operations | `.agents/CONTEXT_ENGINEERING.md` |
| PR review | `AI_REVIEW.md` and matching review skill |
| Durable finding | `.agents/skills/finding/SKILL.md` |

---

## 7. Targeted Fetch And Reuse Rule

When a task touches a documented topic, retrieve the **smallest relevant heading,
symbol or bounded range once**. Follow `.agents/CONTEXT_ENGINEERING.md`.

- Use this contract from injected session context; do not reread unchanged
  instructions already present.
- Resolve the matching intent entry, not the full registry.
- Load required headings, matching skill routers and matching path rules only.
- Prefer graph, outline, symbol, heading and diff retrieval over whole files.
- Keep an in-session `path + heading/symbol + revision` ledger. Reread only
  after the source changes, a concrete decision lacks evidence or evidence
  contradicts the prior reading.
- For cross-owner/security work, use bounded impact/caller/callee/test evidence
  before editing. Use an available knowledge graph first; fall back when it
  does not cover the target. Never invent graph results.
- Before external design/dependency research, load the clean-room skill and
  relevant IP governance section; ordinary official API documentation does not
  require the full legal chain.

---

## 8. Verification Baseline

Use SDK 10.0.302, MTP, central versions, generated locks, deterministic builds
and actual compiler/analyzer diagnostics. On this workstation set
`TMPDIR="$HOME/.cache/agent-tmp"`.

Establish the relevant code baseline once before product edits. Do not rerun
unchanged baseline checks. Check existing changes and branch state first;
never switch branches or create a worktree against the user's instruction to
work directly here.

**Progressive Verification**

1. **Inner Loop:** Fast in-memory Core/Presentation invariants, sliced to the
   concrete affected class. Subscribe to exact async events with bounded
   timeouts; do not use timing sleeps or polling luck.
2. **Phase Exit:** One targeted Release build and the affected classes/owner.
   No unrelated product suites for tooling changes and no Event provider
   matrix.
3. **Workstream Exit:** Relevant solution wiring, schema/ratchet freshness,
   actual packages and boundary suites once. Broaden only for a new failure,
   changed dependency or open concern.

**Documentation/Context:** No product build or test for pure prose. Verify
Markdown links/anchors, schemas, harness parity and whitespace proportionally.

**Failure Quarantine:** Never absorb unrelated test rot. Establish its baseline
evidence, record it in task context/backlog and continue the assigned scope.
Never delete, skip, weaken or edit an unrelated wrong test to make it green.

**Build Command (Code Changes Only)**

```bash
dotnet build --configuration Release --verbosity quiet
```

Targeted commands and project names are authoritative in `PROJECTS.md`.

---

## 9. Agent Operational Baseline

- **Tooling:** Native edits and POSIX Bash/native .NET execution only.
- **Subagents:** Use a bounded independent track when it speeds the task.
  Parent retains decisions, synthesis and validation of its own changes.
- **Memory:** Resume substantial work from the task-owned context, not
  plan/context/tasks all at once. Graduate durable findings to `dev/_journal/`.
- **Todos:** Create immediately for multi-step tasks; mark state changes when
  they occur and reconcile against the latest user instruction.
- **Context:** Follow the context-engineering retrieval/reuse contract.
  Duplicate unchanged context is a defect.
- **Scaffold Transfers:** Copy permitted original structure/content and change
  only what the destination actually needs. Do not replace detailed reusable
  guidance with thin summaries or redirect every source anchor to one overview.

### Validate Commit Messages Before Recording History

Before an authorized commit, confirm `git config --get core.hooksPath` reports
`.githooks` or its absolute path. If no custom hook path is configured, activate
`.githooks`; preserve any other configured checks. Never bypass hooks.

Stage owned literal paths only. Native `preflight-commit` validates message and
staged provenance before history is recorded; `preflight-range` retains range
enforcement. Internal skip declarations stay in one message argument with a
concrete `Changelog-Reason`. Breaking messages require `!` and a dedicated
`BREAKING CHANGE` footer block, plus a governed Change-Id where required.

See `.agents/skills/conventional-commit/SKILL.md` and `eng/release/README.md`.

### Readable Technical Communication

Lead with the concrete problem, who is affected, what changes and why it
matters. Define consequential terms and connect them to practical effects.
Improve readability by adding explanations, not deleting technical substance.
Preserve mechanisms, rationale, invariants, exceptions, trade-offs, exact
identifiers, evidence and uncertainty.

### Final Teaching Summary

Before completion or handoff, explain the delivered result, then the relevant
architecture, concrete files/classes/components and data/control flow. Explain
how the mechanisms produce the result and what was verified or remains. Do
not give only an abstract status sentence or require the user to open a diff.

### Self-Contained Decision Briefs

Active plan/context/tasks files are machine/session working memory, not the
developer console. Every material decision request states:

1. Current context and position.
2. Descriptive feature/component names, not bare IDs.
3. The exact choice and its practical consequences.
4. Clear options with recommendation and trade-offs.
5. The immediate next action after the answer.

---

## 10. Tool-Specific Bootloaders (Single Entrypoint Architecture)

`AGENTS.md` is the single source of truth. Harness files are pointers with no
complementary or diverged policy.

| Tool/Harness | Entry File | Dynamic Rules |
|---|---|---|
| OmO | `AGENTS.md` | `.omo/rules/` |
| Claude Code | `CLAUDE.md` | Native `@AGENTS.md` import |
| Cursor/Windsurf | `.cursorrules` | Pointer to this contract |
| GitHub Copilot | `.github/copilot-instructions.md` | `.github/instructions/` |

**Twin Rules:** Keep `.agents/rules/` and `.omo/rules/` counterparts
byte-identical. Update both when a rule changes. Inactive Event-only guidance
may be preserved as explicitly historical reference, never an active
requirement for nonexistent target owners.

---

## Approvals

Existing user authorization covers the commands/tools and reversible in-scope
edits needed for the assigned task. Continue when no material user decision is
needed. Ask before destructive operations, consequential external writes or
actions far beyond the request. No remote publication is implied by local
package validation.

## Local Configuration Overrides

If `AGENTS.local.md` exists, inspect its environment overrides and system
constraints. Preserve it as local configuration, never tracked transfer input.
The authoritative extraction triad remains only in
`dev/active/configurator-extraction/`.
