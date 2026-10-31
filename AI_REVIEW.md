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
- [ ] **Blazor Isolation**: `Configurator.Browser` references browser-safe Presentation/Core and has no Artifacts/native I/O, target authentication or remote apply dependency. Check the actual compiled and packaged transitive closure.

---

## 2. Security & Data Protection

- [ ] **Zero Hard-Coded Secrets**: No passwords, API keys, private tokens, certificates, or real connection strings are committed in source code, appsettings, test fixtures, or docs.
- [ ] **Private Input Custody**: Browser answers are transient; secret entry is explicit per workspace and revocation clears secret leaves/output. Public caches contain no answers/artifacts. Fixtures use synthetic values or approved injected authorities, never real credentials.
- [ ] **Fail-Closed Boundaries**: Definition admission, pinned credential-free HTTPS/local-file loading, consent-gated answer imports and unsupported protected saves reject without insecure fallback or value-bearing diagnostics.
- [ ] **Target Authority**: Preparation does not confer host acceptance, authentication or apply permission; family/publisher metadata is not trust evidence.

---

## 3. Blazor & Presentation Layer

- [ ] **Auto-Property Parameters**: Component parameters (`[Parameter]`) are declared as auto-properties only (`public string Value { get; set; } = string.Empty;`).
- [ ] **No Parameter Mutation via `@ref`**: Component parameters are never set or modified imperatively through component references (`@ref`).
- [ ] **Parameter State Management**: Immutable snapshots and operation generations fence stale preparation, imports and saves; parameter updates do not mutate parent-owned state.
- [ ] **CSS Isolation & Theming**: Styling respects scoped selectors and reusable themes; local styling does not bypass accessibility or leak between components.
- [ ] **Definition-Driven Action Affordances**: Typed controls follow admitted activation and nested sensitivity. Secret-deferred drafts visibly distinguish unresolved secrets from complete preparation; neither result implies host acceptance or authority.
- [ ] **Accessibility (a11y)**: Interactive elements maintain keyboard navigation, semantic headings, proper focus management, and accessible ARIA attributes.

---

## 4. Core, Artifacts & Shared Presentation

- [ ] **Closed Data Vocabulary**: Definitions remain bounded declarative data; remote sources admit only public credential-free HTTPS before answers, with bounded streaming, canonical pinning and cancellation fencing. No executable plugin or arbitrary launch authority is introduced.
- [ ] **Canonical Identity**: Family/revision identity binds canonical content. Previously admitted revisions cannot be replaced with conflicting digests, and active sessions cannot silently upgrade.
- [ ] **Restricted Output**: Native answer-derived artifacts retain sensitivity floors, create-only writes and unsupported-host refusal.
- [ ] **Deliberate Browser Export**: Restricted copy/download requires user action; no browser export claims native owner-only permissions or create-only writes. Edits, consent revocation, replacement and disposal invalidate output.
- [ ] **Public Offline Cache**: Only reviewed public app assets and explicitly retained admitted definitions are cached. Answers/output never enter storage, worker messages or requests; updates cannot replace live workspaces. Published browser evidence is separate from bUnit/package checks.
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

## 8. Documentation and retrieval

- [ ] **Authority and scope**: Current contracts, proposed work, accepted
  decisions and historical observations are distinguishable before relying on
  their claims; disagreements have an explicit disposition.
- [ ] **Reader route**: A contributor can find the responsible owner, mechanism
  and proportional check from the documentation hub or matching agent intent.
- [ ] **Examples and guarantees**: Runnable examples match maintained synthetic
  fixtures and current contracts; excerpts are labeled. Preparation, publication,
  target acceptance and actual-host qualification remain distinct.
- [ ] **Preserved evidence**: Protected strategy, historical records, governed
  fragments, provenance and reviewed digests were not silently rewritten.
- [ ] **Proportional validation**: Links, anchors, metadata and affected harness
  parity were checked; executable fixtures or tooling used their own checks.
  An editorial review date does not renew runtime qualification.
