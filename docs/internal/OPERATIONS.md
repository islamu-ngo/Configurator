# Operations

> **Audience:** Contributors | AI agents
> **Status:** Implemented local engineering procedures; publication not activated
> **Owner:** Packaging/Release
> **Last Verified:** 2026-10-09
> **Source Anchors:** `AGENTS.md`, `PROJECTS.md`, `global.json`, `eng/agent-workflow/`, `eng/release/`

This selective transfer uses the AI-agent operational section from Event
revision `c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38`, with local verification
instructions anchored to Configurator owners. Event's backend operations,
databases, API/BFF, Aspire startup and production trust are not imported.
Configurator prepares artifacts offline; it does not apply target configuration
or acquire host administration authority.

## Operational Runbooks

- [Packaging technical anchor](PACKAGING.md) and [adopter guide](../public/packaging.md)
  own native/static output commands and platform limits.
- [Protected artifacts](PROTECTED_ARTIFACTS.md) and [native output guide](../public/native-output.md)
  own create-only save, privacy and failure behavior.
- [Launch contract](LAUNCH_CONTRACT.md) owns the local handoff boundary.
- [Release runbook](RELEASE_RUNBOOK.md) owns generated notes and local evidence.

Do not use service-recovery or direct-database instructions from Event as
Configurator operating procedures.

## Verification Policy

Use SDK 10.0.302 and Microsoft.Testing.Platform. Set the current workstation
temporary root before native tool/test execution:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
```

Run the targeted Release build and affected classes at coherent phase exit as
described in [TESTING.md](TESTING.md#the-3-ring-progressive-verification-model).
Preserve passing evidence; rerun only failed/newly affected checks. Broader
affected-owner and architecture/package checks run once at workstream exit.

The current CI/package lane covers Linux x64 native clients and static Browser.
The Windows x64, macOS arm64 and Linux arm64 actual-host preparation,
restricted-save and package checks are **Planned**. Record those as unrun until
their host commands and results exist; cross-published output is not a substitute.

Documentation-only changes verify local targets, anchors, formatting and
whitespace. They do not run product builds or .NET test suites. Never broaden a
tooling-only change into product suites or an Event database matrix.

### Verification Evidence

For each completion, report exact commands and outcomes, including exit status
and executed test selections. Record omitted checks and why; distinguish package
creation, test execution and measured coverage. Do not say a build or test passed
when it was not executed. Explicit tests omitted from an ordinary run are not
evidence for their behavior.

Report unrelated failures without absorbing them into the task. Prove baseline
origin where available and quarantine pre-existing rot in task-owned context.
No warning suppression, hidden skip or test weakening is an acceptable repair.

## Browser custody and offline verification

The [browser ADR](adr/ADR-browser-session-trust-and-export.md) records the design;
[launch](LAUNCH_CONTRACT.md), [presentation](PRESENTATION_STATE.md) and
[packaging](PACKAGING.md) identify production owners. Browser package creation
does not deploy hosting or prove platform behavior. Use a published static
package, not development hosting, for offline/cache/update evidence.

Before claiming actual-platform support, record the static host, browser/version,
package identity and exact scenarios. Prepare public app assets and explicitly
retain a public admitted definition before answers. Disconnect, reopen/reload,
edit typed settings and object rows, and inspect complete/draft copy/download
against independent vectors. With distinctive synthetic sentinels, inspect
network requests, storage and Cache API payloads to prove no answers/output
entered those sinks. Exercise consent revocation, clear/navigation, late work
and updates with another active tab. Inspect keyboard focus, mobile reflow,
zoom and value-safe failures. Never include private paths or real credentials
in evidence.

Application-local no-fetch assertions are distinct from browser-managed worker
update checks and installation of public app assets. These update requests carry
no answers while an older workspace remains pinned. The page's connection guard
does not control all worker/browser traffic. Physical disconnection is stronger.
Managed-memory clearing, clipboard and downloads are not forensic-erasure claims. Cross-published native
RIDs and package tests remain separate from actual-host protected-save evidence.

Implementation evidence from the authorized `browser-complete-configuration`
workstream on 2026-10-09 currently establishes these separate owner checks:

| Gate | Observed result | Local result artifact |
| --- | --- | --- |
| Core final Release suite | 97 passed, zero failures/skips; 1.219 s | `.omo/evidence/20261009-browser-complete-configuration/core-workstream-final.txt` |
| Presentation final Release suite | 36 passed, zero failures/skips; 0.973 s | `.omo/evidence/20261009-browser-complete-configuration/presentation-event-final-tests.txt` |
| Browser final owner Release suite | 81 passed, zero failures/skips; 4.390 s | `.omo/evidence/20261009-browser-complete-configuration/browser-owner-final-tests.txt` |
| Final solution Release build after locked restore | Zero warnings/errors; 3.226 s wall time | `.omo/evidence/20261009-browser-complete-configuration/solution-build-final.txt` |
| Solution format verification | Exit 0; 125.975 s wall time | `.omo/evidence/20261009-browser-complete-configuration/solution-format-green.txt` |
| Last changed Browser slice format verification | Exit 0; 11.437 s wall time | `.omo/evidence/20261009-browser-complete-configuration/format-last-slice.txt` |
| Final static Browser publication | Exit 0; 17.233 s wall time; 33 dependency notices, ratchets current 3/3 | `.omo/evidence/20261009-browser-complete-configuration/browser-publish-final.txt` |
| Actual final published Browser package | 1 passed, zero failures/skips; 0.746 s | `.omo/evidence/20261009-browser-complete-configuration/browser-package-final.txt` |
| Compiled product dependency closure | 7 owner rows passed, zero failures/skips; 0.741 s | `.omo/evidence/20261009-browser-complete-configuration/browser-architecture-final.txt` |
| Event wire/host checks against eight actual Core-produced files | 15 tests passed; 8.129 s; independent exporter verification 8/8 valid, exit 0 | `.omo/evidence/20261009-browser-complete-configuration/event-actual-tests.txt`, `event-actual-verify.txt` |
| Definition schema freshness | Current | `.omo/evidence/20261009-browser-complete-configuration/core-schema-check.txt` |
| Native capability/schema ratchets | Generated/current 3/3, exit 0 | `.omo/evidence/20261009-browser-complete-configuration/governance-ratchet-write.txt`, `governance-ratchet-check.txt` |
| Intent YAML, changed rule twins and whitespace | Valid/identical/clean, exit 0 | `.omo/evidence/20261009-browser-complete-configuration/governance-intents-final.txt`, `governance-docs-checks.txt` |

The ratchet invocations were `dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --write`
and the matching `--check`, with the documented TMPDIR. The intent syntax check
used `dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release --no-build --no-restore -- validate-intents .agents/contract/intents.yaml`.
These local paths are session evidence, not distributed package inputs or
release certification. Event acceptance is wire/semantic validation of synthetic
files, not deployment or authenticated apply.

The package and compiled-closure checks used the actual final published assets
and compiled assemblies, respectively:

```bash
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --treenode-filter "/*/*/*StandalonePackageTests/BrowserPackage*" --minimum-expected-tests 1 --no-progress
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --treenode-filter "/*/*/*StandaloneArchitectureTests/CompiledProductClosureHasNoEventOrTestDependency" --minimum-expected-tests 7 --no-progress
```

An initial Browser-name wildcard selected zero parameterized closure rows and
correctly failed the minimum-test guard; the method filter above selected all
seven owners. This is not a full architecture suite or a native package matrix.

Published Chrome checks reported by the parent execution track have exercised
worker boot, all three independent neutral complete-output vectors, a
secret-deferred draft, offline reload and a fresh tab, uncached-source local
rejection, explicit clipboard export and consent revocation. Inspection found
65 allowlisted public app cache entries and no synthetic answer canary in
application cache/store/network sinks. The browser downloaded a declared `.env`
as `env.txt` despite the correct link hint; operators must check/rename downloaded
filenames before target use. Application-state clearing does not clear those
exported files or clipboard content.

The independent published track records its observations in
`.omo/evidence/20261009-browser-complete-configuration/platform-published-qa.md`
and `platform-final-narrow.png`. Published Chromium ran against the native static
host `http://127.0.0.1:5078/`. The broad published retest used cache
`configurator-app-V6l73mQ7`; minimal revision 2 was admitted. Offline neutral
inputs covered all seven admitted field types and nested object-list
uniqueness/bounds/removal, with preserved survivor identity. Complete/draft
actual downloads passed while offline; reload and fresh-tab boot started with
empty answers and consent. All 65 app-cache responses in that retest and captured request
URLs/bodies had no exercised answer canaries; local/session storage, cookies
and IndexedDB were empty. Controlled answer-bearing fetch/XHR/WebSocket/
EventSource/beacon attempts were blocked in both offline and online active
workspaces, with zero observed destination requests. This verifies exercised
page wrappers, not every browser primitive or extension.

Final-package retests linked required Count's `aria-invalid` to its field error,
and keyboard Add at the collection maximum focused the new row input. Event
typed controls prepared independent zero-, one-, two- and three-tenant manifest
vectors, including explicit empty objects. Unsupported conditional/repeat
inverse import reported `inverse-unsupported` and retained answers. Supported
neutral JSON import respected existing-answer conflicts, succeeded with cleared
conflicts and explicit consent, and rejected secret-bearing input after revocation
without partial answers. The subsequent published duplicate-row refinement used
cache `configurator-app-8oPgRM97`: `alpha/beta/alpha` marked the collection and
third row/input invalid, linked `value-duplicate`, left other rows valid and
published no export. `platform-final-duplicate-aria.png` records the 375 CSS px
retest without horizontal overflow. Preceding unchanged passes remain separate
evidence.

Two old-package active tabs retained distinct ordinary answers and consent while
an update installed a waiting worker; old/new public app caches coexisted without
automatic workspace replacement. After clearing, navigation away/reopening did
not independently prove activation in the original context; a fresh context
verified the new package. Navigation away/back cleared answers/exports, but
persisted BFCache restoration was not forced. Actual 375×812 and 640×450 CSS
viewports showed no horizontal overflow; text enlargement passed as a proxy,
while real browser toolbar zoom was ignored. No screen-reader pass is claimed.
Injected clipboard denial retained Download. Controlled source admission,
redirect/credential/referrer/stream/timeout checks executed the published JS,
but successful live CORS/worker transport remains unverified: the grounded
public endpoint returned 404. No live target deployment, OS permission or
native protected-save certification follows from these browser observations.

A final idle-host check opened three concurrent cached workspaces on
`configurator-app-8oPgRM97` in 3.769 s. All were ready and editable, preserved
distinct values and prepared/revealed an exact draft; no crash, page-error or
request-failure events occurred. Earlier spare-third-tab failures under
concurrent load did not reproduce in this idle check. Their cause remains
unclassified; three passing tabs are not scale certification.

## Full Project Test List

The [project map](../../PROJECTS.md) owns project commands and package preconditions.
[TESTING.md](TESTING.md#test-projects) maps all product and engineering suites.
Use `dotnet test --project <affected-test-project.csproj> --configuration Release`
with actual paths, not a solution-level run or an absent source-system owner.

## AI Agent Operational Context

AI-agent workflow rules are not runtime operations. Keep them in
[AGENTS.md](../../AGENTS.md) and
[the context-engineering contract](../../.agents/CONTEXT_ENGINEERING.md) so
adopters do not have to scan agent tooling while using the utility.

The read-only `eng/agent-workflow` guard owns no state and never executes Git.
Use `validate-intents .agents/contract/intents.yaml` for YAML syntax and
`validate-commit -- git commit ... -- <literal-files>` to reject broad commit
pathspecs. Validation is not authorization to commit.

```bash
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- \
  validate-intents .agents/contract/intents.yaml
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- \
  validate-commit -- git commit --only -m "message" -- src/ExactFile.cs docs/ExactFile.md
```

The literal file examples illustrate validation grammar, not actual product
paths to stage. Select only owned paths when committing is explicitly authorized.
Never switch the shared checkout, create a worktree or alter another contributor's
changes contrary to the current task.

ReleaseEngineering separately validates commit syntax, immutable Change-Ids and
local release evidence. See its [operating instructions](../../eng/release/README.md).
No credentials, Event signer lists or machine MCP settings are transfer inputs.
