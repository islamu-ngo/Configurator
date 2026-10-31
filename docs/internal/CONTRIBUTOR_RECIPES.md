# Contributor recipes

> **Audience:** Contributors | AI agents
> **Kind:** Guide
> **Status:** Current source-grounded recipes; example edits and commands unexecuted
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `PROJECTS.md`, `docs/internal/TESTING.md`, `eng/configurator/ValidateDefinitionDocumentation.cs`, `tests/Configurator.Core.Tests/Definitions/DefinitionSemanticTests.cs`, `tests/Configurator.Cli.Tests/SetupCliContractTests.cs`, `tests/Configurator.Browser.Tests/BrowserV2EditingTests.cs`, `tests/Configurator.Artifacts.Tests/ProtectedArtifactBundleTests.cs`, `tests/Configurator.Desktop.Tests/DesktopPublicationLifetimeTests.cs`
> **Review basis:** Named source, test bodies and project files inspected on 2026-10-30; no recipe execution, builds, tests or host exercises

Choose the behavior you want to change, then follow its owner, example edit and
proportional check. These recipes show small, coherent contributions using
synthetic data. They are not requests to change working product behavior.
[Developer guide](DEVELOPER_GUIDE.md) explains the architecture;
[First contribution](FIRST_CONTRIBUTION.md) walks through a documentation change;
[Style guide](DOCUMENTATION_STYLE_GUIDE.md) explains how to write the result.

## Before using a recipe

All commands below are **runnable current command forms, source-inspected but
not executed for these guides**. Run them only as part of an authorized
contribution, from the repository root in a POSIX shell. Use SDK 10.0.302 from
[`global.json`](../../global.json), restored project dependencies and an existing
writable `"$HOME/.cache/agent-tmp"`. Product test projects are executable TUnit/MTP
owners. Their selectors below name inspected existing classes or methods, not
invented test names.

The build commands produce local Release outputs; test runs may create results
and temporary fixtures. Do not run while another owner is editing the same
slice. A repeat uses the current source and does not repair a failed invariant
by itself. Native filesystem cases additionally need the actual supported host.
Generated-output commands explicitly write their owning generated file; do not
run them for a test-only or prose-only change.

For domain, concurrency or privacy fixes, establish the runtime-failing invariant
before implementation. The example test additions here strengthen specified
behavior; they do not claim a known current defect or a demonstrated Red run.
If a test already covers the proposed case, do not duplicate it. Use
[Testing](TESTING.md#the-3-ring-progressive-verification-model) for wider phase
and workstream gates after the focused check, and [PROJECTS.md](../../PROJECTS.md)
for authoritative project scope.

| Task | Recipe |
| --- | --- |
| Explain an existing mechanism accurately | [Documentation](#documentation) |
| Guard a bounded definition assertion | [Core assertion](#core-assertion) |
| Guard the machine envelope | [CLI contract](#cli-contract) |
| Reject an obsolete editor callback | [Interactive editor](#interactive-editor) |
| Preserve installation facts through interruption | [Native cancellation and publication](#native-cancellation-and-publication) |

## Documentation

**Task and owner.** Clarify that a stale native save may leave an installation
receipt without restoring current readiness. Contributor Experience owns the
explanatory developer guide; Presentation and Artifacts/Security own the
underlying contracts. Start with
[Presentation's stale-save explanation](PRESENTATION_STATE.md#why-a-stale-save-can-still-have-an-installation-receipt)
and [protected artifacts](PROTECTED_ARTIFACTS.md), not a historical success count.

**Inspected source/test owners.**
[`ArtifactPublisherViewModel.SaveAsync`](../../src/Configurator.Desktop/ViewModels/ArtifactPublisherViewModel.cs)
captures bytes and clears preparation before awaiting native save. Its current
generation gates status publication.
[`DesktopPublicationOperation`](../../src/Configurator.Desktop/ViewModels/DesktopPublicationOperation.cs)
owns borrowed storage and settles the copied receipt before releasing its
operation. The existing
[`DesktopPublicationLifetimeTests.BorrowedSaveBytesSurviveInvalidationUntilConsumerSettles`](../../tests/Configurator.Desktop.Tests/DesktopPublicationLifetimeTests.cs)
covers cancel/edit/dispose orderings. Its test body retains bytes while held,
then asserts clearing and a settled receipt. Reading it is not a test pass.

**Smallest coherent edit.** In [Developer guide](DEVELOPER_GUIDE.md), improve the
native-save explanation only if it lacks this distinction. This is a
**conceptual replacement paragraph**, not a new contract:

> Editing the form retires the earlier save's eligibility to restore current
> output. It does not undo an installed file. Keep the established receipt
> separate from readiness, and retain borrowed bytes until the native consumer
> settles.

Keep the [policy link](DOCUMENTATION_ARCHITECTURE.md) and technical source route.
If changing operator instructions, update
[native output and recovery](../public/native-output.md) and its technical twin
under the parity protocol; a clarification in this contributor guide alone does
not change operator behavior.

**Focused command.** The prose owner is checked by
[`DocumentationCheck.Run`](../../eng/configurator/ValidateDefinitionDocumentation.cs),
whose arguments are `ROOT FILE...`:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --file eng/configurator/ValidateDefinitionDocumentation.cs -- \
  . docs/internal/DEVELOPER_GUIDE.md
```

No product test selector applies to a prose-only edit. The named Desktop test
above is the source evidence owner, not a suite to execute for wording.

**Expected evidence and limits.** Retain exit 0 and the actual
`DOC_CHECK files=1 ... errors=0` line, plus a manual check that the paragraph
agrees with the two contracts. The checker covers explicit local links,
supported anchors, fences and metadata. It does not prove save behavior or
request external links. Source inspection on 2026-10-30 established the cited
mechanisms and test assertions; neither the command nor the Desktop test ran
for this guide. Use [checker limitations](../../eng/knowledge/README.md#bounded-link-checking-and-honest-failures)
when interpreting a result.

## Core assertion

**Task and owner.** Guard the rule that string length counts Unicode scalar
values, not UTF-16 code units. Core owns validation; do not correct this in a
Browser or Desktop control. Start with the
[Core contract](CORE_DEFINITION_CONTRACT.md) and
[bounded conformance profile](DEFINITION_CONFORMANCE_PROFILE.md).

**Inspected source/test owners.**
[`DefinitionValidator.LocalValueValidAsync`](../../src/Configurator.Core/Definitions/DefinitionValidator.cs)
uses `EnumerateRunes().Count()` for `minLength`/`maxLength`.
[`DefinitionSemanticTests`](../../tests/Configurator.Core.Tests/Definitions/DefinitionSemanticTests.cs)
exercises validation/composition with real Core objects.
Its [`DefinitionFixture`](../../tests/Configurator.Core.Tests/Definitions/DefinitionAdmissionTests.cs)
supplies a required public `service.name` string, minimum length 1, and maximum
length 80. The
[`Core test project`](../../tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj)
references Core directly.

**Smallest coherent edit.** Add this **partial source example** as a method in
`DefinitionSemanticTests`, using that file's existing JSON and Core imports.
It changes only the in-memory fixture's maximum to 1; the JSON answer strings
below are synthetic. The existing fixture retains its identity and output
projection.

```csharp
[Test]
[Arguments("""{"service.name":"a"}""", true)]
[Arguments("""{"service.name":"\uD83D\uDE42"}""", true)]
[Arguments("""{"service.name":""}""", false)]
[Arguments("""{"service.name":"ab"}""", false)]
public async Task StringBoundsCountUnicodeScalars(string answers, bool ready)
{
    JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
    source["configuration"]!["properties"]!["service.name"]!["maxLength"] = 1;
    SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
    DefinitionValidationResult result = DefinitionValidator.Validate(
        definition, DefinitionFixture.Answers(answers));
    await Assert.That(result.IsReady).IsEqualTo(ready);
}
```

The literal expected values derive from the bounded rule: the escaped surrogate
pair represents one scalar; `ab` represents two. Do not calculate the expected
answer by invoking the production length algorithm. If a real defect is exposed,
fix the shared validator and any affected admission/composition callers, then
update the public explanation and technical anchor together.

**Focused command.** Select the inspected existing class, which also includes
the proposed method after insertion:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/DefinitionSemanticTests/*" --minimum-expected-tests 1
```

**Expected evidence and limits.** Retain the build exit, test exit and actual
passed/failed/skipped counts. Confirm the four proposed cases were discovered;
the minimum guard prevents an entirely empty selection but does not prove all
four arguments ran. A regression Red run must fail for the assertion, not a
compile error. This is in-memory Core evidence, not client rendering, full JSON
Schema conformance or target acceptance. Source and fixture inspection were
completed on 2026-10-30; this excerpt was not compiled and no selector was run.

## CLI contract

**Task and owner.** Strengthen rejection of malformed machine-envelope version
identifiers without adding aliases. CLI owns serialization and its closed v4
machine contract. Start with [CLI contract](CLI_CONTRACT.md), not human-help
formatting or a private answer file.

**Inspected source/test owners.**
[`SetupCliMachineOutput.Serialize`](../../src/Configurator.Cli/SetupCliMachineOutput.cs)
constructs a v4 envelope and newline-framed bytes.
[`SetupCliCommandSchemaMetadata`](../../src/Configurator.Cli/SetupCliCommandSchemaMetadata.cs)
generates the schema.
[`SetupCliContractTests.MachineVersionFourRejectsRetiredAndUnknownEnvelopesWithoutCompatibility`](../../tests/Configurator.Cli.Tests/SetupCliContractTests.cs)
mutates a synthetic envelope and checks `machine-version`.
[`SetupCliMachineContractVerifier`](../../tests/Configurator.Cli.Tests/SetupCliMachineContractVerifier.cs)
and [`SetupCliContractSpecification`](../../tests/Configurator.Cli.Tests/SetupCliContractSpecification.cs)
provide independent closed-field and vocabulary expectations.
[`SetupCliMachineBoundaryTests`](../../tests/Configurator.Cli.Tests/SetupCliMachineBoundaryTests.cs)
also verifies actual CLI output through `CliFixture`, rather than only generated
schema metadata.

**Smallest coherent edit.** Extend the version array in that existing contract
test with whitespace/case variants. This **partial source example** replaces
only its version loop; retain the rest of the test, including valid
authoring/runtime handshake cases:

```csharp
foreach (string version in new[] {
    "islamu-configurator-command/v1", "islamu-configurator-command/v2",
    "islamu-configurator-command/v3", "islamu-configurator-command/v5",
    "2", "", "islamu-configurator-command/v4 ",
    "ISLAMU-CONFIGURATOR-COMMAND/v4"
})
    await Assert.That(SetupCliMachineContractVerifier.Validate(
        SetupCliMachineContractVerifier.Mutate(
            root => root["schemaVersion"] = version)))
        .Contains("machine-version");
```

This is a test-only contract clarification; do not change the v4 emitter or
generated schema to admit the negative cases. For a separately approved shape
change, coordinate emitter/model, schema metadata, independent specification,
actual CLI boundary assertions, public CLI guide and technical contract.
Never update expected fields by deriving them from the emitter under test.

**Focused commands.** These existing classes cover the independent schema
contract and actual machine-output boundary:

The contract class checks malformed versions with independent verifier fixtures.
The separate boundary selector below checks actual CLI continuation admission
and its emitted receipt, not malformed-version rejection. Keep those evidence
claims separate; the test-only version-array edit does not change the emitter.

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/SetupCliContractTests/*" --minimum-expected-tests 1
dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/SetupCliMachineBoundaryTests/FullInventoryOffsetsReachContinuationValidationRatherThanOldGrammarLimit" \
  --minimum-expected-tests 1
```

For a real schema-source change only, the inspected
[`generator entrypoint`](../../eng/configurator/Configurator.CommandSchemaGenerator/Program.cs)
owns regeneration and freshness:

```bash
dotnet run --project eng/configurator/Configurator.CommandSchemaGenerator/Configurator.CommandSchemaGenerator.csproj -- --write
dotnet run --project eng/configurator/Configurator.CommandSchemaGenerator/Configurator.CommandSchemaGenerator.csproj -- --check
dotnet run --file eng/knowledge/RenderCommandReference.cs -- --write
dotnet run --file eng/knowledge/RenderCommandReference.cs -- --check
```

`--write` replaces generated schema bytes and removes retired v1-v3 schema paths;
it is not part of the test-only example. `--check` is read-only with respect to
the schema and expects `configurator-command-schema-current`, exit 0.
The renderer projects that checked schema to the
[generated command reference](../public/cli-command-reference.md); it does not
generate semantic help. Follow [schema ownership](../../schemas/README.md) and
the [reference commands](../../eng/knowledge/README.md#generated-command-reference)
for projection self-tests and freshness.

**Expected evidence and limits.** Record each command's exit and test counts,
including the three offset cases selected by the named boundary method. Keep
the new malformed-version expectations and retained valid cases passing. A
generated-schema freshness pass alone does not prove actual CLI output.
The focused offset method is not publication or whole-CLI qualification.
All named owners/selectors were source-inspected on 2026-10-30; no command or
example edit was executed, and no new version acceptance is proposed.

## Interactive editor

**Task and owner.** Prove that a callback captured before an editor is unmounted
cannot edit a remounted field, while a fresh callback advances one revision.
Browser owns the callback's mounting lifetime; Presentation owns the answer
mutation and Core owns validity. Start with
[Browser ownership](BROWSER_UI.md) and [Presentation state](PRESENTATION_STATE.md).

**Inspected source/test owners.**
[`ConfigurationField.razor`](../../src/Configurator.Browser/Components/Configuration/ConfigurationField.razor)
captures form, path, input revision and action generation for callbacks.
[`ConfigurationField.razor.cs`](../../src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.cs)
checks those facts in `IsCurrentAction` before `ChooseVariant` mutates state.
[`DefinitionFormModel`](../../src/Configurator.Presentation/Presentation/DefinitionFormModel.cs)
owns the shared form.
[`BrowserV2EditingTests.RetiredVariantCallbackCannotEditAfterViewOnlyUnmountAndRemount`](../../tests/Configurator.Browser.Tests/BrowserV2EditingTests.cs)
already holds a stale callback across collapse/expand. Its local `Form()` fixture
declares synthetic `first` and `second` variants.

**Smallest coherent edit.** Add revision assertions to that existing method.
This **partial source example** replaces its final stale/fresh invocation block,
after collapse/expand and the final `field.Render()`. All variables and the
admitted fixture come from that method:

```csharp
long revision = form.InputRevision;
await field.InvokeAsync(() => stale.InvokeAsync("1"));
await Assert.That(form.Snapshot().Values.ContainsKey("provider")).IsFalse();
await Assert.That(form.InputRevision).IsEqualTo(revision);
await field.InvokeAsync(() =>
    field.FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync("1"));
await Assert.That(form.Snapshot().Values["provider"].GetProperty("kind").GetString())
    .IsEqualTo("second");
await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
```

Keep the fresh component lookup after rendering; do not keep a stale component
instance as the positive control. The ordinal `"1"` selects the second declared
variant, not a private answer interpreted as a label. If a failing regression
requires a fix, preserve the mounting/revision fence rather than adding a
second answer store or moving validation into the control.

**Focused commands.**

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/BrowserV2EditingTests/*" --minimum-expected-tests 1
```

The inspected
[`Browser test project`](../../tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj)
references Browser and bUnit. Browser Release publication has additional
workload requirements; see [PROJECTS.md](../../PROJECTS.md) rather than treating
component tests as a published package check.

**Expected evidence and limits.** Record passing counts and demonstrate that
the stale action changes neither answers nor revision, while the fresh action
changes both as specified. No sleep or timing window is needed. A real UI fix
also needs the affected rendered keyboard/focus behavior on the actual surface,
with desktop/mobile checks as applicable. This bUnit example does not measure
latency, accessibility conformance, browser caches or physical devices.
Source/test inspection is dated 2026-10-30; the proposed assertions and commands
were not executed.

## Native cancellation and publication

**Task and owner.** Preserve the installed prefix when cancellation happens
after installation but before verification. Artifacts/Security owns those
physical facts; native adapters must retain them without reviving current
prepared output. Read [protected artifacts](PROTECTED_ARTIFACTS.md) and
[Presentation's stale-save explanation](PRESENTATION_STATE.md#why-a-stale-save-can-still-have-an-installation-receipt).

**Inspected source/test owners.**
[`ProtectedArtifactBundlePublisher.PublishAsync`](../../src/Configurator.Artifacts/ProtectedArtifactBundlePublisher.cs)
prepares every output before committing in order. It retains an established
result after exceptions, observes each outcome and clears cloned payloads in
`finally`.
[`ProtectedArtifactBundleTests.PostPublicationExceptionOrCancellationPreservesTheObservedPrefix`](../../tests/Configurator.Artifacts.Tests/ProtectedArtifactBundleTests.cs)
uses a real Linux writer with a deterministic post-installation fault seam.
Its `cancel` arguments cover exception and cancellation without a timing race.
[`ArtifactPublisherViewModel`](../../src/Configurator.Desktop/ViewModels/ArtifactPublisherViewModel.cs)
and [`DesktopPublicationOperation`](../../src/Configurator.Desktop/ViewModels/DesktopPublicationOperation.cs)
keep receipt/storage lifetime separate from current workspace eligibility.
[`DesktopPublicationLifetimeTests.BorrowedSaveBytesSurviveInvalidationUntilConsumerSettles`](../../tests/Configurator.Desktop.Tests/DesktopPublicationLifetimeTests.cs)
uses entered/release signals to hold the consumer through cancel/edit/dispose.

**Smallest coherent edit.** Strengthen the existing Artifacts test to assert
that its observed installed-but-unverified prefix is itself unsuccessful.
This **partial source example** belongs immediately after
`await Assert.That(observed.Single().Result!.VerificationSucceeded).IsFalse();`
in the named test:

```csharp
await Assert.That(observed.Single().Result!.Succeeded).IsFalse();
```

Keep its existing assertions: first file exists, second file does not,
installation is established, verification failed, the result matches the
observed prefix, cancellation has its own completion code, and bundle success
is false. This addition uses the existing synthetic artifact fixture and
`synthetic-inspection-fault` path; it introduces no destination or credential.
If the behavior fails, fix the writer/publisher fact retention, then the native
consumer if necessary. Do not translate cancellation into "nothing written."

**Focused commands.** The filesystem test explicitly requires Linux and a
filesystem supporting the protected writer. The Desktop selection is a
controlled headless lifetime test, not an actual X11 exercise:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build tests/Configurator.Artifacts.Tests/Configurator.Artifacts.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Artifacts.Tests/Configurator.Artifacts.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/ProtectedArtifactBundleTests/PostPublicationExceptionOrCancellationPreservesTheObservedPrefix" \
  --minimum-expected-tests 1
dotnet build tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/DesktopPublicationLifetimeTests/BorrowedSaveBytesSurviveInvalidationUntilConsumerSettles" \
  --minimum-expected-tests 1
```

**Expected evidence and limits.** Record host/filesystem, exits and actual
counts: two Artifacts argument cases and three Desktop invalidation cases.
The first demonstrates established installation without successful verification;
the second demonstrates borrowed-byte lifetime, retirement of Save and settled
receipt retention. Neither proves whole-bundle rollback, other operating
systems, forced-termination cleanup or target acceptance.

Follow [native output recovery](../public/native-output.md) after real partial
publication: preserve the known prefix and unknown facts, inspect through
authorized local tools and use a fresh private destination rather than
overwriting a conflict. Test-owned temporary directories are cleaned by their
fixtures; that is not a production rollback promise. Source/test inspection on
2026-10-30 established these assertions, not an actual-host pass. No publication,
test, build or recovery command ran while writing this recipe.

## Record the result, not just the command

For any recipe, hand off the changed behavior, exact owner paths, synthetic
scenario, command exits and discovered/passed/failed/skipped counts. State
whether evidence is source-inspected, executed in-process, rendered or
actual-host. Include anything unrun and why. A proposed excerpt is not a shipped
fix, and a passing narrow selection is not a claim that every suite passed.

Use [AI review](../../AI_REVIEW.md) and the
[docs-impact contract](DOCUMENTATION_ARCHITECTURE.md#docs-impact-contract).
Keep unrelated shared edits and historical records intact. Submission and
publication follow [Contributing](../../CONTRIBUTING.md); these recipes grant
no Git, external-write or production authority.
