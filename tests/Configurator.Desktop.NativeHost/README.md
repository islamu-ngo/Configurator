# Native profile qualification

Phase13 source adds three required guards to the existing thirty. The executable
requires exactly `--minimum-expected-checks 33` and completion of the entire
named set. No previous guard is removed.

- `D31-profile-library-custody`: real `NativeProfileContainerCodec` seal/open,
  blind inspection refusal, secret-stripped comparison and protected local store.
- `D32-profile-controls-focus`: rendered password and confirmation masking,
  deliberate Open click, credential retirement and focus, nonempty retained
  slots, ordinary snapshot and visible text/editor/automation disclosure checks.
  It visits Open, Review and Settings/Privacy using the real profile views.
- `D33-profile-protected-target`: held secret composition through the native
  protected writer, exact synthetic output, owner-only permissions and
  create-only conflict without replacement.

All inputs are synthetic. These scenarios do not create keyring entries.
They do not establish actual-host recovery, assistive-technology conformance,
published Desktop execution or Browser custody/performance qualification.

Run from the repository root on Linux x64 with an X11 session. The Core analyzer
blocker recorded during source authorship has been resolved; subsequent staged
Desktop builds passed without compiler or analyzer warnings.

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --verbosity quiet
CONFIGURATOR_NATIVE_UI_CULTURE=en dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 33 --capture-directory "$PWD/.tmp/profile-native/en"
CONFIGURATOR_NATIVE_UI_CULTURE=ar dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 33 --capture-directory "$PWD/.tmp/profile-native/ar"
```

The five new capture state prefixes are `profile-open-masked`,
`profile-open-held`, `profile-review-held`, `profile-settings-held` and
`profile-protected-target`. Each uses the existing real `RenderTargetBitmap`
capture with Light, Dark and HighContrast themes, desktop/minimum/wide sizes,
and top/end scroll positions: 90 profile PNGs per culture, 180 across both
invocations, plus JSON metadata and all existing captures. These are expected
counts, not executed evidence. The parent must inspect the rendered PNGs for
clipping, overlap, focus visibility, RTL layout and masking. Capture metadata
contains no textbox values.

## Scoped profile checks

Earlier full runs failed `D27-responsive-scale-focus` in the rail, compact
settings and inspector controls. The retained logs are
`profiles-nativehost-{en,scale-diagnosis,scale-ancestry}.log` under the cached
agent temporary directory. Type/ancestor/font-size diagnostics contain no UI
values. The view templates now inherit the window's text scale. Current English
and Arabic Linux x64/X11 full cohorts both pass 33/33 guards; manual dialog,
keyboard, RTL, scaling, assistive-technology and published-payload checks remain
separate qualification work.

An explicit cohort selector exercises exactly D31-D33 without claiming a full
native qualification. It requires a minimum of three and reports
`profiles-only`; the default command still requires all 33 named guards:

```bash
CONFIGURATOR_NATIVE_UI_CULTURE=en xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 3 --profiles-only --capture-directory "$PWD/.tmp/profile-native/profiles-only/en"
CONFIGURATOR_NATIVE_UI_CULTURE=ar xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 3 --profiles-only --capture-directory "$PWD/.tmp/profile-native/profiles-only/ar"
```

The package owner must first publish CLI, Terminal, Desktop for Linux x64 and
Browser through the commands in [the packaging guide](../../docs/public/packaging.md#2-compile-specific-interfaces).
Missing real inputs produce `PROFILE_PACKAGE_PRECONDITION`, never a security
Red result. Then run the actual package probe and Architecture owner:

```bash
dotnet run .ci/scripts/validate-cli-package.cs -- --package-directory artifacts/configurator/cli/linux-x64 --expected-rid linux-x64
dotnet build tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release -- --treenode-filter "/*/*/*ProfilePackageTests/*"
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release
```

`ProfilePackageTests` has five cases: three native closure cases, one Browser
worker/notice/custody-contract case and one actual CLI synthetic probe case.
The probe's `encrypted-profile-policy` cohort seals and unlocks a synthetic
profile through the packaged CLI's real facade, checks the independent JWE
format/cipher/KDF specification and restricted file mode, and refuses alternate
cipher/KDF requests. Non-Linux probe runs require protected-save refusal instead.
Package metadata does not prove runtime network/cache behavior. Product Browser
AOT execution, responsive KDF/termination, network/storage observations,
native-provider recovery and release authority remain separate parent gates.

The original source-only task executed no commands or captures. Later
qualification is recorded separately; the commands above remain procedures,
and their presence does not establish a complete 33-guard or release pass.

## Scoped verification on 2026-11-14

The exact staged source built in Release with zero warnings and errors.
English and Arabic Xvfb executions each passed the explicit three-guard
`profiles-only` cohort. No screenshots were captured by these runs.
All five `ProfilePackageTests` cases passed against the available real published
CLI, Terminal, Desktop and Browser inputs. Those inputs were not freshly
republished by this check. The existing full-run D27 failure and separate
capture/provenance, actual-host and assistive-technology gates remain open.
