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

### Browser UI refinement evidence

The screenshot-driven interface refinement on 2026-10-10 corrected the vendor
cascade, added explicit shared native-control styling, refined shell/footer
composition, separated definition entry sources and grouped workspace controls.
The [interface anchor](BROWSER_UI.md#interface-refinement) records ownership,
independent design and official standards/API sources.

| Check | Executed result | Local evidence |
| --- | --- | --- |
| Shell baseline before edits | 5 passed, zero failures/skips; 1.339 s | Session command output |
| Final Browser owner semantics | 125 passed, zero failures/skips; 6.772 s | `.tmp/browser-ui-refinement-tests.log` |
| Browser Release build | Exit 0; zero warnings/errors; 2.91 s | `.tmp/browser-ui-refinement-build.log` |
| Changed C# format verification | Exit 0 | `.tmp/browser-ui-refinement-format.log` |
| Fresh Store-enabled and Store-free publication | Both exit 0 | `.tmp/browser-ui-refinement-publish.log`, `.tmp/browser-ui-refinement-without-store-publish.log` |
| Published asset/snapshot inventory | 4 passed, zero failures/skips | `.tmp/browser-ui-refinement-packages.log` |
| Local preview routing/assets | Nine HTTP 200 checks; missing framework asset HTTP 404 | Session `curl` checks, isolated port 5084 |

Executed commands used SDK 10.0.302 and
`TMPDIR="$HOME/.cache/agent-tmp"`:

```bash
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --treenode-filter "/*/*/BrowserShellTests/*" --minimum-expected-tests 5 --no-progress
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 125 --no-progress
dotnet build src/Configurator.Browser/Configurator.Browser.csproj --configuration Release --verbosity quiet
dotnet format Configurator.slnx --verify-no-changes --no-restore --verbosity quiet --include src/Configurator.Browser/Services/AppearanceThemeService.cs tests/Configurator.Browser.Tests/BrowserSourceEntryTests.cs tests/Configurator.Browser.Tests/BrowserStoreIndependenceTests.cs
dotnet msbuild eng/configurator/Configurator.Release.proj -target:PublishConfiguratorBrowser -property:Configuration=Release -property:ConfiguratorOutputRoot="$PWD/.tmp/browser-ui-refinement" -property:DefinitionStoreSourceRoot="$PWD/store" -verbosity:quiet
dotnet msbuild eng/configurator/Configurator.Release.proj -target:PublishConfiguratorBrowser -property:Configuration=Release -property:ConfiguratorOutputRoot="$PWD/.tmp/browser-ui-refinement-without-store" -verbosity:quiet
```

Package tests set `CONFIGURATOR_PACKAGE_ROOT` to
`.tmp/browser-ui-refinement-without-store` and
`CONFIGURATOR_STORE_PACKAGE_ROOT` to `.tmp/browser-ui-refinement`, then executed:

```bash
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --treenode-filter "/*/*/BrowserStorePackageTests/*" --minimum-expected-tests 4 --no-progress
```

The isolated Apache preview uses port 5084 and leaves the existing port 5081
preview intact. Syntax checking initially preceded publication and rejected the
missing document root; after publication it passed and the server started.
No browser was exposed by the automation inventory, and opening the in-app
browser returned unavailable. Visual, keyboard, screen-reader, zoom, RTL and
theme contrast verification for these refinements remain unexecuted. Earlier
browser screenshots and manual results below describe earlier assets and do
not qualify this refinement. No product references, dependencies, custody
policy or native owners changed, so unrelated owner suites were not rerun.

### Browser interface and optional store evidence

The authorized `browser-ui-and-definition-store` implementation was verified
directly in the checkout on 2026-10-10 with SDK 10.0.302. No history or remote
publication was recorded. The existing evidence below remains historical;
these results describe the new interface and optional-store integration.

| Gate | Executed result | Local evidence |
| --- | --- | --- |
| Locked solution restore and Release build | Exit 0; zero warnings/errors; build 10.74 s | `.tmp/browser-ui-and-definition-store/final-restore.log`, `final-build.log` |
| Full solution format verification | Exit 0 after targeted owned formatting; final router slice also exit 0 | `.tmp/browser-ui-and-definition-store/final-format-green.log`, `final-router-format.log` |
| Browser owner suite after visible-focus correction | 124 passed, zero failures/skips; 6.176 s | `.tmp/browser-ui-and-definition-store/final-browser-visible-green.log` |
| Presentation owner suite | 36 passed, zero failures/skips; 1.575 s | `.tmp/browser-ui-and-definition-store/final-presentation-tests.log` |
| Native store self-check and freshness | 26 passed, zero failures; two revisions generated and checked | `.tmp/browser-ui-and-definition-store/store-self-test-green.log`, `store-write.log`, `store-check.log` |
| Final secret-recovery focus slice | 3/3 privacy tests; affected format exit 0; actual focus moved to visible consent after recovery | `.tmp/browser-ui-and-definition-store/final-visible-focus-tests.log`, `final-visible-focus-format.log`, `delivery-recovery-focus-green.png` |
| Actual store-enabled and store-free publication | Both exit 0; source-absent publication also exit 0 and source restored | `.tmp/browser-with-store-visible-final-publish.log`, `.tmp/browser-without-store-visible-final-publish.log`, `.tmp/browser-without-store-final-absent-source-publish.log` |
| Actual package store/UI inventories and Browser closures | 4/4 and 2/2 passed against final outputs | `.tmp/browser-store-visible-final-package-tests.log`, `.tmp/browser-standalone-visible-final-package-tests.log` |
| Compiled product dependency closure | 7/7 passed | `.tmp/browser-ui-and-definition-store/final-compiled-closure.log` |
| MIT dependency policy and generated ratchets | Exit 0; 86 package/version pairs; ratchets current 3/3 | `.tmp/browser-ui-and-definition-store/final-license-policy.log`, `final-ratchets.log` |

Executed owner commands used `TMPDIR="$HOME/.cache/agent-tmp"` and MTP:

```bash
dotnet restore Configurator.slnx --locked-mode --verbosity quiet
dotnet build Configurator.slnx --configuration Release --verbosity quiet
dotnet format Configurator.slnx --verify-no-changes --no-restore --verbosity quiet
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 1 --no-progress
dotnet test --project tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 1 --no-progress
dotnet run eng/configurator/BuildDefinitionStore.cs -- --self-test
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --check
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser -p:ConfiguratorOutputRoot="$PWD/.tmp/browser-with-store" -p:DefinitionStoreSourceRoot="$PWD/store"
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser -p:ConfiguratorOutputRoot="$PWD/.tmp/browser-without-store"
dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .
dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --check
```

Package checks bound `CONFIGURATOR_PACKAGE_ROOT` to `.tmp/browser-without-store`
and `CONFIGURATOR_STORE_PACKAGE_ROOT` to `.tmp/browser-with-store`. They executed
`BrowserStorePackageTests` (minimum 4), `StandalonePackageTests/BrowserPackage*`
(minimum 2), and `StandaloneArchitectureTests/CompiledProductClosureHasNoEventOrTestDependency`
(minimum 7) through the Architecture test project with the same Release,
`--no-build --no-restore`, nonempty-count and `--no-progress` options.

Published acceptance used Apache 2.4.68 on three isolated localhost origins and
Google Chrome 153.0.8010.52 through Playwright. DevTools, Tavily and Context7 MCP
were unavailable; official API documentation supplied research facts. Package
worker identities are `RHpkSzhl` (store) and `A/UkP6ID` (store-free).
Screenshots live under `.tmp/browser-ui-and-definition-store/delivery-*.png`.
Observed outcomes:

- Home, Open, Help, Store and project details reflowed at 1440 and 390 pixels;
  320, 360 and 768 pixel RTL checks had no horizontal page overflow. The 360
  pixel viewport supplies 400%-equivalent reflow for a 1440 pixel window.
- System appearance followed emulated light/dark changes. All four explicit
  palettes rendered, with body contrast ratios 16.27, 16.67, 21 and 20.12;
  primary-button contrast exceeded 10 after transitions. Forced colors and
  reduced motion were inspected. These measurements do not certify every
  element or assistive technology.
- Escape within the inline navigation collapsed it and returned focus to Menu.
  Skip focused main without changing the route, answers or prepared artifacts.
  Cancelled departure preserved synthetic answers/output and trigger focus;
  confirmed departure removed the workspace before opening another route.
- A host returning 403 for every optional store file still installed the core
  worker, enabled controls and prepared three drafts without a store request.
  A reviewed store revision reopened offline. A store-free package admitted a
  local file, deliberately retained public metadata, reopened it offline and
  prepared a draft. A real independent public HTTPS file was admitted; a
  mismatched digest rejected with local upload still available. Copied source
  links contained pinned definition identity and selectors only.
- Secret controls were masked; complete preview and deliberate clipboard copy
  worked with synthetic values. Revocation cleared secret leaves and output,
  preserving ordinary answers. Synthetic sentinels were absent from local and
  session storage, public cache keys/text payloads and resource URLs; editing,
  preparation and revocation emitted no answer-bearing worker messages.
  Recovery originally removed its focused button and left focus on body;
  the final current-form consent action now restores focus to the existing
  consent control and scrolls it into view, verified in the published package.
- Event tenant insertion focused the new identifier. Duplicate-row review
  focused the second row; correction produced one manifest with exactly two
  distinct tenant names and no inactive artifacts. Narrow nested controls had
  no horizontal page overflow. Late operation rejection is separately covered
  by controlled owner tests; browser-managed updates left the existing realm
  on its old worker until clean reload.
- The clean final origin reported zero console errors or CSP diagnostics.

Download dispatch reported the intended `service.json` filename. Saved-file
completion is **unverified**: this isolated Chrome harness closed on product
downloads and on an independent standalone synthetic Blob download. No native
zoom control, physical network disconnection, screen-reader audit or additional
browser engine was available; offline media/network behavior used Playwright
emulation. These limits are distinct from green owner/package checks. No native
client, target apply, hosting, signer or remote publication claim is made.

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

## Maintainer authoring verification

The authorized maintainer-authoring implementation was executed directly in the
Configurator checkout on 2026-10-10 with SDK10.0.302 and
`TMPDIR="$HOME/.cache/agent-tmp"`. Existing browser/store work and user strategy
documents were preserved. No branch, worktree, staging, commit or remote
publication was created. The [maintainer contract](MAINTAINER_AUTHORING.md)
describes the delivered source/compiler/CLI flow; the
[public integration guide](../public/integrate-your-tool.md) describes adoption.

| Executed gate | Result | Evidence |
| --- | --- | --- |
| Locked solution restore | Exit0 | `.tmp/maintainer-restore.log` |
| Post-format normal solution Release build | Exit0, zero warnings/errors,111.08s | `.tmp/maintainer-solution-build.log` |
| Full solution format verification | Exit0 after formatting only five attributable source/test files and ordering their imports | `.tmp/maintainer-format-final.log` |
| Core owner |122 passed,0 failed,0 skipped | `.tmp/maintainer-core-final-tests.log` |
| CLI owner |58 passed,0 failed,0 skipped | `.tmp/maintainer-cli-final-tests.log` |
| Terminal owner |38 passed,0 failed,0 skipped | `.tmp/maintainer-terminal-final-tests.log` |
| Desktop headless owner |67 passed,0 failed,0 skipped | `.tmp/maintainer-desktop-final-tests.log` |
| Browser owner |135 passed,0 failed,0 skipped, including regenerated Event6 resources | `.tmp/maintainer-browser-final-tests.log` |
| Presentation owner |36 passed,0 failed,0 skipped | `.tmp/maintainer-presentation-final-tests.log` |
| Linux X11 native host |13 checks passed; no broader host/accessibility claim | `.tmp/maintainer-native-host.log` |
| Generated runtime/authoring and command schemas | Native `--check` exits0 for both schema owners | Task-owned execution context |
| Native store integrity |46 self-tests passed; baseline-required write/check passed for four revisions and generated curation schema | Task-owned execution context |
| Actual store-free and store-enabled browser publication | Both exit0, each retaining original notices for36 dependency packages | `.tmp/maintainer-browser-no-store-publish.log`, `.tmp/maintainer-browser-with-store-publish.log` |
| Candidate standalone skill adoption | Actual tool-managed identity, paired YAML/JSON bytes, nested literal outputs, protected mode600, strict/acknowledged/refused imports, comment/no-op/revision, retained history and source-only contribution | `.tmp/maintainer-adoption/REVIEW.md` |
| Final standalone CLI publication | Exit0; packaged schema and portable skill; executable SHA-256 `5031706878d98386f78f69f07845b680d107c183993ee8aab07587c827e59f97` | `.tmp/maintainer-cli-final-publish.log` |
| Final compiler adoption confirmation | Reserved `$defs` entry named `$id`: init/check/build all exit0; existing nested revision2 check exit0 with unchanged canonical digest | `.tmp/maintainer-adoption/evidence/final-reserved-*.json`, `final-nested-check.json` |
| Event ordinary source/host owner |16 passed,0 failed,0 skipped with compiler environment variables absent; exporter build zero warnings/errors | Event technical export anchor |
| Event explicit pinned compiler owner |11 passed,0 failed,0 skipped; final CLI write/check and source-only check exit0;361 environment definitions current | Event technical export anchor |
| Final Terminal Linux-x64 publication | Exit0; retained notices for26 dependency packages | `.tmp/maintainer-terminal-final-publish.log` |
| Final Desktop Linux-x64 publication | Exit0; retained notices for29 dependency packages | `.tmp/maintainer-desktop-final-publish.log` |
| Published Browser custody and updates | Actual Chromium153 Linux scenarios passed with the bounded export recovery described below | `.tmp/maintainer-platform/qa-evidence.md` |
| Clean Browser republications | Both exit0; retired stylesheet/sidecars removed; four retained store revisions and36 dependency notices | `.tmp/maintainer-browser-clean-publish.log`, `.tmp/maintainer-browser-store-clean-publish.log` |
| Final architecture/package owner |41 passed,0 failed,0 skipped,6.689s; actual CLI/Terminal/Desktop Linux packages and both Browser variants | `.tmp/maintainer-architecture-package-closure.log` |
| Package regression owner build and format | Normal Release build zero warnings/errors; exact-path whitespace verification exit0 | `.tmp/maintainer-package-fix-build.log`, `.tmp/maintainer-package-fix-format.log` |

The accepted owner commands were physically run against the normal solution
build, without analyzer suppression:

```bash
dotnet restore Configurator.slnx --locked-mode --verbosity quiet
dotnet build Configurator.slnx --configuration Release --no-restore --verbosity quiet -p:UseSharedCompilation=false
dotnet format Configurator.slnx --verify-no-changes --no-restore --verbosity quiet
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 1 --no-progress
```

The same test command was executed for CLI, Terminal, Desktop, Browser and
Presentation, with the exact counts above. The native host command was:

```bash
xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 13
```

Independent review exposed inherited-private-default, expansion-budget,
schema-resource and recorded-anchor failures before their fixes. Final review
found no remaining issue in those bounded changes. Early zero-test diagnostic
runs and builds with `RunAnalyzers=false` are excluded from acceptance evidence.
The final Core suite also covers reserved `$defs` names without confusing them
with schema resource keywords. Native store history retains neutral2 and Event5
bytes; current sources compile into neutral3 and Event6 instead of reusing their
identities.

Source and package owners total497 passing Configurator cases, with no failed or
skipped cases in their accepted runs. Unit tests and publication alone do not
prove browser platform scenarios.
Tavily and Context7 were unavailable; official-source
Firecrawl research was the disclosed fallback. No remote CI, other-host,
stakeholder, future-agent, legal or scholarly certification is implied.

Event commands were executed in the sibling Event checkout with the same SDK and
temporary-directory configuration. The two actual test selections were:

```bash
env -u CONFIGURATOR_COMPILER -u CONFIGURATOR_COMPILER_SHA256 dotnet test --project tests/Event.Wire.Contracts.UnitTests/Event.Wire.Contracts.UnitTests.csproj --no-restore --configuration Release --verbosity quiet -p:BuildProjectReferences=false -p:UseSharedCompilation=false -- --treenode-filter "/*/*/*ConfiguratorDefinitionArtifactTests/*" --minimum-expected-tests 16 --no-progress --maximum-parallel-tests 1
CONFIGURATOR_COMPILER=/home/amir/ISLAMU/Github/Configurator/artifacts/configurator/cli/linux-x64/islamu-configurator CONFIGURATOR_COMPILER_SHA256=5031706878d98386f78f69f07845b680d107c183993ee8aab07587c827e59f97 dotnet test --project tests/Event.Wire.Contracts.UnitTests/Event.Wire.Contracts.UnitTests.csproj --no-restore --configuration Release --verbosity quiet -p:BuildProjectReferences=false -p:UseSharedCompilation=false -- --treenode-filter "/*/*/*ConfiguratorCompilerHandoffTests/*" --minimum-expected-tests 11 --no-progress --maximum-parallel-tests 1
```

The final Event release receipt has SHA-256
`8a5e5e57c2501903efb20d49c3d5b4e1d79fb39228353f282f2f0cb42ba5867b`.
The source copies have digest
`106504676e3087b35eef0761702dc88714d8f2ab9a4da823d7356f348ee39304`;
canonical Event/example/store revision6 copies have digest
`b50171ca0a52aa48c3099088db925e440791aff3a450e03e1caca97cf3e4066e`.
All copies compare byte-identical. Generated provenance describes the compiler
used; it grants no publication, trust promotion or target apply authority.

Published Browser inspection used Chromium153.0.8010.52 on Linux with an isolated
Apache host and synthetic fixtures. Local compact YAML was admitted before
answers; explicit public retention stored canonical runtime JSON, and simulated
offline reload/reopening started a fresh workspace. Independent draft145-byte
and complete204-byte vectors matched actual clipboard and download output.
Revoking consent cleared three top-level/nested/list secret inputs and removed
prepared output while preserving public values. Clear and navigation reset the
workspace. Scans of82 active and83 final cached responses found no answer
sentinels; local/session storage and IndexedDB were empty. Deliberate application
connection attempts were blocked; observed answer-time traffic carried only a
public asset GET with no body.

Two independent active tabs retained their original workspace and cached Core
asset when the actual store-enabled package installed a waiting worker. After
closing both tabs, the new package activated and its Store booted offline.
Invalid authoring rejected without displaying the invalid scalar and restored
file-picker focus. Keyboard focus and375px reflow were inspected; CSS200% zoom
kept content within the viewport. The exact observations, package manifest
hashes, synthetic vectors and native host commands are in the task evidence.
Only owned tabs/host were closed and the synthetic clipboard was cleared.

Chromium's simulated offline mode crashed a generic Blob download control as
well as actual download clicks. Recovery verified both exports with simulated
offline disabled and Apache physically stopped, confirmed by failed curl. This
proves export without reaching its host; it does not prove physical disconnection
of every network. Offline boot/edit/draft/copy were separately observed under
simulated disconnection. Other engines, assistive technology and native browser
zoom remain unqualified; browser exports carry no native protected-save promise.

The first full architecture/package run executed41 cases:39 passed,2 failed,
none skipped. A removed stylesheet remained in a reused Browser publish directory;
`PublishConfiguratorBrowser` now replaces its generated destination before
publication. The store inventory test also rejected standard SDK Brotli/gzip
sidecars; its closed-file assertion now verifies that each transport encoding
decodes to exactly its admitted canonical bytes, including end-of-stream.
The normal Architecture owner rebuild passed with zero warnings/errors (2.67s),
and the exact store transport case passed1/1 before republishing the Browser
variants. The final full package run passed41/41, zero failures/skips; the initial
failure is retained in `.tmp/maintainer-architecture-final-tests.log`.

The final package command was:

```bash
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet -- --minimum-expected-tests 41 --no-progress
```

All three native Linux payloads were physically published with
`dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator
-p:ConfiguratorSurface=<cli|terminal|desktop> -p:ConfiguratorRid=linux-x64
-p:UseSharedCompilation=false`, one surface per command. Both Browser variants
were republished through `PublishConfiguratorBrowser`, passing an empty
`DefinitionStoreSourceRoot` for the store-free package and the explicit local
`store/` root plus `ConfiguratorOutputRoot=artifacts/configurator-with-store` for
the store-enabled package. Final worker-manifest SHA-256 values are
`63dfce4aa1a2e7bd18eb545c7594a6b11bc6443a8d4529fca7ca38388f4daf51`
and `3896d753e850330f44495cf4f331c12a078e7ba64074f6f0acb49bcfb3bfb3dd`,
respectively. Earlier platform evidence retains its original package identities;
fresh package confirmation is appended to that evidence rather than relabeling it.

The final clean-package confirmation passed against both manifest identities:
fresh local YAML admission and exact204-byte complete clipboard output, default
consent off, three masked secret inputs, and complete revocation clearing.
Active/final scans of82/83 cache responses found no answer sentinels; storage and
IndexedDB remained empty. Two active old tabs retained separate values and Core
assets while the new worker waited. A cleared tab booted/reopened offline with
empty answers while the other tab kept the update waiting; after both closed,
the new Store activated, discarded the old app cache and booted offline.
Earlier download, keyboard, invalid-source, navigation and connection-guard
observations retain their original package hashes and were not redundantly rerun.
Owned host/tabs were closed, online mode restored and synthetic clipboard cleared.

## Full Project Test List

### Desktop readiness evidence

The 2026-10-09 readiness work recorded 64 Desktop MTP passes, 26 real native
protected-artifact passes and thirteen named Linux x64/X11 NativeHost passes.
The native executable has strict RID/backend/count/timeout guards; missing
display or incomplete results reject qualification. Headless and native-host
checks are distinct from actual published application proof.

Published-app isolation uses Bubblewrap with a new network namespace, a clean
operator home, no mounted source/NuGet tree and a masked `/usr/share/dotnet`.
The single-file executable uses its embedded runtime, system native libraries,
Xvfb's local X11 socket, distro font cache and software rendering. A first
over-minimal graphics environment aborted; supplying its native font/graphics
prerequisites allowed startup. This qualifies only that observed environment,
not all Linux desktop backends. Synthetic operator output remains outside shared
evidence; capture only value-free outcomes and public source/payload identities.

The task evidence under `.omo/evidence/20261009-desktop-cross-platform-packaging/`
contains exact commands/counts, inventories, reviews and unrun limits. Partial
isolated-app checks and automated GTK interaction do not replace the complete
published workflow, physical keyboard/scaling or assistive-technology proof.
Readiness stays unqualified until all required evidence is present. No installer,
Windows/macOS qualification, signing, promotion or publication was performed.

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
