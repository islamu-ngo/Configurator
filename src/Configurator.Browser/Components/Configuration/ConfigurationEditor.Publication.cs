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
    private async Task PrepareAsync(bool draft)
    {
        if (_disposed || !_ready) return;
        if (_form!.RequiresBrowserProfileComposition)
        {
            SetStatus("ProfilePrivateOutputRequired");
            _statusCode = "profile-private-output-required";
            return;
        }
        if (_activeWorkflowStage != WorkspaceStage.Files)
            SelectWorkflowStage(WorkspaceStage.Files);
        DefinitionFormModel form = _form!;
        DefinitionSessionPrivacy privacy = form.SessionPrivacy;
        long inputRevision = form.InputRevision;
        long generation = _generation + 1;
        await InvalidateOutputAsync();
        if (!IsCurrentPreparation(form, generation, inputRevision, privacy)) return;
        _activeWorkflowStage = WorkspaceStage.Files;
        AnswerSnapshot answers;
        try { answers = form.Snapshot(); }
        catch (DefinitionException)
        {
            SetStatus("AnswersBudgetExceededStatus");
            _statusCode = "answer-budget-exceeded";
            _diagnostics.Add("answer-budget-exceeded");
            return;
        }
        if (form.HasAnswerBudgetError)
        {
            SetStatus("InputBudgetExceededStatus");
            _statusCode = "answer-budget-exceeded";
            _diagnostics.Add("answer-budget-exceeded");
            return;
        }
        if (form.HasInputErrors)
        {
            _validation = draft ? DefinitionValidator.ValidateDraft(Definition, answers) : form.Validate();
            _errorSummaryRequested = true;
            _diagnostics.Add("input-type-invalid");
            SetStatus("InputTypeInvalidStatus");
            _statusCode = "input-type-invalid";
            return;
        }
        DefinitionPreparationResult? prepared = null;
        var staged = new List<PreparedArtifact>();
        CancellationToken cancellationToken = _mountCancellation.Token;
        try
        {
            async ValueTask YieldPreparationAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (!IsCurrentPreparation(form, generation, inputRevision, privacy))
                    throw new OperationCanceledException(token);
                await YieldComparisonAsync(token);
                if (!IsCurrentPreparation(form, generation, inputRevision, privacy))
                    throw new OperationCanceledException(token);
            }

            prepared = draft
                ? await DefinitionPreparationSession.PrepareActiveDraftAsync(
                    Definition, answers, YieldPreparationAsync, cancellationToken)
                : await DefinitionPreparationSession.PrepareActiveAsync(
                    Definition, answers, YieldPreparationAsync, cancellationToken);
            if (!IsCurrentPreparation(form, generation, inputRevision, privacy)) return;

            _validation = prepared.Validation;
            IReadOnlyList<DefinitionArtifact> artifacts;
            if (draft)
            {
                if (prepared.Artifacts.Count == 0 || prepared.Diagnostics.Any(item => item.Code is not ("draft-dependency-unresolved" or "artifact-omitted")))
                {
                    _errorSummaryRequested = true;
                    _diagnostics.AddRange(prepared.Diagnostics.Select(item => $"{item.FieldId}: {item.Code}"));
                    SetStatus("PreparationRejectedStatus");
                    _statusCode = "preparation-rejected";
                    return;
                }
                artifacts = prepared.Artifacts;
                _diagnostics.AddRange(prepared.Diagnostics.Select(item => item.Code));
            }
            else
            {
                if (!prepared.Succeeded)
                {
                    _errorSummaryRequested = true;
                    _diagnostics.AddRange(prepared.Diagnostics.Select(item => $"{item.FieldId}: {item.Code}"));
                    SetStatus("PreparationRejectedStatus");
                    _statusCode = "preparation-rejected";
                    return;
                }
                artifacts = prepared.Artifacts;
            }
            if (!IsCurrentPreparation(form, generation, inputRevision, privacy)) return;
            foreach (DefinitionArtifact artifact in artifacts)
            {
                using DefinitionPreparedAdoption operation = DefinitionPresentationOperation.Adopt(
                    artifact, draft ? prepared.Completions : null);
                WorkspaceId.TryCreate(Guid.NewGuid().ToString("N"), out var workspaceId);
                var workspace = _session.CreateWorkspace(workspaceId, operation);
                bool retained = false;
                try
                {
                    workspace.Activate();
                    await workspace.ExecuteAsync(Guid.NewGuid());
                    if (!IsCurrentPreparation(form, generation, inputRevision, privacy)) return;
                    if (workspace.Result is not DefinitionPreparationMetadata { Succeeded: true } metadata)
                    {
                        if (workspace.Result is DefinitionPreparationMetadata failure) _diagnostics.AddRange(failure.DiagnosticCodes);
                        SetStatus("PreparationRetryStatus");
                        _statusCode = "preparation-rejected";
                        return;
                    }
                    staged.Add(new PreparedArtifact(metadata, workspace, Javascript));
                    retained = true;
                }
                finally { if (!retained) workspace.Dispose(); }
            }
            DefinitionOutputExportContext exportContext = form.CaptureOutputExportContext(
                answers.InputRevision, form.EligibleArtifacts.Select(artifact => artifact.Id));
            _prepared.AddRange(staged);
            _hadPreparedOutput = true;
            _outputExportContext = exportContext;
            staged.Clear();
            _navigation?.RefreshWork(this, _navigationLease);
            bool incompleteDraft = draft && (_prepared.Any(item => item.Metadata.IsDraft) ||
                _diagnostics.Contains("draft-dependency-unresolved"));
            SetStatus(incompleteDraft
                ? (_prepared.Count == 1 ? "PreparedDraftStatus" : "PreparedDraftsStatus")
                : (_prepared.Count == 1 ? "PreparedArtifactStatus" : "PreparedArtifactsStatus"), _prepared.Count);
            _statusCode = "preparation-ready";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            foreach (PreparedArtifact artifact in staged) await artifact.DisposeAsync();
            prepared?.Dispose();
        }
    }

    private bool IsCurrentPreparation(DefinitionFormModel form, long generation, long inputRevision,
        DefinitionSessionPrivacy privacy) =>
        !_disposed && _ready && generation == _generation && ReferenceEquals(form, _form)
        && form.InputRevision == inputRevision && form.SessionPrivacy == privacy;
    private async Task ExportAllAsync(bool archive)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!IsCurrentView(form, view) || !_ready || !IsFilesSurfaceActive
            || _prepared.Count == 0 || _outputExportContext is not { } exportContext) return;
        _lastAggregateExportWasArchive = archive;
        if (_prepared.Count == 1)
        {
            if (archive) await DownloadAsync(_prepared[0]);
            else await CopyAsync(_prepared[0]);
            return;
        }
        InvalidateImport();
        long generation = _generation;
        _bundleExport ??= new BrowserArtifactBundleExport(Javascript);
        var snapshot = _prepared.ToArray();
        var entries = snapshot.Select(artifact => (artifact.Metadata.FileName!, artifact.Workspace.Output)).ToArray();
        if (!_bundleExport.SetOutput(entries, archive, _mountCancellation.Token))
        {
            SetStatus("AggregateExportRejectedStatus");
            _statusCode = "aggregate-export-rejected";
            return;
        }
        bool exposed = archive ? await _bundleExport.DownloadAsync() : await _bundleExport.CopyAsync();
        if (!IsCurrentView(form, view) || !_ready || generation != _generation || snapshot.Length != _prepared.Count
            || !snapshot.SequenceEqual(_prepared)) return;
        if (exposed)
            form.RecordOutputExport(exportContext, snapshot.Select(artifact => artifact.Metadata.ArtifactId!),
                archive ? DefinitionOutputDeliveryKind.DownloadRequested : DefinitionOutputDeliveryKind.ClipboardCopied);
        SetStatus(exposed ? archive ? "AggregateDownloadedStatus" : "AggregateCopiedStatus"
            : archive ? "DownloadUnavailableStatus" : "ClipboardUnavailableStatus");
        _statusCode = exposed ? archive ? "aggregate-download-requested" : "aggregate-copied" : "aggregate-export-unavailable";
    }
    private async Task CopyAsync(PreparedArtifact artifact)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!IsCurrentExport(artifact, form, view) || _outputExportContext is not { } exportContext) return;
        InvalidateImport();
        if (!artifact.Export.HasOutput) artifact.Export.SetOutput(artifact.Workspace.Output);
        bool copied = await artifact.Export.CopyAsync();
        if (!IsCurrentExport(artifact, form, view)) return;
        if (copied) form.RecordOutputExport(exportContext, [artifact.Metadata.ArtifactId!], DefinitionOutputDeliveryKind.ClipboardCopied);
        artifact.StatusKey = copied ? "ArtifactCopiedStatus" : "ClipboardUnavailableStatus";
        artifact.StatusCode = copied ? "copied" : "clipboard-unavailable";
    }
    private async Task DownloadAsync(PreparedArtifact artifact)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!IsCurrentExport(artifact, form, view) || _outputExportContext is not { } exportContext) return;
        InvalidateImport();
        if (!artifact.Export.HasOutput) artifact.Export.SetOutput(artifact.Workspace.Output);
        bool downloaded = await artifact.Export.DownloadAsync(artifact.Metadata.FileName!, artifact.Metadata.MediaType!);
        if (!IsCurrentExport(artifact, form, view)) return;
        if (downloaded) form.RecordOutputExport(exportContext, [artifact.Metadata.ArtifactId!], DefinitionOutputDeliveryKind.DownloadRequested);
        artifact.StatusKey = downloaded ? "DownloadRequestedStatus" : "DownloadUnavailableStatus";
        artifact.StatusCode = downloaded ? "download-requested" : "download-unavailable";
    }
    private async Task RevealAsync(PreparedArtifact artifact)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!IsCurrentExport(artifact, form, view)) return;
        InvalidateImport();
        if (artifact.Revealed)
        {
            artifact.Revealed = false;
            await artifact.Export.InvalidateAsync();
            if (!IsCurrentExport(artifact, form, view)) return;
            artifact.StatusKey = "PreviewHiddenStatus";
            artifact.StatusCode = "preview-hidden";
            return;
        }
        if (artifact.View?.PreviewElement is not { } preview) return;
        if (!artifact.Export.HasOutput) artifact.Export.SetOutput(artifact.Workspace.Output);
        artifact.Revealed = true;
        StateHasChanged();
        bool revealed = await artifact.Export.RevealAsync(preview);
        if (!IsCurrentExport(artifact, form, view)) return;
        artifact.Revealed = revealed;
        artifact.StatusKey = revealed ? "PreviewRevealedStatus" : "PreviewUnavailableStatus";
        artifact.StatusCode = revealed ? "preview-revealed" : "preview-unavailable";
    }
    private bool IsCurrentExport(PreparedArtifact artifact, DefinitionFormModel form, long view) =>
        IsCurrentView(form, view) && _ready && IsFilesSurfaceActive && _prepared.Contains(artifact);
}
