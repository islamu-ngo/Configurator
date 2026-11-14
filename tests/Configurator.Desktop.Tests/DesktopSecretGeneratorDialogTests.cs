namespace Configurator.Desktop.Tests;

using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;
using Configurator.Presentation;

public sealed class DesktopSecretGeneratorDialogTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    public async Task SecretGeneratorDialogViewModelEnforcesPasswordPolicyAndOnlyWritesSnapshotOnApply()
    {
        using var form = new DefinitionFormModel(SecretOptionsDefinition(), DefinitionSessionPrivacy.NativeComplete);
        DefinitionPath passwordPath = DefinitionPath.FromSegments(["password"]);
        using DefinitionSecretGeneratorSession? session = form.CreateSecretGeneratorSession(passwordPath);
        await Assert.That(session).IsNotNull();

        var clipboard = new RecordingClipboard();
        using var viewModel = new SecretGeneratorDialogViewModel(form, session!, new DesktopStrings("en"), clipboard);

        await Assert.That(viewModel.IsPasswordBased).IsTrue();
        await Assert.That(viewModel.IsByteTokenBased).IsFalse();
        await Assert.That(viewModel.CanGenerate).IsTrue();
        await Assert.That(viewModel.Bounds.MinLength).IsEqualTo(30);
        await Assert.That(viewModel.Bounds.MaxLength).IsEqualTo(64);
        await Assert.That(viewModel.Session.EstimatedEntropyBits >= 128).IsTrue();

        // Dropping length below the 128-bit floor must block generation without throwing.
        viewModel.LengthText = "20";
        await Assert.That(viewModel.Session.MeetsEntropyFloor).IsFalse();
        await Assert.That(viewModel.CanGenerate).IsFalse();
        await Assert.That(string.IsNullOrWhiteSpace(viewModel.ErrorText)).IsFalse();

        // Restoring valid bounds + adding an excluded character applies to generated preview cleanly.
        viewModel.LengthText = "36";
        viewModel.AdditionalForbiddenCharacters = "@#";
        await Assert.That(viewModel.CanGenerate).IsTrue();
        await Assert.That(viewModel.ErrorText).IsNull();

        DefinitionSecretOperationResult previewResult = await viewModel.GeneratePreviewAsync();
        await Assert.That(previewResult.Succeeded).IsTrue();
        await Assert.That(viewModel.HasPreview).IsTrue();
        await Assert.That(viewModel.CanApply).IsTrue();
        await Assert.That(viewModel.PreviewText.Length).IsEqualTo(36);
        await Assert.That(viewModel.PreviewText.Contains('@')).IsFalse();
        await Assert.That(viewModel.PreviewText.Contains('#')).IsFalse();

        // Preview must not leak into form answers or diagnostics prior to explicit Apply.
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);

        await viewModel.CopyPreviewCommand.ExecuteAsync(null);
        await Assert.That(clipboard.LastSecret).IsEqualTo(viewModel.PreviewText);
        await Assert.That(string.IsNullOrWhiteSpace(viewModel.ClipboardNotice)).IsFalse();
        await Assert.That(viewModel.ClipboardNotice!.Contains(viewModel.PreviewText, StringComparison.Ordinal)).IsFalse();

        string generatedPreview = viewModel.PreviewText;
        bool? requestedCloseResult = null;
        viewModel.RequestClose += (_, closedResult) => requestedCloseResult = closedResult;

        DefinitionSecretOperationResult applyResult = await viewModel.ApplyAsync();
        await Assert.That(applyResult.Succeeded).IsTrue();
        await Assert.That(requestedCloseResult).IsTrue();
        await Assert.That(form.Snapshot().Values["password"].GetString()).IsEqualTo(generatedPreview);
        await Assert.That(form.AnswerOrigin(["password"])).IsEqualTo(DefinitionAnswerOrigin.Generated);
        await Assert.That(viewModel.HasPreview).IsFalse();
        await Assert.That(viewModel.PreviewText).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SecretGeneratorDialogViewModelSupportsVariableByteTokensAndClearsPreviewOnCancel()
    {
        using var form = new DefinitionFormModel(SecretOptionsDefinition(), DefinitionSessionPrivacy.NativeComplete);
        DefinitionPath tokenPath = DefinitionPath.FromSegments(["token"]);
        using DefinitionSecretGeneratorSession? session = form.CreateSecretGeneratorSession(tokenPath);
        await Assert.That(session).IsNotNull();

        using var viewModel = new SecretGeneratorDialogViewModel(form, session!, new DesktopStrings("en"));
        await Assert.That(viewModel.IsPasswordBased).IsFalse();
        await Assert.That(viewModel.IsByteTokenBased).IsTrue();
        await Assert.That(viewModel.ShowsByteLengthInput).IsTrue();

        viewModel.LengthText = "32";
        DefinitionSecretOperationResult previewResult = await viewModel.GeneratePreviewAsync();
        await Assert.That(previewResult.Succeeded).IsTrue();
        await Assert.That(viewModel.HasPreview).IsTrue();
        viewModel.ToggleRevealPreviewCommand.Execute(null);
        await Assert.That(viewModel.IsPreviewRevealed).IsTrue();

        bool? closedResult = null;
        viewModel.RequestClose += (_, result) => closedResult = result;
        viewModel.CancelCommand.Execute(null);

        await Assert.That(closedResult).IsFalse();
        await Assert.That(viewModel.HasPreview).IsFalse();
        await Assert.That(viewModel.PreviewText).IsEqualTo(string.Empty);
        await Assert.That(viewModel.IsPreviewRevealed).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
    }

    [Test]
    public async Task SecretGeneratorDialogViewModelCancelsAndClearsWhenWorkspaceBecomesStale()
    {
        using var form = new DefinitionFormModel(SecretOptionsDefinition(), DefinitionSessionPrivacy.NativeComplete);
        DefinitionPath passwordPath = DefinitionPath.FromSegments(["password"]);
        using DefinitionSecretGeneratorSession session = form.CreateSecretGeneratorSession(passwordPath)!;

        using var viewModel = new SecretGeneratorDialogViewModel(form, session, new DesktopStrings("en"));
        await Assert.That((await viewModel.GeneratePreviewAsync()).Succeeded).IsTrue();
        await Assert.That(viewModel.HasPreview).IsTrue();

        bool? closedResult = null;
        viewModel.RequestClose += (_, result) => closedResult = result;

        // An unrelated edit invalidates the generation session revision.
        form.EditValue(["note"], "concurrent-edit");

        await Assert.That(closedResult).IsFalse();
        await Assert.That(viewModel.HasPreview).IsFalse();
        await Assert.That(viewModel.CanApply).IsFalse();
        await Assert.That(viewModel.PreviewText).IsEqualTo(string.Empty);
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
    }

    [Test]
    public async Task StringFieldViewModelOpensInjectedSecretGeneratorDialogService()
    {
        var dialogService = new RecordingSecretGeneratorDialogService();
        using var shell = DesktopTestContext.Root(secretGeneratorDialog: dialogService);
        shell.Loader.LoadDefinition(SecretOptionsDefinitionBytes());
        await Assert.That(shell.Workspace.Form).IsNotNull();

        StringFieldViewModel passwordField = shell.Workspace.Fields
            .OfType<StringFieldViewModel>()
            .Single(field => field.Id == "password");

        await Assert.That(passwordField.HasSecretGeneration).IsTrue();
        await passwordField.OpenGeneratorCommand.ExecuteAsync(null);

        await Assert.That(dialogService.OpenedPaths.Count).IsEqualTo(1);
        await Assert.That(dialogService.OpenedPaths[0].ToPointer()).IsEqualTo("/password");
    }

    [Test]
    [NotInParallel]
    public async Task RealSecretGeneratorDialogGeneratesAppliesAndCancelsOnEscape()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopSecretGeneratorDialogTests));
        try
        {
            var observed = await session.Dispatch(async () =>
            {
                using var form = new DefinitionFormModel(SecretOptionsDefinition(), DefinitionSessionPrivacy.NativeComplete);
                DefinitionPath passwordPath = DefinitionPath.FromSegments(["password"]);
                using DefinitionSecretGeneratorSession generatorSession = form.CreateSecretGeneratorSession(passwordPath)!;

                using var viewModel = new SecretGeneratorDialogViewModel(form, generatorSession, new DesktopStrings("en"));
                var dialog = new SecretGeneratorDialog { DataContext = viewModel };
                var owner = new Window { Width = 900, Height = 700 };
                owner.Show();
                Dispatcher.UIThread.RunJobs();

                Task<bool> dialogTask = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();

                await viewModel.GeneratePreviewAsync();
                Dispatcher.UIThread.RunJobs();
                bool previewGeneratedBeforeCommit = viewModel.HasPreview && !form.Snapshot().Values.ContainsKey("password");

                // Escape closes and clears generated candidate without committing to form.
                dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "\x1b");
                Dispatcher.UIThread.RunJobs();
                bool escapeResult = await dialogTask.WaitAsync(TimeSpan.FromSeconds(5));
                bool snapshotCleanAfterEscape = !form.Snapshot().Values.ContainsKey("password") && !viewModel.HasPreview;

                owner.Close();
                Dispatcher.UIThread.RunJobs();
                return (previewGeneratedBeforeCommit, escapeResult, snapshotCleanAfterEscape);
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15));

            await Assert.That(observed.previewGeneratedBeforeCommit).IsTrue();
            await Assert.That(observed.escapeResult).IsFalse();
            await Assert.That(observed.snapshotCleanAfterEscape).IsTrue();
        }
        finally
        {
            await Task.Run(session.Dispose);
        }
    }

    private sealed class RecordingClipboard : ISecretClipboardService
    {
        public string? LastSecret { get; private set; }

        public Task<bool> CopyAsync(string value)
        {
            LastSecret = value;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingSecretGeneratorDialogService : ISecretGeneratorDialogService
    {
        public List<DefinitionPath> OpenedPaths { get; } = [];

        public Task<DefinitionSecretOperationResult?> OpenAsync(
            DefinitionFormModel form,
            DefinitionPath path,
            DesktopStrings strings,
            ISecretClipboardService? clipboard = null,
            CancellationToken cancellationToken = default)
        {
            OpenedPaths.Add(path);
            return Task.FromResult<DefinitionSecretOperationResult?>(null);
        }
    }

    private static byte[] SecretOptionsDefinitionBytes() => Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": "0.0.1",
      "profile": "bounded-v0.0.1",
      "familyId": "7d1a4c9f-4e11-45fa-92c0-18f71a6f7e33",
      "revision": 1,
      "project": {"id": "example", "name": "Example", "publisher": "example"},
      "applicationVersions": ["1"],
      "entryPoints": ["setup"],
      "locales": ["en"],
      "localization": {"defaultLocale": "en", "translations": {}},
      "requiredCapabilities": ["projection.copy", "projection.object", "schema.bounds", "schema.object", "schema.secret", "schema.string"],
      "presentation": {
        "defaultJourney": "setup",
        "sections": [{"id": "general", "title": "General", "pointers": ["/note", "/password", "/token"]}],
        "journeys": [{"id": "setup", "title": "Setup", "sections": ["general"]}]
      },
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "x-configurator": {"sensitivity": "public"},
        "properties": {
          "note": {
            "type": "string",
            "title": "Note",
            "x-configurator": {"sensitivity": "public", "detail": "basic"}
          },
          "password": {
            "type": "string",
            "title": "Password",
            "minLength": 12,
            "maxLength": 64,
            "x-configurator": {
              "sensitivity": "secret",
              "detail": "basic",
              "secret": {
                "kind": "password",
                "acquisition": "local",
                "constraints": {
                  "characterSet": "ascii-printable",
                  "allowWhitespace": false,
                  "minLowercase": 2,
                  "minUppercase": 2,
                  "minDigits": 2,
                  "minSymbols": 2,
                  "allowedSymbols": "!@#$",
                  "forbiddenCharacters": "0O"
                },
                "generation": {"length": 32}
              }
            }
          },
          "token": {
            "type": "string",
            "title": "Token",
            "x-configurator": {
              "sensitivity": "secret",
              "detail": "basic",
              "secret": {
                "kind": "opaque-token",
                "acquisition": "local",
                "encoding": "hex",
                "constraints": {"minBytes": 16, "maxBytes": 64},
                "generation": {"bytes": 32}
              }
            }
          }
        }
      },
      "artifacts": [{
        "id": "config",
        "format": "json",
        "fileName": "config.json",
        "targetUpdateMode": "create",
        "restartRequirement": "process",
        "projection": {"op": "object", "members": {"note": {"op": "copy", "reference": {"scope": "root", "path": "/note"}}}}
      }]
    }
    """);

    private static Definition SecretOptionsDefinition() => DefinitionCodec.ParseJson(SecretOptionsDefinitionBytes());
}
