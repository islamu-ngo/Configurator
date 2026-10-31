# Terminal.Gui dependency decision and history

> **Audience:** Contributors | Maintainers | AI agents
> **Kind:** Accepted decision with historical evidence
> **Status:** Implemented; approved and verified 2026-10-13
> **Owner:** Terminal
> **Last Verified:** 2026-10-13
> **Decision authority:** Project Steward's explicit instruction to delete the fork
> **Source Anchors:** `Directory.Packages.props`, `NuGet.Config`, `src/Configurator.Terminal/Configurator.Terminal.csproj`, `.ci/scripts/write-dependency-notices.cs`, `eng/configurator/dependency-notices/`

## Decision and practical effect

Use official Terminal.Gui from NuGet.org with an exact centrally managed version,
generated dependency locks and complete retained component notices. Retire the
temporary `ISLAMU.Terminal.Gui` package, repository feed, patch, binary archive,
probe, approval/evidence generators and upstream rebuild lane. The migration
reviewed official `Terminal.Gui` **2.5.0**, the latest stable release observed on
2026-10-13; the central package file remains the authority for future versions.

This removes maintaining and rebasing a downstream UI toolkit. Configurator
maintains its own thin terminal adapter and normal dependency reviews. A version
pin fixes the resolved dependency identity; it does not waive component licenses,
prove behavior compatibility or eliminate ordinary upgrade checks. No floating
`latest` version or automatic unreviewed upgrade is introduced.

## Why a fork was originally selected

On 2026-09-01 the Steward approved a temporary downstream build of upstream
Terminal.Gui `v2.4.17`, tag object
`58f3af1a4afe5d2772be134b2299a0f78f35c93c`, commit
`d0a0ed9b150d3fc8aacf4ab07b7f7d91264fe6d6`.
It was packaged as `ISLAMU.Terminal.Gui 2.4.17-islamu.1`.

Terminal.Gui itself retained MIT licensing and upstream authorship. The patch
removed `TextMateSharp` and `TextMateSharp.Grammars`, excluded the TextMate-backed
implementations and selected plain built-in Markdown/code rendering. The public
`ISyntaxHighlighter` extension seam remained. This avoided conveying the grammar
and theme asset collection, which has component terms beyond its NuGet MIT label.
The terminal application uses ordinary form controls and did not need those
syntax-highlighting features.

The downstream build also used a distinct package ID, fixed version and explicit
modification notice, removed GitVersion, and disabled upstream post-pack mutation
of the global NuGet cache. No upstream implementation source was vendored here.
The repository instead retained a patch and rebuilt package, frozen patch/
assembly/closure evidence, an approval record and a 21-component closure/SBOM.
The approved archive SHA-256 was
`d61e31f90f58875bc8e8632a96b1882eb718b265f66da0309c816ca9510049ec`.

The fork was transferred into Configurator during the October 2026 foundation
extraction. CI cloned the pinned upstream tag, applied the patch and rebuilt it.
NuGet pack's random OPC metadata meant the rebuilt ZIP was not byte-identical;
verification compared semantic package content and deterministic assembly inputs
while preserving the approved original archive. This was careful provenance
control, but it added upstream build work and a separate dependency maintenance
process for an unused feature.

The historical approval documents the authorized removal strategy. It does not
identify a particular GPL, AGPL or commercial asset or demonstrate that complying
with the official dependency's component terms would be impossible. It must not
be treated as proof that official Terminal.Gui is inherently incompatible with
Configurator's MIT distribution.

## Investigation and change of direction

On 2026-10-13 the Steward requested investigation of the fork's necessity and
maintenance burden, then selected the official-package approach and explicitly
authorized deleting the fork and recording this history.

Official **2.5.0** still requires TextMateSharp and TextMateSharp.Grammars **2.0.4**.
The observed **2.6.0-develop.61** prerelease also retained them. Therefore an
upgrade alone does not remove the grammar/theme closure. Not using the feature
also does not remove the embedded assets from an untrimmed published package.
NuGet asset exclusion or unverified trimming would not be a supported substitute
for a dependency review.

The [upstream modularization discussion](https://github.com/tui-cs/Terminal.Gui/issues/5321)
proposes moving views and their dependencies into optional packages. Its closed
status does not establish that a suitable dependency-clean package was published;
the inspected package manifests still declare both TextMate dependencies.

| Approach considered | Consequence and decision |
| --- | --- |
| Official exact pin with complete notices | Normal package upgrades without patch rebasing or rebuilding upstream. Selected. |
| Official package with optional highlighting dependencies | A desirable future packaging improvement; not present in the inspected versions. |
| Retain the fork | Removes unused assets, but preserves patch, archive, CI rebuild and version-drift overhead. Retired. |
| Replace the terminal framework | Requires rewriting and qualifying the adapter. No demonstrated license incompatibility justified that cost. |

## Component terms and retained notices

The MIT labels of Terminal.Gui and TextMateSharp do not replace licenses of
embedded grammars, snippets, themes or native libraries. The package source at
TextMateSharp commit `622d1b240a5be474939857c0b2780249022f14a3` has **51** grammar
provenance manifests and **58** component registrations (**57** unique source
repository/revision pairs). Their recorded labels are 40 MIT, 14 TextMate Bundle
License, one BSD, one GitHub License, one Apache2 and one Apache-2.0.
These are provenance labels, not a claim that every asset is MIT or that every
nonstandard label is an incompatible license.

The inspected TextMate Bundle terms permit copying, modification, selling and
distribution, with file-specific exceptions preserved. The CSS entry's GitHub
License text retains MIT-style terms and TextMate ancestry. The Dart entry has
BSD redistribution, notice and non-endorsement obligations. Original Apache
terms and the available Docker NOTICE are retained. No copyleft or commercial
restriction was found in the reviewed component records and original terms.

The grammar NuGet archive embeds assets and provenance in its DLL, with no
standalone license/notice text. Terminal.Gui and TextMateSharp also declare MIT
without shipping their upstream license files. The existing notice writer only
copied ordinary package files, so switching the pin without supplementation
would leave distribution notices incomplete.

Reviewed text now lives in `eng/configurator/dependency-notices/`:

| Exact reviewed package | Retained supplemental material |
| --- | --- |
| Terminal.Gui 2.5.0 | Original upstream MIT LICENSE, including Novell/Microsoft attribution. |
| TextMateSharp 2.0.4 | Original MIT LICENSE, copyright Daniel Peñalba 2021. |
| TextMateSharp.Grammars 2.0.4 | Original grammar source licenses, bundled license details, available NOTICE and theme-family notices. |

`Onigwrap 1.0.11` already ships `THIRD-PARTY-NOTICES.TXT` with its native binaries.
That original file is copied unchanged, including Oniguruma's BSD notice and the
other upstream acknowledgements. No supplemental Onigwrap implementation or
rebuilt native package is introduced.

Some upstream provenance is incomplete. The Razor repository was recovered at
its exact recorded commit through a preserved repository. The x86asm manifest
omits its source revision; its publisher's MIT terms were checked at a revision
predating the package source snapshot. Six contributed theme conversions lack
exact original-source declarations. Palette comparisons support Dracula and
Atom/One families, but do not prove a unique or byte-identical lineage. Their
published TextMateSharp MIT contribution terms and identifiable permissive
family notices are retained conservatively; Visual Studio theme conversions
have no identified separate license. These evidence limits remain explicit,
rather than inventing source identities or calling the metadata legal proof.

## Packaging mechanism and future upgrades

The terminal client still references Presentation, Core and Artifacts. The
package replacement does not move definition, answer custody or protected-save
policy into the UI toolkit. `SetupSecretTextField` continues to hold bullets in
the widget, keep the actual input in a separate clearable buffer, block
clipboard/context/history commands and clear editor state on the existing
lifecycle boundaries.

The existing notice writer walks the actual restored package closure. When a
package has reviewed supplemental material, it requires the matching version
and canonical NuGet content hash in its `manifest.json`. Ordinary packages must
also match the assets file and restored NuGet metadata. This uses NuGet's content
identity, not the raw signed ZIP hash, so signature/ZIP metadata differences do
not cause false package changes. Only approved text filenames are copied into
that dependency's `supplemental/` directory. Missing/mismatched review data,
linked notices and executable notices reject publication. Unrelated surfaces
do not acquire Terminal.Gui/TextMate notice supplements.
Native publication replaces the generated notice tree before writing the
current closure, removing retired fork notices on same-destination republish.

For an upgrade, change the authoritative central version, regenerate affected
locks through native restore, review changed dependency/embedded-asset terms,
update applicable supplemental notices and review hashes, then run the terminal
owner and actual packaging checks. Preserve passing evidence for unchanged
owners. There is no upstream clone, patch application, custom feed, custom
package build, archive ratchet or downstream release to maintain.

## Source register

Accessed 2026-10-13. Original notice text and each grammar component's recorded
repository/revision remain in the shipped supplemental notice file.

- [Official package and dependency list](https://www.nuget.org/packages/Terminal.Gui/2.5.0#dependencies-body-tab), [official release notes](https://github.com/tui-cs/Terminal.Gui/releases/tag/v2.5.0), [MIT license](https://github.com/tui-cs/Terminal.Gui/blob/v2.5.0/LICENSE).
- [TextMateSharp package source](https://github.com/danipen/TextMateSharp/tree/622d1b240a5be474939857c0b2780249022f14a3), [project license](https://github.com/danipen/TextMateSharp/blob/622d1b240a5be474939857c0b2780249022f14a3/LICENSE.md).
- [CSS component terms](https://github.com/danipen/TextMateSharp/blob/622d1b240a5be474939857c0b2780249022f14a3/src/TextMateSharp.Grammars/Resources/Grammars/css/cgmanifest.json), [Dart component terms](https://github.com/danipen/TextMateSharp/blob/622d1b240a5be474939857c0b2780249022f14a3/src/TextMateSharp.Grammars/Resources/Grammars/dart/cgmanifest.json).
- [Recovered Razor original license](https://github.com/peterblazejewicz/language-cshtml/blob/e6e54d5a86a28cc1e44609a32aaa10a244cd3f81/LICENSE.md), [x86asm publisher license](https://github.com/haxo-games/Universal-Assembly-Syntax-Highlighting/blob/db22e2f015a2cac3315141f40f536f3d8534c5eb/LICENSE).
- [VS Code license](https://github.com/microsoft/vscode/blob/18e3a1ec544e6907be1e944a94c496e302073435/LICENSE.txt) and [Colorsublime ancestry notice](https://github.com/microsoft/vscode/blob/18e3a1ec544e6907be1e944a94c496e302073435/ThirdPartyNotices.txt).
- [Dracula license](https://github.com/dracula/visual-studio-code/blob/a08a206f2c8420ba3c05f0e8d01d43b2f933fdf8/LICENSE), [Akamud One Dark license](https://github.com/akamud/vscode-theme-onedark/blob/a8be970644982221f9b61fb1c4b3da74b4beab79/LICENSE), [Akamud One Light license](https://github.com/akamud/vscode-theme-onelight/blob/5866e900db932d580e978a58db42f65cde07998b/LICENSE), [Atom One Dark license](https://github.com/atom/one-dark-syntax/blob/9c96f4454362267ac45322063e193ccf9d2debb1/LICENSE.md), [Atom One Light license](https://github.com/atom/one-light-syntax/blob/d84579027410c576086dfca14d934c4bd74b0438/LICENSE.md).
- [Onigwrap native/component notices](https://github.com/aikawayataro/Onigwrap/blob/dd8a1cbbe3190c18033b96f2922b95c1761ae793/THIRD-PARTY-NOTICES.TXT), also inspected in the distributed 1.0.11 NuGet archive.

The notice-publication change was implemented from a source-free functional
handoff in a separate agent context using the existing repository writer and
native NuGet metadata. No external implementation source informed its structure.
Only original licensing/provenance text was retained from dependency research.
The original extraction manifests and verification records remain historical
evidence, with retirement annotations rather than rewritten history.

## Executed verification

All .NET commands use SDK 10.0.302 with `TMPDIR="$HOME/.cache/agent-tmp"`.

| Check | Executed result |
| --- | --- |
| Terminal Release build and full owner suite before replacement | Zero warnings/errors; 38 passed, zero failed/skipped. |
| Native force-evaluate restores for terminal and its tests | Exit 0; official package and transitive locks regenerated. |
| Terminal Release build and full owner suite on official 2.5.0 | Zero warnings/errors; 38 passed, zero failed/skipped; no terminal source changes needed. |
| Complete solution Release build | Exit 0; zero warnings/errors. |
| Supplemental-review missing-version/hash regression | Initial generator rejected neither scenario; both tests failed. After implementation, 2 passed, zero failed/skipped. |
| Actual terminal Linux-x64 publication and same-destination republish | Both exit 0; 29 dependency notice packages retained. A seeded obsolete fork directory was removed. All three supplements and Onigwrap's upstream notice matched source bytes. |
| CLI and Desktop Linux-x64 publication | Exit 0 for each; six and 31 dependency notice packages retained respectively. |
| Store-free and store-enabled static Browser publication | Exit 0 for each; 36 dependency notice packages retained; the explicit store variant contains four reviewed revisions. |
| NuGet vulnerability audit including transitives, without restore | Exit 0; no vulnerable packages reported for the terminal project. |
| Repository dependency-license metadata policy | Exit 0; 104 package/version pairs accepted. Asset terms were reviewed separately as described above. |
| External Action pin validator | Exit 0; six external references accepted. |
| Owned whitespace, local Markdown link targets and rule twins | Exit 0; no missing local targets; matching IP-rule twins. |
| AgentWorkflow engineering owner | Two passed, zero failed/skipped. |
| ReleaseEngineering engineering owner | 406 passed, one failed, zero skipped (407 total); the unchanged fragment-policy baseline is detailed below. |
| Full architecture/package owner against all fresh outputs | 43 passed, zero failed/skipped, including the final independent supplemental-review scenarios and matching supplement publication. |
| Exact-path C# whitespace formatter verification | Exit 0 after formatting the new test's anonymous object; no semantic test change. |
| Actual standalone terminal executable `--help` | Exit 0; public usage printed without SDK hosting. |

Code review approved the final implementation after the stale-notice cleanup
and independent missing-version/hash scenarios were corrected. This migration
does not qualify additional native hosts, assistive technology, signing or
remote publication. Unchanged Core, Artifacts, Presentation, CLI, Desktop and
Browser behavioral suites were not rerun; their actual package/compiled closure
checks passed in the architecture owner.

Exact command evidence (all commands ran from Configurator):

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet restore src/Configurator.Terminal/Configurator.Terminal.csproj --force-evaluate --verbosity quiet
dotnet restore tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --force-evaluate --verbosity quiet
dotnet build src/Configurator.Terminal/Configurator.Terminal.csproj --configuration Release --no-restore --verbosity quiet
dotnet test --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --configuration Release --no-restore -- --minimum-expected-tests 1
dotnet build --configuration Release --verbosity quiet
dotnet list src/Configurator.Terminal/Configurator.Terminal.csproj package --vulnerable --include-transitive --no-restore --format json
dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .
dotnet run .ci/scripts/validate-action-pins.cs -- .github/workflows
dotnet format whitespace Configurator.slnx --verify-no-changes --no-restore --include tests/Configurator.Architecture.Tests/DependencyNoticeTests.cs tests/Configurator.Architecture.Tests/StandalonePackageTests.cs tests/Configurator.Architecture.Tests/StandaloneArchitectureTests.cs
dotnet test --project eng/agent-workflow/tests/Configurator.AgentWorkflow.Tests/Configurator.AgentWorkflow.Tests.csproj --configuration Release -- --minimum-expected-tests 1
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release -- --minimum-expected-tests 1
```

Actual package construction used `dotnet msbuild
eng/configurator/Configurator.Release.proj` with `-verbosity:minimal -nr:false`:
`PublishConfigurator`, `ConfiguratorRid=linux-x64`, and each of `cli`, `terminal`
and `desktop`, writing to `.tmp/terminal-upstream-packages`. Terminal was also
republished to that same root after seeding a synthetic retired-fork notice
directory. `PublishConfiguratorBrowser` wrote the store-free package to that
root and the explicit `DefinitionStoreSourceRoot=$PWD/store` variant to
`.tmp/terminal-upstream-packages-with-store`.

The final package suite used those actual destinations:

```bash
CONFIGURATOR_PACKAGE_ROOT="$PWD/.tmp/terminal-upstream-packages" CONFIGURATOR_STORE_PACKAGE_ROOT="$PWD/.tmp/terminal-upstream-packages-with-store" dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-build --no-restore -- --minimum-expected-tests 1
```

The negative generator tests' initial failing and subsequent passing reports are
retained locally under `.tmp/dependency-notice-supplementation/`. Final command
logs are local session evidence under
`$TMPDIR/terminal-gui-investigation.98k5kj/`, not distribution inputs.

### Unrelated release-policy baseline

`RepositoryChangeFragmentsPassReleaseInputPolicy` reported
`fragment_restricted_detail_marker` for existing change fragments
`CHG-01M4G97ENQW00FR9W0DWSMC7FQ` and `CHG-01M4G98CPT6MW3WNDXMWNHFDX7`.
Their working-file Git blobs match HEAD exactly:
`3e0a27ac14d71d58aecac7543863b35de1e17b3c` and
`bbf7c4cf1ebdfcbd5fde06ccf409a71539f7791a`.
The release validator and its tests are also unchanged. This existing fragment
policy failure is recorded separately from the Terminal.Gui migration; no
fragment, validator or unrelated test was edited or weakened.
