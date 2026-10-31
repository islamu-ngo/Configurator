# Testing

> **Audience:** Contributors | AI agents
> **Status:** Implemented testing policy
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-08
> **Source Anchors:** `PROJECTS.md`, `global.json`, `tests/`, `eng/release/tests/`, `.agents/rules/tests.md`

Selected reusable sections from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` retain execution evidence, framework,
verification cadence, conventions and fixture responsibilities. Event historical
test counts and database/API/BFF infrastructure do not apply.

## Test execution versus measured coverage

Browser interaction performance uses published Release packages, not bUnit wall
clock assertions. The [qualification procedure](OPERATIONS.md#browser-interaction-qualification-2026-10-22)
binds trusted input, complete DOM state and final frames to exact candidate and
baseline inventories. Keep all 40 samples after five event-complete warmups,
first-use separately, with 1x/4x CPU, full search counts and actual viewport.
Unit tests guard custody, literal paths, correction read order, queued typing,
blur deduplication, remounting and paging. They do not prove latency, offline
platform behavior or assistive-technology conformance. A failed active budget
blocks qualification rather than becoming a weakened timing test.

The frozen indexed-edit package's 92 directional rows (3,680 measured samples)
pass those gates on the recorded reference host. Operations binds the exact
inventory, untouched comparisons, custody/layout evidence and quarantined
Desktop failures. This evidence does not certify preparation responsiveness,
screen-reader speech, physical mobile devices or actual browser UI zoom.

A passing TRX proves test execution, not line or branch coverage. A count of
passed cases and zero skips does not establish coverage unless those runs used
the collector and produced validated coverage files for the same commit.

Use unique report filenames so successive selections do not overwrite each
other. Inspect product assemblies and covered lines in the actual report before
interpreting percentages. No test count, local workflow validation or upload
implies that another analysis service received coverage. Missing measurements
must be reported as missing; test failures must not be converted into success by
publication. Configurator does not inherit Event's Codecov/provider publication
pipeline.

### Safe retained owner reports

`.ci/scripts/prepare-test-evidence.cs` accepts explicit `--owner`, `--rid`,
`--run`, `--input`, `--output`, `--exit-code` and `--coverage required|none`
arguments. The input directory contains `results.trx` and, when required,
`coverage.cobertura.xml`. Each selection must use a fresh private directory.
The tool creates `<owner>-<rid>-<run>.json` without replacing an existing file.
CI run identifiers include the source SHA, workflow run, attempt and lane;
owner and host are separate filename components.

The JSON retains owner/test assembly identity, passed/failed/skipped counts,
duration, SHA-256 case identities, outcomes and fixed diagnostic codes. Test
names, parameter values, stdout/stderr, exception text, source paths, machine
names and TRX attachments are not copied. XML inputs are limited to 64 MiB each,
100,000 cases/classes and 1,000,000 class-line entries, with DTDs prohibited.
Coverage is computed from the selected product assembly's class-line entries
and branch numerators/denominators, not from supplied percentages or dependency
assemblies. Generated classes sharing a source line remain distinct, matching
the collector's class-line model. Raw files remain private and are not uploaded.

`EvidenceStatus: validated` means parsing succeeded, not that tests passed.
`CommandExitCode` retains the original process status. A nonzero test exit is
returned unchanged, including when report preparation fails. Invalid/missing
reports after a zero test exit return 1; they cannot turn into success or zero
coverage. Failed test counts survive an invalid coverage report.
`CoverageStatus: unmeasured` is explicit for runs without collection.
`--self-test` exercises synthetic positive/negative vectors and does not measure
product coverage. CI uploads only safe JSON with `always()` and seven-day
retention, including failed selections; it does not upload the private command
log or overwrite dependency advisory files. The standalone loop continues
through owners and returns the first nonzero test/evidence status.

The selected collector is `Microsoft.Testing.Extensions.CodeCoverage` 18.9.0,
centrally pinned with `PrivateAssets="all"` only in Core/Presentation tests.
TUnit 1.63.0 already resolved this exact collector transitively. Native restore
promotes it to direct in those two locks and central-transitive in the other
eight TUnit test locks, without changing package versions or hashes. The exact
affected locks are in Core, Presentation, Architecture, Artifacts, Browser, CLI,
Desktop and Terminal tests, plus AgentWorkflow and ReleaseEngineering tests.
Its nuspec requires MTP >= 2.3.0; the actual closure
resolves MTP 2.3.3. The collector uses Microsoft .NET Library terms, not MIT:
development/testing use is permitted, while redistribution carries separate
requirements. Keep it out of product packages and preserve its original terms
and third-party notices if test tooling is ever conveyed. Central pinning does
not relicense it or authorize its redistribution.

Official interface sources, consulted 2026-10-27:
[MTP coverage](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage),
[MTP TRX reporting](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports),
[collector configuration](https://github.com/microsoft/codecoverage/blob/main/docs/configuration.md)
and the restored 18.9.0 nuspec/license. Implementation uses documented interface
facts and independently designed allowlisted JSON output; no third-party
implementation is copied.

Collect a selected settled owner with native MTP arguments (no VSTest `--`
separator after `dotnet test` in this SDK):

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1
raw=$(mktemp -d "$TMPDIR/presentation-evidence-XXXXXXXX")
if dotnet test --project tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj \
  --configuration Release --minimum-expected-tests 1 \
  --report-trx --report-trx-filename results.trx --results-directory "$raw" \
  --coverage --coverage-settings "$PWD/.ci/scripts/coverage.config" \
  --coverage-output-format cobertura --coverage-output coverage.cobertura.xml \
  > "$raw/command.log" 2>&1
then status=0; else status=$?; fi
dotnet run .ci/scripts/prepare-test-evidence.cs -- \
  --owner Configurator.Presentation --rid linux-x64 --run local-presentation-unique \
  --input "$raw" --output artifacts/test-evidence --exit-code "$status" --coverage required
```

Choose a new run token for every invocation. Inspect the safe report and record
the source/build identity before interpreting it. No global percentage or
mutation gate is imposed.

### Initial collection and resolved Core instrumentation prerequisite

On Linux x64 with SDK 10.0.302, the 2026-10-27 initial Presentation collection
executed 155 tests: 155 passed, 0 failed, 0 skipped, exit 0. Validated assembly
`Configurator.Presentation` measured 1,674/1,758 class-line entries and
1,201/1,368 branches. These are one run's measurements, not a maintained
threshold or a claim about other owners/hosts.

The initial Core collection executed 145 tests: 144 passed, 1 failed, 0 skipped,
exit 2. Its validated `Configurator.Core` report contains 3,188/3,783 class-line
entries and 2,737/3,683 branches, but this is **failed-run evidence**, not a green
Core baseline. Default collector instrumentation adds
`Microsoft.CodeCoverage.Instrumentation.Static.Tracker` types; the unchanged
`SetupCoreArchitectureTests.ProductAssemblyRemainsBclAndApprovedYamlOnlyWithoutAmbientAuthority`
guard correctly rejects their ambient I/O and mutable statics. A dynamic-only
configuration passed all 145 tests but emitted an empty `<packages />` report;
the evidence tool rejected it with exit 1 rather than claiming coverage.

The repository's coverage settings now select dynamic managed instrumentation
with the same pinned collector. This leaves the compiled Core image unchanged;
no architecture assertion, test or covered product class is excluded. On this
rolling Linux workstation the profiler initially could not load because
`libInstrumentationEngine.so` requires the older `libxml2.so.2` ABI, while the
system provides `libxml2.so.16`. Native dependency inspection identified that
missing library. Pointing this local run's `LD_LIBRARY_PATH` at the already
installed Rider x64 debugger library directory satisfied it without installing,
copying or shipping a dependency. Other hosts must supply the profiler's actual
runtime prerequisites; an empty report still fails validation.

With that prerequisite available, the actual current Core collection passed
150/150 tests, zero failures/skips, exit 0. The safe report validated
`Configurator.Core`: 3,223/3,815 class-line entries and 2,745/3,687 branches;
TRX duration 903.5408 ms. The earlier failed static and empty dynamic attempts
remain historical failed evidence, not a green baseline. Final settled-source
Core/Presentation collection is still required after remaining product edits.
These local results do not prove GitHub execution or artifact delivery.

The same checked-in dynamic settings subsequently passed the current
Presentation 167/167 lane, zero failures/skips, exit 0. Its validated selected
assembly measured 1,699/1,771 class-line entries and 1,192/1,346 branches;
TRX duration 1,021.8177 ms. This successor supersedes the earlier 155-case
measurement for these shared-form/adoption inputs, without rewriting its history.

## Framework

The project uses TUnit through Microsoft.Testing.Platform (MTP), selected in
`global.json`. Browser semantic tests use bUnit. Use MTP's project selector, not
solution-level `dotnet test`.

| Feature | Usage |
|---------|-------|
| `[Test]` | Test method marker |
| `[Before(Test)]` / `[After(Test)]` | Per-test setup/teardown |
| `[Before(Class)]` / `[After(Class)]` | Per-class lifecycle hooks |
| `[NotInParallel]` | Serialize tests sharing resources |
| `Assert.That(x).IsEqualTo(y)` | Fluent async assertions |
| `Assert.Multiple()` | Group multiple assertions |
| `[Explicit]` | Intentionally selected real-renderer/rehearsal checks |

## The 3-Ring Progressive Verification Model

Keep verification bounded to actual owners. For the universal MVP workstream,
security invariant-breaker Red runs once before the relevant fix. Otherwise
complete coherent source/caller/docs/regression slices continuously, then run
one targeted Release build and affected classes at phase exit. Preserve passes;
rerun only failed or newly affected checks. Broader checks run once at final
workstream exit.

| Ring | Scope & Cadence | Permitted Suites & Infrastructure | Purpose |
|---|---|---|---|
| Inner loop | Relevant invariant-breaker Red or newly discovered concern | Focused in-memory owner tests; no containers/network | Prove the failure and local invariant |
| Phase exit | Coherent phase completed | Targeted Release build and affected classes | Project-level integrity without unrelated suite delays |
| Workstream exit | Before final handoff/PR | Required affected-owner suites and relevant architecture/package checks once | Cross-owner and package drift |

### Fast-Loop In-Memory Slicing vs Native Integration Testing

Pure algorithmic, normalization, validation and state-machine invariants belong
in Core or Presentation tests. Do not launch clients or exercise filesystem
output just to prove pure in-memory behavior. Native Artifacts tests exercise
real protected create-only output; client tests exercise the actual adapter
seam. Architecture/package checks verify evaluated project contracts and actual
closure. None requires an Event database or running service.

Windows/macOS protected save and Linux arm64 package behavior remain
**Planned** until Artifacts and package/flow checks execute on those actual
hosts. A Linux x64 test or cross-publish does not satisfy their host gate.

### The Yak-Shaving Quarantine Rule

When working on a feature or bug fix:

1. Do not repair pre-existing unrelated suite rot or fixture failures.
2. Establish whether the failure also occurs on untouched baseline evidence;
   do not create branches/worktrees when the task forbids them.
3. Record proven pre-existing failures in task-owned `*-context.md` under
   `Validation Baseline / Pre-Existing Technical Debt` (or `dev/backlog/`),
   quarantine them and proceed with assigned scope. Label unproven origin as
   unverified, not pre-existing.

## Test Projects

| Behavior | Project |
|---|---|
| Definition admission/evaluation | `tests/Configurator.Core.Tests` |
| Protected native writes | `tests/Configurator.Artifacts.Tests` |
| Immutable workspace/generation fencing | `tests/Configurator.Presentation.Tests` |
| Machine CLI | `tests/Configurator.Cli.Tests` |
| Terminal interaction | `tests/Configurator.Terminal.Tests` |
| Desktop interaction | `tests/Configurator.Desktop.Tests` |
| Browser semantics | `tests/Configurator.Browser.Tests` |
| Compiled boundaries and package closure | `tests/Configurator.Architecture.Tests` |
| Read-only agent guard | `eng/agent-workflow/tests/Configurator.AgentWorkflow.Tests` |
| Release policy and local Git evidence | `eng/release/tests/Configurator.ReleaseEngineering.Tests` |

### Run Commands

Run from Configurator with SDK 10.0.302. Replace the Core paths below with the
affected owner; do not run all product suites for engineering-tool changes.

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/<TestClass>/*" --minimum-expected-tests 1
```

`<TestClass>` is a selector placeholder, not a literal runnable test name. Confirm
the actual class/method and retain passing counts. Use the commands in
[PROJECTS.md](../../PROJECTS.md) for owner and packaging gates.
Full current architecture execution requires the three native Linux x64 surface
packages and static Browser output first; a package build alone is not evidence
that every suite passes.

### Disabled Test Governance

Disabled tests are allowed only when the test still expresses required future
behavior and cannot run in the current lane. Do not comment out `[Test]`; keep
it active, mark it with `[Skip("Category: ... Removal: ...")]`, or delete it when
the behavior is obsolete or unnecessary.

Use `[Explicit]` for valid, intentionally opt-in runtime or release-rehearsal
tests whose infrastructure/duration makes them unsuitable for an unfiltered
developer run. Keep a positive filter documented so evidence remains runnable.
The tool-bundle environment variable alone does not select explicit tests.

Skip reason requirements:

- `Category:` names the owning suite/lane.
- `Removal:` states the concrete condition for re-enabling or deleting the skip.
- No permanent skips; infrastructure-gated tests need a documented selected lane.
- No preservation tests for obsolete endpoints, shapes or behavior in greenfield
  development. Delete obsolete tests, never required failing tests.

Do not claim an Event source-hygiene gate exists in Configurator merely because
these conventions were transferred.

## Architecture Tests

### Executable Architecture Contracts

Architecture tests enforce rules through compiled assembly/type relationships,
reflection, runtime behavior or parsed machine-consumed schemas and evaluated
project contracts. They must not freeze raw C#, Razor, CSS, Markdown prose,
class-name inventories, line counts or historical debt allowlists.

Structured manifests are appropriate only when shipped machine-consumed
contracts or generator inputs, with the complete current surface derived
independently. A stale historical exemption is not a contract. When no executable
seam can prove a style preference, use an analyzer or review guidance, not a
source-text test.

Product tests never consume `AGENTS.md`, `.agents/**`, Markdown under `docs/`,
active plans/tasks/evidence, journals or skill prose. Validate machine-readable
automation artifacts through the consuming parser or an explicit `eng/` tool.
Invoke documentation generators directly, not as disguised TUnit tests.

Browser component tests use generic bUnit rendering and typed parameter builders
for compile-time-known public components. Reserve `DynamicComponent` for behavior
that genuinely selects a descriptor at runtime. Invoke load/toggle/save actions
through rendered controls rather than reflective calls into private behavior.

`Configurator.Architecture.Tests` verifies compiled product boundaries, evaluated
release contracts and actual package closure. Event's assurance-audit executable
is not a Configurator command. Package prerequisites remain in `PROJECTS.md`.

## TDD Workflow

### Native Desktop qualification

`tests/Configurator.Desktop.NativeHost` is an isolated executable, not an MTP
project. It initializes the real Desktop App and classic lifetime on the main
thread and requires Linux x64/X11. Twenty required named scenarios exercise actual
controls, JSON/YAML/dotenv import, preparation, native owner-only/create-only
saves, bundle preflight/recovery, stale import, close and one/two/three-tenant
Event manifests with independent expected fields. Missing display, wrong
backend/RID, timeout, failure and incomplete counts reject the run.

The required set preserves all nineteen previous names and adds
`D20-read-only-import-comparison`. Comparison tests use real form/publisher
behavior and controlled external acquisition and owner-thread gates. They check
read-only answers/drafts/preparation, schema-only explanations, deliberate
adoption refusal and stale completion separately from physical read cleanup.
Use the focused Desktop filter
`/*/*/(DesktopImportComparisonTests)|(DesktopShellViewModelTests)|(DesktopApplicationTests)/*`
with `--minimum-expected-tests 1` after building that owner. Headless execution
is not actual native/published comparison evidence. Published interaction,
resource/latency measurements, manual dialogs, keyboard, RTL/scaling and
assistive-technology requirements remain separate qualification work.

Run its commands from [PROJECTS](../../PROJECTS.md#standalone-packaging).
Headless checks remain faster invariant/focus evidence. Neither harness replaces
actual published binary, installed-package, physical keyboard or assistive
technology verification. A dispatcher-flushed native import case guards against
deferred TextChanged initialization invalidating imported form state.

TDD is the default unless explicitly allowed to skip:

1. Write a failing test defining expected behavior.
2. Run to confirm it fails for the right reason.
3. Write minimal code, just enough to pass.
4. Run the relevant tests; all must pass.
5. Refactor with tests green.

The approved phase cadence above controls execution timing. Pure-prose changes
need documentation checks, not new tests that pin wording.

## Test Conventions

### Do

- Test one behavior per method.
- Use descriptive names: `MethodName_Condition_ExpectedResult`.
- Use real data and APIs in integration tests; avoid mocks in end-to-end flows.
- Run required project-level suites before a PR, never solution-level tests.
- Keep output pristine: no unexpected warnings or stack traces.
- Prefer deleting unnecessary compatibility-only tests over obsolete behavior.
- Derive expected results independently of the implementation under test.
- For async behavior, subscribe to the exact signal before triggering work and
  await it with a bounded timeout.

### Do Not

- Delete or weaken failing tests to make a suite pass.
- Commit with broken affected tests.
- Mock away an in-process integration seam that the test is intended to assert.
- Create ad-hoc test scripts; use the test projects.
- Skip required architecture/package evidence.
- Comment out `[Test]` attributes to hide failure.
- Add compatibility tests for obsolete behavior in greenfield development.
- Use timing sleeps, source/CSS scraping or internal mock-mirroring assertions.

## Test Data And Fixtures

- **Core tests:** construct admitted definitions and invalid boundary inputs.
- **Presentation tests:** use immutable state and explicit operation signals;
  assert stale-generation rejection through observable behavior.
- **Artifacts tests:** use isolated temporary output directories and real native
  protection; preserve sensitivity floors and create-only failure semantics.
- **Client tests:** exercise adapter semantics; Browser keeps answers transient,
  requires workspace consent for secret leaves and excludes answers/output from
  public caches/requests. Unit seams do not replace published-browser network,
  cache/update, accessibility and export evidence.
- **Release tests:** disposable Git repositories and synthetic trust fixtures
  prove local validation only. Real-renderer checks run sequentially because
  independent processes share the synthetic promotion-root output.

### Choosing the Right Test Layer

| Question | If Yes | If No |
|---|---|---|
| Does behavior require real browser JS or visual interaction? | Focused browser QA plus semantic seams below | Next question |
| Does it exercise native output protection? | Artifacts tests | Next question |
| Does it exercise client rendering or accessibility? | Corresponding client suite | Next question |
| Does it exercise shared state/generation behavior? | Presentation tests | Next question |
| Does it verify definition admission/evaluation? | Core tests | Reconsider whether a test is needed |

## CI Pipeline Integration

Locked restore requires matching generated `packages.lock.json` updates when
package inputs change. Restore, Release build, per-project execution and retained
reports are separate evidence. Do not imply source-derived tests that require
absent Event CI scripts are runnable publication gates here; the workflow owner
must rebase them first. Real renderer fixtures do not activate signers or provider
publication.

## Related

- [Project commands](../../PROJECTS.md)
- [Operations](OPERATIONS.md)
- [Release renderer selectors](../../eng/release/README.md#tests)
- [Protected artifacts](PROTECTED_ARTIFACTS.md)
