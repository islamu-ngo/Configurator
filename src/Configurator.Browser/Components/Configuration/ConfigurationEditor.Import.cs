namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Browser.Components.Common;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

public partial class ConfigurationEditor
{
    private void ChangeImportFormat(ChangeEventArgs args)
    {
        string? artifactId = args.Value?.ToString();
        if (!ImportArtifacts.Any(item => item.Id == artifactId)) return;
        InvalidateImport();
        _importArtifact = artifactId!;
        SetStatus("ImportFormatChangedStatus");
        _statusCode = "import-format-changed";
    }
    private void SelectCurrentImportArtifact()
    {
        _importOutputSelection = OutputSelection;
        var artifacts = ImportArtifacts.Select(artifact => artifact.Id).ToArray();
        if (OutputSelector is { } selector)
        {
            artifacts = _form!.Validate().TryGetEffectiveValue(selector.Location, out var value)
                ? selector.Options.FirstOrDefault(option => option.Value == value.GetString())?.ArtifactIds
                    .Where(id => artifacts.Contains(id, StringComparer.Ordinal)).ToArray() ?? []
                : [];
        }
        _importArtifact = _form!.ConfigurationContext?.PrimaryArtifactId is { } primary && artifacts.Contains(primary, StringComparer.Ordinal)
            ? primary : artifacts.Length == 1 ? artifacts[0] : string.Empty;
    }
    private void ChangeImportPurpose(ChangeEventArgs args)
    {
        if (args.Value is not ("compare" or "fill")) return;
        InvalidateImport();
        _importPurpose = (string)args.Value;
        SetStatus("ImportPurposeChangedStatus");
        _statusCode = "import-purpose-changed";
    }
    private void ClearComparison()
    {
        InvalidateImport();
        SetStatus("ImportClearedStatus");
        _statusCode = "import-cleared";
        RequestFocus(FocusTarget.Import);
    }
    private void ClearComparison(DefinitionImportReport report)
    {
        if (ReferenceEquals(_form?.ImportComparison, report)) ClearComparison();
    }
    private void CancelComparison(long generation)
    {
        if (generation == _generation) ClearComparison();
    }
    private async Task AdoptComparison(DefinitionImportReport report)
    {
        if (_importPending) return;
        var cancellation = new CancellationTokenSource();
        _readCancellation = cancellation;
        _importPending = true;
        _navigation?.RefreshWork(this, _navigationLease);
        StateHasChanged();
        try { await AdoptComparisonAsync(report, cancellation.Token); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_readCancellation, cancellation))
            {
                _readCancellation = null;
                _importPending = false;
                _navigation?.RefreshWork(this, _navigationLease);
            }
            cancellation.Dispose();
            if (!_disposed) StateHasChanged();
        }
    }
    private async Task AdoptComparisonAsync(DefinitionImportReport report, CancellationToken cancellationToken)
    {
        if (_disposed || !_ready || !ReferenceEquals(_form!.ImportComparison, report)) return;
        DefinitionFormModel form = _form!;
        long generation = _generation;
        long revision = form.InputRevision;
        await YieldComparisonAsync(cancellationToken);
        DefinitionImportResult result = await form.AdoptImportComparisonAsync(YieldComparisonAsync, cancellationToken);
        if (_disposed || !_ready || !ReferenceEquals(form, _form)) return;
        // Adoption itself raises InputChanged, retiring this operation exactly once.
        if (result.Succeeded
            ? _generation != generation + 1 || form.InputRevision != revision + 1
            : _generation != generation || cancellationToken.IsCancellationRequested) return;
        _statusCode = result.Succeeded ? "imported" : result.Code;
        SetStatus(result.Succeeded ? "ImportedStatus" : "AutofillRefusedStatus", result.Code);
        if (result.Succeeded) { ++_fileKey; RequestFocus(FocusTarget.Import); }
    }
    private async ValueTask YieldComparisonAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _comparisonRendering ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js").AsTask();
        IJSObjectReference module = await _comparisonRendering;
        cancellationToken.ThrowIfCancellationRequested();
        await module.InvokeVoidAsync("nextTask");
        cancellationToken.ThrowIfCancellationRequested();
    }
    private bool CanReadImport()
    {
        if (_disposed || !_ready) return false;
        if (_form!.SessionPrivacy != DefinitionSessionPrivacy.BrowserSecretsEnabled)
        {
            _importConsentRequired = true;
            SetStatus("ImportConsentRequiredStatus");
            _statusCode = "import-secret-consent-required";
            return false;
        }
        if (ImportCapability is not { InspectionSupported: true } ||
            _importPurpose == "fill" && !ImportCapability.StrictAutofillSupported)
        {
            SetStatus("ImportUnsupportedStatus");
            _statusCode = ImportCapability?.Code ?? "inverse-unsupported";
            return false;
        }
        return true;
    }
    private Task ImportAsync(InputFileChangeEventArgs args) =>
        LoadAsync(async (buffer, token) =>
        {
            await using Stream stream = args.File.OpenReadStream(MaximumImportBytes, token);
            return await ReadBoundedAsync(stream, buffer, token);
        });

    public Task LoadAsync(Stream stream) =>
        LoadAsync((buffer, token) => ReadBoundedAsync(stream, buffer, token));

    private static async Task<int> ReadBoundedAsync(Stream stream, Memory<byte> buffer, CancellationToken token)
    {
        int length = 0;
        while (length < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[length..], token);
            if (read == 0) break;
            length += read;
        }
        return length;
    }
#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
    private Task LoadAsync(Func<Memory<byte>, CancellationToken, Task<int>> read)
    {
        Task operation = LoadOwnershipObservedAsync(read);
        Configurator.Engineering.ImportComparisonOwnershipProbe.ObserveTask(operation, handler: true);
        return operation;
    }
    private async Task LoadOwnershipObservedAsync(Func<Memory<byte>, CancellationToken, Task<int>> read)
#else
    private async Task LoadAsync(Func<Memory<byte>, CancellationToken, Task<int>> read)
#endif
    {
        if (_disposed || !_ready) return;
        InvalidateImport(releaseFileControl: false);
        if (!CanReadImport())
        {
            ++_fileKey;
            RequestFocus(FocusTarget.Import);
            StateHasChanged();
            return;
        }
        DefinitionFormModel form = _form!;
        string artifactId = _importArtifact;
        string purpose = _importPurpose;
        long generation = _generation;
        var cancellation = new CancellationTokenSource();
        _readCancellation = cancellation;
        byte[] buffer = new byte[MaximumImportBytes + 1];
        _readBuffer = buffer;
#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
        Configurator.Engineering.ImportComparisonOwnershipProbe.ObserveInput(buffer, this, form, generation,
            static owner =>
            {
                var editor = (ConfigurationEditor)owner;
                return [editor._generation, editor._readBuffer is null ? 0 : 1,
                    editor._importPending ? 1 : 0, editor._readCancellation is null ? 0 : 1, editor._disposed ? 1 : 0];
            },
            static owner => ((DefinitionFormModel)owner).ReadImportOwnershipProbeState());
#endif
        _importPending = true;
        _navigation?.RefreshWork(this, _navigationLease);
        bool tooLarge = false;
        StateHasChanged();
        try
        {
            await YieldComparisonAsync(cancellation.Token);
            var comparison = form.CompareAsync(artifactId, async token =>
            {
                int length = await read(buffer, token);
                if (length > MaximumImportBytes)
                {
                    tooLarge = true;
                    throw new IOException();
                }
                return buffer.AsMemory(0, length);
            }, YieldComparisonAsync, cancellation.Token);
#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
            Configurator.Engineering.ImportComparisonOwnershipProbe.ObserveTask(comparison, handler: false);
#endif
            var result = await comparison;
            // Paint the detached report before remounting the file control and restoring focus.
            if (result.Succeeded) await YieldComparisonAsync(cancellation.Token);
            if (_disposed || !_ready || !ReferenceEquals(form, _form) || generation != _generation ||
                cancellation.IsCancellationRequested) return;
            if (tooLarge)
            {
                SetStatus("ImportTooLargeStatus");
                _statusCode = "import-too-large";
                return;
            }
            _statusCode = result.Code;
            SetStatus(result.Succeeded ? "ImportComparedStatus" : "ImportRefusedStatus", result.Code);
            if (result.Succeeded && purpose == "fill")
            {
                await AdoptComparisonAsync(result.Report!, cancellation.Token);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or InvalidOperationException or DefinitionException)
        {
            if (generation == _generation && ReferenceEquals(form, _form))
            {
                form.ClearImportComparison();
                SetStatus("ImportReadFailedStatus");
                _statusCode = "import-read-failed";
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            if (ReferenceEquals(_readBuffer, buffer)) _readBuffer = null;
            if (ReferenceEquals(_readCancellation, cancellation))
            {
                _readCancellation = null;
                _importPending = false;
                _navigation?.RefreshWork(this, _navigationLease);
                ++_fileKey;
                RequestFocus(FocusTarget.Import);
            }
            cancellation.Dispose();
            if (!_disposed) await InvokeAsync(StateHasChanged);
#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
            Configurator.Engineering.ImportComparisonOwnershipProbe.CompleteInput(buffer);
#endif
        }
    }
}
