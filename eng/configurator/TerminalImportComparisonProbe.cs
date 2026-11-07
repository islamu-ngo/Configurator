namespace Configurator.Terminal;

using Configurator.Presentation;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Engineering;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using global::Terminal.Gui.App;

internal sealed partial class TerminalWindow
{
    internal IApplication ProbeApplication => _application;
    internal DefinitionFormModel ProbeForm => _operation.Form;

    internal void SelectProbeArtifact(string artifact) => _operation.ArtifactId = artifact;

    internal Task StartProbeImport(string path)
    {
        _importPath.Text = path;
        StartImport(true);
        return _importOperation.Completion;
    }

    internal void NextProbeFinding() => NextComparisonFinding();
    internal void ClearProbeState() => ClearPrivateState();
    internal void CancelProbeImport() => CancelImport();

    internal Task StartProbePreparation()
    {
        Prepare();
        return _preparationCompletion;
    }

    internal Task StartProbeSave()
    {
        Save();
        return _workspace.WaitForPendingOperationsAsync();
    }

    internal void CancelProbePreparation()
    {
        Configurator.Engineering.MeasureNativePreparation.CancelRequestedToken();
        _operation.Invalidate();
    }

    internal Task CancelProbeSave() => _workspace.RequestCancellationAsync();
    internal bool ProbePrepared => _operation.IsPrepared;
    internal ProtectedArtifactPublicationReceipt? ProbeReceipt => _operation.LatestPublication;
    internal string? ProbeOutputDirectory { set => _operation.ProbeDirectory = value; }

    internal void SelectProbePartialOutputs()
    {
        foreach (string id in TerminalPartialSaveProbe.ArtifactIds)
        {
            _operation.ArtifactId = id;
            if (!_operation.CurrentArtifactSelected) _operation.ToggleCurrentArtifact();
        }
        if (!_operation.SelectedArtifactIds.SequenceEqual(TerminalPartialSaveProbe.ArtifactIds))
            throw new InvalidOperationException("partial-save-selection-refused");
    }

    internal bool ProbePartialResultCurrent => _receipt is { IsSettled: true }
        && _receipt.AttemptId == _operation.LatestPublication?.AttemptId;

    internal void FailProbePartialSave()
    {
        ExitCode = 70;
        RequestStopFromSignal();
    }

    internal void ClearProbeHistory(string mode)
    {
        if (mode is "importPath" or "all") _importPath.ClearHistoryChanges();
        if (mode is "value" or "all") _value.ClearHistoryChanges();
        if (mode is "status" or "all") _status.ClearHistoryChanges();
    }
}

/// <summary>Fixed synthetic create-only conflict journey inside the actual diagnostic Terminal process.</summary>
internal sealed class TerminalPartialSaveProbe
{
    private const UnixFileMode PrivateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    internal static IReadOnlyList<string> ArtifactIds { get; } = Array.AsReadOnly<string>(["first", "second"]);
    private static readonly string[] FileNames = ["phase4-first.json", "phase4-second.json"];
    private static readonly byte[] ConcurrentBytes = Encoding.UTF8.GetBytes("phase4-fixed-concurrent-target\n");
    private readonly string _directory;
    private readonly string _report;
    private readonly ArtifactDigest _digest;
    private int _outcomes;
    private bool _conflictCreated;
    private bool _firstVerifiedBeforeConflict;

    private TerminalPartialSaveProbe(string directory, string report, ArtifactDigest digest)
    {
        _directory = directory;
        _report = report;
        _digest = digest;
    }

    internal static TerminalPartialSaveProbe? Bind(Definition definition)
    {
        if (Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_MODE") != "partial-save") return null;
        if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("partial-save-host-refused");
        string directory = Directory.GetCurrentDirectory();
        string? output = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_OUTPUT");
        Definition expected = DefinitionCodec.ParseJson(NativePreparationFixture.CreatePartialSave());
        if (definition.Digest != expected.Digest
            || !definition.Artifacts.Select(artifact => artifact.Id).SequenceEqual(ArtifactIds)
            || !definition.Artifacts.Select(artifact => artifact.FileName).SequenceEqual(FileNames)
            || Directory.EnumerateFileSystemEntries(directory).Any()
            || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0
            || string.IsNullOrWhiteSpace(output) || !Path.IsPathFullyQualified(output))
            throw new InvalidOperationException("partial-save-binding-refused");
        string report = Path.GetFullPath(output);
        if (report.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || report == directory || File.Exists(report) || Directory.Exists(report))
            throw new InvalidOperationException("partial-save-report-refused");
        return new(directory, report, definition.Digest);
    }

    private void RequireBoundDirectory()
    {
        if (!string.Equals(Directory.GetCurrentDirectory(), _directory, StringComparison.Ordinal)
            || (File.GetAttributes(_directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("partial-save-directory-changed");
    }

    internal void ObserveOutcome(ProtectedArtifactBundleOutcome outcome)
    {
        if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("partial-save-host-refused");
        RequireBoundDirectory();
        _outcomes++;
        if (_outcomes == 1 && outcome.ArtifactId == "first" && outcome.Result is
            { Status: ProtectedArtifactStatus.Written, InstallationEstablished: true, VerificationSucceeded: true, CleanupFailed: false })
        {
            string first = Path.Combine(_directory, "phase4-first.json");
            _firstVerifiedBeforeConflict = File.ReadAllBytes(first).SequenceEqual(ExpectedOutput())
                && File.GetUnixFileMode(first) == PrivateMode;
            if (!_firstVerifiedBeforeConflict) throw new InvalidOperationException("partial-save-first-write-refused");
            using (var conflict = new FileStream(Path.Combine(_directory, "phase4-second.json"), new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, UnixCreateMode = PrivateMode
            }))
            {
                conflict.Write(ConcurrentBytes);
                conflict.Flush(true);
            }
            _conflictCreated = true;
        }
        else if (_outcomes != 2 || !_conflictCreated || outcome.ArtifactId != "second"
            || outcome.Result is not
                { Status: ProtectedArtifactStatus.TargetChanged, InstallationEstablished: false, VerificationSucceeded: false, CleanupFailed: false })
            throw new InvalidOperationException("partial-save-outcome-refused");
    }

    internal void Attach(TerminalWindow window) => _ = Task.Run(() => RunAsync(window));

    private async Task RunAsync(TerminalWindow window)
    {
        if (!OperatingSystem.IsLinux()) { Environment.Exit(70); return; }
        MeasureNativePreparation.TerminalPaintObservation? paint = null;
        string? error = null;
        bool preparedBefore = false, readyAfter = true, resultCurrent = false, resultCycle = false;
        bool firstComplete = false, concurrentUnchanged = false, modesPrivate = false, noExtraFiles = false;
        ProtectedArtifactPublicationReceipt? receipt = null;
        string? locale = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_UI_CULTURE");
        try
        {
            RequireBoundDirectory();
            Task preparation = Task.CompletedTask;
            Task preparationReadyCycle = Task.CompletedTask;
            await UiAsync(window, () =>
            {
                if (window.ProbeForm.Definition.Digest != _digest)
                    throw new InvalidOperationException("partial-save-definition-changed");
                if (locale is not ("en" or "ar") || !window.ProbeForm.SelectLocale(locale))
                    throw new InvalidOperationException("partial-save-locale-refused");
                System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(locale);
                window.SelectProbePartialOutputs();
                paint = new(window, 0);
                preparationReadyCycle = paint.ObserveCurrentResult(() => window.ProbePrepared);
                preparation = window.StartProbePreparation();
            }).ConfigureAwait(false);
            await preparation.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            await preparationReadyCycle.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            Task publication = Task.CompletedTask;
            Task currentResultCycle = Task.CompletedTask;
            await UiAsync(window, () =>
            {
                preparedBefore = window.ProbePrepared;
                if (!preparedBefore) throw new InvalidOperationException("partial-save-not-prepared");
                paint!.Dispose();
                paint = new(window, 1);
                currentResultCycle = paint.ObserveCurrentResult(() => window.ProbePartialResultCurrent);
                publication = window.StartProbeSave();
            }).ConfigureAwait(false);
            await publication.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            await currentResultCycle.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            Task cycle = Task.CompletedTask;
            await UiAsync(window, () =>
            {
                receipt = window.ProbeReceipt;
                readyAfter = window.ProbePrepared;
                resultCurrent = window.ProbePartialResultCurrent;
                if (receipt is not { IsSettled: true, IsUnknown: false, Failed: false, CleanupFailed: false }
                    || receipt.CompletionCode != ProtectedArtifactBundleCompletionCode.Completed
                    || receipt.Outcomes.Count != 2 || readyAfter || !resultCurrent
                    || receipt.Outcomes[0] is not { ArtifactId: "first", Result:
                        { Status: ProtectedArtifactStatus.Written, InstallationEstablished: true, VerificationSucceeded: true, CleanupFailed: false } }
                    || receipt.Outcomes[1] is not { ArtifactId: "second", Result:
                        { Status: ProtectedArtifactStatus.TargetChanged, InstallationEstablished: false, VerificationSucceeded: false, CleanupFailed: false } }
                    || window.ExitCode != 4)
                    throw new InvalidOperationException("partial-save-result-refused");
                cycle = paint!.ObserveLatest("partial-result-observed");
            }).ConfigureAwait(false);
            await cycle.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            await UiAsync(window, () =>
            {
                resultCycle = paint!.CurrentResultCycleObserved && window.ProbePartialResultCurrent;
                if (!resultCycle) throw new InvalidOperationException("partial-save-result-draw-missing");
            }).ConfigureAwait(false);
            RequireBoundDirectory();
            string first = Path.Combine(_directory, "phase4-first.json");
            string second = Path.Combine(_directory, "phase4-second.json");
            firstComplete = File.ReadAllBytes(first).SequenceEqual(ExpectedOutput());
            concurrentUnchanged = File.ReadAllBytes(second).SequenceEqual(ConcurrentBytes);
            modesPrivate = File.GetUnixFileMode(first) == PrivateMode && File.GetUnixFileMode(second) == PrivateMode;
            noExtraFiles = Directory.EnumerateFileSystemEntries(_directory).Select(Path.GetFileName)
                .Order(StringComparer.Ordinal).SequenceEqual(FileNames);
            if (!_firstVerifiedBeforeConflict || !_conflictCreated || _outcomes != 2
                || !firstComplete || !concurrentUnchanged || !modesPrivate || !noExtraFiles)
                throw new InvalidOperationException("partial-save-physical-facts-refused");
        }
        catch (Exception)
        {
            error = "partial-save-observation-failed";
        }
        finally { paint?.Dispose(); }
        try
        {
            byte[] report = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = "0.0.1", diagnosticCandidate = true,
                recipe = "terminal-partial-save-fixed-conflict-natural-result-v1",
                locale,
                definitionDigest = _digest.Value,
                fixtureSha256 = Convert.ToHexStringLower(SHA256.HashData(NativePreparationFixture.CreatePartialSave())),
                expectedOutputSha256 = Convert.ToHexStringLower(SHA256.HashData(ExpectedOutput())),
                candidateAssemblies = new[]
                {
                    typeof(TerminalWindow).Assembly, typeof(Definition).Assembly,
                    typeof(DefinitionFormModel).Assembly, typeof(ProtectedArtifactWriter).Assembly
                }.Select(assembly => new
                {
                    name = assembly.GetName().Name,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location)))
                }).ToArray(),
                completed = error is null, error, preparedBefore, readinessInvalidated = !readyAfter,
                resultCurrent, naturalCurrentResultCycle = resultCycle,
                firstVerifiedBeforeConflict = _firstVerifiedBeforeConflict, conflictCreated = _conflictCreated,
                firstComplete, concurrentUnchanged, modes0600 = modesPrivate, noExtraFiles,
                outcomes = receipt?.Outcomes.Select(outcome => new
                {
                    artifact = outcome.ArtifactId, status = outcome.Result?.Status.ToString(),
                    installed = outcome.Result?.InstallationEstablished, verified = outcome.Result?.VerificationSucceeded,
                    cleanupFailed = outcome.Result?.CleanupFailed
                }).ToArray(),
                expectedProcessExit = error is null ? 4 : (int?)null,
                closePending = true
            });
            if (report.Length > 16384) throw new InvalidOperationException("partial-save-report-size-refused");
            using var stream = new FileStream(_report, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, UnixCreateMode = PrivateMode
            });
            stream.Write(report);
            stream.Flush(true);
            if (File.GetUnixFileMode(_report) != PrivateMode)
                throw new InvalidOperationException("partial-save-report-mode-refused");
        }
        catch (Exception) { error = "partial-save-report-failed"; }
        try
        {
            await UiAsync(window, () =>
            {
                if (error is null) window.RequestStopFromSignal();
                else window.FailProbePartialSave();
            }).ConfigureAwait(false);
        }
        catch (Exception) { Environment.Exit(70); }
    }

    private static Task UiAsync(TerminalWindow window, Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.ProbeApplication.AddTimeout(TimeSpan.Zero, () =>
        {
            try { action(); done.TrySetResult(); }
            catch (Exception) { done.TrySetException(new InvalidOperationException("partial-save-owner-action-failed")); }
            return false;
        });
        return done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static byte[] ExpectedOutput()
    {
        string value = new('x', 65536);
        return Encoding.UTF8.GetBytes("{\"a\":\"" + value + "\",\"b\":\"" + value
            + "\",\"c\":\"" + value + "\",\"d\":\"" + value + "\"}");
    }
}
