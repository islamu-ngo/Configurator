# AI_REVIEW.md - Code Change & Pull Request Review Checklist

> **Audience:** Contributors | Reviewers | AI Agents
> **Status:** Authoritative
> **Source Anchors:** [`AGENTS.md`](AGENTS.md), [`PROJECTS.md`](PROJECTS.md), [`docs/internal/QUICK_REFERENCE.md`](docs/internal/QUICK_REFERENCE.md), [`docs/internal/GOVERNANCE.md`](docs/internal/GOVERNANCE.md)

Use this checklist before proposing, authoring, or accepting code changes. It provides a structured, high-signal self-review to catch regressions and architectural rot before submission.

---

## 1. Scope & Layer Boundaries

- [ ] **Focused Scope**: Change remains strictly within the requested task; no unrelated refactoring, cosmetic touchups, or opportunistic cleanup in unmodified files.
- [ ] **Protected Paths Untouched**: Generated schemas/ratchets, dependency locks, audited dependency archives/notices and CI/release automation were not modified unless required by the assigned task.
- [ ] **Dependency Direction**: Clients depend inward on Core through the appropriate Presentation/Artifacts boundary; no Event backend, domain, wire or database reference exists.
- [ ] **Blazor Isolation**: `Configurator.Browser` references Core only and has no native I/O, target authentication or remote apply dependency.

---

## 2. Security & Data Protection

- [ ] **Zero Hard-Coded Secrets**: No passwords, API keys, private tokens, certificates, or real connection strings are committed in source code, appsettings, test fixtures, or docs.
- [ ] **Private Input Custody**: Operator answers are transient or protected native artifacts; fixtures use synthetic values or approved injected authorities, never real credentials.
- [ ] **Fail-Closed Boundaries**: Definition admission, browser public-choice import and unsupported protected saves reject without insecure fallback or value-bearing diagnostics.
- [ ] **Target Authority**: Preparation does not confer host acceptance, authentication or apply permission; family/publisher metadata is not trust evidence.

---

## 3. Blazor & Presentation Layer

- [ ] **Auto-Property Parameters**: Component parameters (`[Parameter]`) are declared as auto-properties only (`public string Value { get; set; } = string.Empty;`).
- [ ] **No Parameter Mutation via `@ref`**: Component parameters are never set or modified imperatively through component references (`@ref`).
- [ ] **Parameter State Management**: Immutable snapshots and operation generations fence stale preparation, imports and saves; parameter updates do not mutate parent-owned state.
- [ ] **CSS Isolation & Theming**: Styling respects scoped selectors and reusable themes; local styling does not bypass accessibility or leak between components.
- [ ] **Definition-Driven Action Affordances**: Controls follow admitted field activation and effective sensitivity. Public browser controls accept only active public enum/boolean choices and never imply native readiness or host authority.
- [ ] **Accessibility (a11y)**: Interactive elements maintain keyboard navigation, semantic headings, proper focus management, and accessible ARIA attributes.

---

## 4. Core, Artifacts & Shared Presentation

- [ ] **Closed Data Vocabulary**: Definitions remain bounded declarative data; no executable plugin or arbitrary remote definition loader is introduced.
- [ ] **Canonical Identity**: Family/revision identity binds canonical content. Previously admitted revisions cannot be replaced with conflicting digests, and active sessions cannot silently upgrade.
- [ ] **Restricted Output**: Native answer-derived artifacts retain sensitivity floors, create-only writes and unsupported-host refusal.
- [ ] **Actual Host Proof**: Any newly claimed native RID has actual-host restricted-save and package/flow evidence; cross-publication alone is not treated as support.
- [ ] **Operation Generations**: Completion commits only the current workspace generation; cancellation or replacement cannot publish stale results.
- [ ] **Thin Adapters**: CLI, Terminal.Gui, Avalonia and Blazor consume the shared contracts rather than duplicating target logic or adding Event dependencies.

---

## 5. Testing & Verification

- [ ] **Behavioral Coverage**: Focused tests were added or updated for every observable behavior change (RFC 2119 requirements).
- [ ] **Invariant-First Sequencing**: Tests were authored in the Red phase before production code, asserting against pre-agreed public seams (definition admission, artifact composition/write contracts, workspace generations and rendered client behavior).
- [ ] **No Tautological Tests**: Expected assertion values derive from independent literal specifications, not by repeating the production calculation (`items.Sum(x => x.Price)`).
- [ ] **No Internal Mocking**: Internal Core, artifact composition and shared workspace behavior are NOT mocked. Mock only genuine external boundaries; mocks must preserve the behavior under test.
- [ ] **bUnit Interaction Best Practices**: Component tests re-query DOM elements after actions, use async helpers (`ClickAsync`, `InputAsync`), and leverage `TimeProvider`/`FakeTimeProvider` rather than fragile `Task.Delay`.
- [ ] **Test Naming Conventions**: Test method names describe the scenario and expected outcome clearly without ending in `Test` or `Async`, and without `Test_` prefixes or trailing underscores.

---

## 6. Code Quality & Compiler Standards

- [ ] **Minimal & Focused Diff**: Diffs are concise, readable, and free of extraneous line shifts or whitespace churn.
- [ ] **No Warning Suppressions**: No compiler warnings or analyzer diagnostics were suppressed via `#pragma warning disable` or `[SuppressMessage]` to force a build to pass. Root causes must be resolved.
- [ ] **XML Documentation**: Non-inferable public contracts include clear XML `<summary>` documentation; source naming and folder structure remain self-documenting.
- [ ] **Code Formatting**: Code complies with the repository `.editorconfig` rules (file-scoped namespaces, brace styles, member order).
- [ ] **No Ad-Hoc Scripts**: No ad-hoc Python or Node.js scratch scripts were used or committed. Tooling lives in `eng/` as C# or POSIX Bash.

---

## 7. Verification Evidence Reporting

- [ ] **Commands Run & Exit Codes**: Final summary explicitly records every command run (build, test, format) with exact outcomes.
- [ ] **Checks Not Run with Reasons**: Relevant checks omitted (e.g. unrelated native/client suites for a documentation-only change) are noted with technical justifications.
- [ ] **Zero Fictitious Claims**: No test or build is claimed as passed unless physically executed and verified in the session.
