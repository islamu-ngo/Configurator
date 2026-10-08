---
name: tests
description: Apply when editing Configurator unit, integration, architecture, component, or engineering test source files.
paths:
  - "tests/**/*.cs"
  - "eng/**/tests/**/*.cs"
related_skills: [criticality-guardrail, implementation-plan, senior-cto-feedback]
related_docs: [docs/internal/TESTING.md, AGENTS.md, PROJECTS.md, docs/internal/QUICK_REFERENCE.md]
minimum_tests: []
related_intents: [repository-tooling, definition-engine, protected-artifacts, client-interface, agent-context]
---

# Test Rules

Twin files at `.agents/rules/tests.md` and `.omo/rules/tests.md` must remain byte-identical.

## Applies To
- All product test source in `tests/**/*.cs` and engineering tests in `eng/**/tests/**/*.cs`.

## Path-Specific Constraints
- **Owner-Sliced Execution & The 3-Ring Progressive Verification Model**:
  - **Ring 1 (Invariant Red / Focused Diagnosis)**: Run the target in-memory TUnit class with `--treenode-filter "/*/*/*<TestClass>/*"` when a failing invariant must be established or a failure diagnosed. No containers or network. Algorithmic admission, normalization, validation and state-machine checks belong in `Configurator.Core.Tests`.
  - **Ring 2 (Phase Exit Gate)**: Finish a coherent source/caller/docs/regression slice, then run one targeted Release build and the affected classes in the owning suite. Preserve passing evidence; repeat only failed or newly affected checks. Real filesystem guarantees belong in `Configurator.Artifacts.Tests`, not mocked Core tests.
  - **Ring 3 (Workstream Exit Gate)**: Run the affected owner suites and architecture/package closure checks once when the workstream changes those boundaries. No Event provider matrix, migration suites or backend services.
- **The Yak-Shaving Quarantine Rule**: Do not absorb pre-existing suite rot or broken fixtures outside the phase-owned scope. Establish pre-existence from untouched baseline evidence without unauthorized branches/worktrees or resetting user changes; if that cannot be proved, label the finding unverified. Record the exact failing command, error, affected path and evidence in the task's context ledger under `Validation Baseline / Pre-Existing Technical Debt`, quarantine it, and proceed. Do not claim green when the owned slice is failing.
- **Test-First Invariant Specification**: Start trust/privacy fixes with failing Invariant-Breaker tests before production changes. Tests must specify the contract independently, never generate post-hoc assertions that mirror implementation assumptions ("The Ugly Mirror").
- **High-Leverage Behavioral Assertions**: Assert public contracts: admitted definitions, validation results, sensitivity floors, operation-generation fencing, create-only publication and rendered semantics. Prioritize races and state transitions over getter/setter checks.
- **Pre-Agreed Public Seams**: Exercise Core codecs/registry/validator/composer, Presentation sessions/workspaces, Artifacts writer contracts and client entrypoints. Do not reach into private internals or substitute internal collaborators.
- **No Tautological Assertions**: Never recompute expectations with the same algorithm as production. Use literal expected values, a worked specification scenario or an independent known-good oracle.
- **No Interface Bypassing**: Observe success and failure through the public operation and its published state. Direct filesystem inspection is appropriate for the writer's atomicity, permissions and no-partial-output contract, not a substitute for exercising that writer.
- **Mock Boundary Rule**: Mock only external boundaries such as clock, randomness, native filesystem/platform capabilities or launch environment where they are not the invariant under test. Use real internal Core/Presentation objects and real supported-host writer integration when asserting protected publication. Mocks must preserve the interaction being asserted.
- **Suite Integrity**: Investigate failures; never delete, skip, weaken or suppress tests to bypass them.
- **No Warning Suppressions**: No `#pragma warning disable` or `[SuppressMessage]` to force builds/tests through. Fix the root cause.
- **Targeted Verification Default & Verification Evidence**: Use [PROJECTS.md](../../PROJECTS.md) for the affected owner. Record exact commands, exit codes, passed/failed/skipped counts and omitted checks with technical reasons. Tooling changes run engineering tests, not product suites; prose-only changes run no product tests.
- **Test Naming Conventions**: Do not end names with `Test` or `Async`, use `Test_` prefixes or trailing underscores. Describe the scenario and expected behavior.
- **Regression Testing Protocol**: Preserve at least one targeted assertion tied to the historical symptom; the defect must fail independently of implementation-shaped expectations.
- **Deterministic Async**: Subscribe to the exact event/state transition before triggering work; await that signal with a bounded timeout. No fixed sleeps, polling delays or timing luck. Use controlled completion gates for race orderings.
- **bUnit Best Practices**: Re-query DOM nodes after every interaction because rendering replaces them. Prefer `ClickAsync`, `InputAsync` and `ChangeAsync`; perform parameter updates on the renderer via `InvokeAsync()`. Use `TimeProvider`/`FakeTimeProvider` when time is the behavior, never fragile `Task.Delay`. Assert focus, labels, keyboard and status semantics, not CSS source.
- **Pristine Output**: No stray warnings, stack traces or noisy logs. Expected failures produce asserted value-safe diagnostics.
- **Runtime Realism**: Keep in-process integration deterministic. Run supported-host native filesystem checks only where platform behavior is the contract; do not certify unsupported hosts from a mock.
- **Project Role Balance**: Core invariants in `Configurator.Core.Tests`, workspace generations in `Configurator.Presentation.Tests`, writer guarantees in `Configurator.Artifacts.Tests`, and surface behavior in the corresponding client suite.
- **Invariant Disposition Before Deletion**: Each removed test/cohort maps to a stronger retained public-seam test, a passing semantic replacement or intentionally removed product behavior. Counts, runtime and coverage are not sufficient deletion evidence.
- **Executable Contracts, Not Source Inventories**: Runtime composition, actual CLI machine output, rendered semantics, package closure and genuinely machine-consumed schemas are valid assurance seams. Raw C#, Razor, CSS, Markdown prose, class-name inventories and historical allowlists are not product assurance.
- **No Governance Documents As Test Inputs**: Product tests must not read `AGENTS.md`, `.agents/**`, internal Markdown, plans, tasks, evidence, journals or skill prose. Validate machine-consumed metadata through its production parser or explicit native `eng/` command; generators are tools, never `[Test]` methods.
- **Zero Hard-Coded Test Secrets**: Never embed real credentials, tokens or connection strings. Use approved environment injection or a boundary provider fixture; never print secret values, arguments or raw exception/path contents.
- **Record Contract Specifications**: Follow [GOVERNANCE.md](../../docs/internal/GOVERNANCE.md). Cover consumed equality, one-fact `with` variants, caller-mutation isolation, JSON construction and trust-boundary attacks where the contract exposes these behaviors. Structured manifests may classify machine-consumed contracts, not pin prose or historical debt.

## Must Read
- [Quick Reference](../../docs/internal/QUICK_REFERENCE.md)
- [Testing](../../docs/internal/TESTING.md)
- [Operations](../../docs/internal/OPERATIONS.md)

## Verification
- Build only the touched owner: `dotnet build tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --verbosity quiet` is the Core example; choose the actual owner from `PROJECTS.md`.
- Tests: `dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release -- --treenode-filter "/*/*/*<TestClass>/*"`; replace the owner/class, not the behavior under test.

## Related
- Intents: `repository-tooling`, `definition-engine`, `protected-artifacts`, `client-interface`, `agent-context`
- Rules: [Browser](blazor-client.md), [Privacy](privacy-and-pii.md), [Criticality](work-criticality-matrix.md)
