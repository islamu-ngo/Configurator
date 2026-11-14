namespace Configurator.Presentation;

using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

/// <summary>
/// Workspace-bound secret generator session with candidate preview custody, live compliance checks,
/// operation-generation fencing, and synchronous zeroization on cancel, consent revocation, or disposal.
/// </summary>
public sealed class DefinitionSecretGeneratorSession : ObservableObject, IDisposable
{
    private readonly DefinitionFormModel _form;
    private readonly DefinitionPath _path;
    private readonly string _identity;
    private readonly DefinitionSecretSpecification _specification;

    private SecretGenerationOptions _options;
    private SecretGenerationAssessment _assessment;
    private SecretGenerationResult? _candidate;
    private long _capturedGeneration;
    private long _capturedRevision;
    private bool _hasCandidate;
    private bool _stale;
    private bool _isBusy;
    private bool _formDisposed;
    private bool _disposed;
    private string? _errorCode;

    internal DefinitionSecretGeneratorSession(
        DefinitionFormModel form,
        DefinitionPath path,
        string identity,
        DefinitionSecretSpecification specification,
        SecretGenerationBounds bounds)
    {
        _form = form;
        _path = path;
        _identity = identity;
        _specification = specification;
        Bounds = bounds;
        _options = SecretGenerationOptions.FromBounds(bounds);
        _assessment = bounds.Evaluate(_options);
    }

    public SecretGenerationBounds Bounds { get; }
    public SecretGenerationOptions Options => _options;
    public SecretGenerationAssessment Assessment => _assessment;
    public DefinitionPath TargetPath => _path;

    public bool HasCandidate => !_disposed && !_stale && _hasCandidate;
    public bool IsBusy => !_disposed && _isBusy;
    public bool IsStale => _stale;
    public string? ErrorCode => _errorCode;

    public double EstimatedEntropyBits => _assessment.EntropyBits;
    public int EffectiveAlphabetSize => _assessment.EffectiveAlphabetSize;
    public bool MeetsPolicy => _assessment.IsValid;
    public bool MeetsSchemaBounds => _assessment.MeetsSchemaBounds;
    public bool MeetsCategoryPolicy => _assessment.MeetsCategoryPolicy;
    public bool MeetsEntropyFloor => _assessment.MeetsEntropyFloor;

    public bool CanGenerate => !_disposed && !_formDisposed && !_isBusy &&
        (Bounds.SupportsOptions || Bounds.ExactByteCount > 0) &&
        _assessment.IsValid && _form.CanGenerateSecretFromSession(_path);

    public bool CanApply => !_disposed && !_formDisposed && !_isBusy && !_stale && _hasCandidate &&
        _form.CanGenerateSecretFromSession(_path);

    public void UpdateOptions(SecretGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using (_form.BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RetireCandidateLocked(stale: false);
            _options = options;
            _assessment = Bounds.Evaluate(options);
            _errorCode = _assessment.ErrorCode;
            _form.NotifyAfterMutation(RaiseStateChanged);
        }
    }

    public Task<DefinitionSecretOperationResult> GeneratePreviewAsync(CancellationToken cancellationToken = default) =>
        GeneratePreviewAsync(cooperativeYield: null, cancellationToken);

    internal async Task<DefinitionSecretOperationResult> GeneratePreviewAsync(
        Func<CancellationToken, Task>? cooperativeYield,
        CancellationToken cancellationToken = default)
    {
        long revision;
        long generation;
        using (_form.BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_formDisposed || !_form.CanGenerateSecretFromSession(_path))
            {
                _errorCode = "secret-operation-unavailable";
                _form.NotifyAfterMutation(RaiseStateChanged);
                return new(false, "secret-operation-unavailable");
            }
            if (_isBusy)
                return new(false, "secret-busy");
            if (cancellationToken.IsCancellationRequested)
                return new(false, "secret-cancelled");

            SecretGenerationAssessment assessment = Bounds.Evaluate(_options);
            _assessment = assessment;
            if (!assessment.IsValid)
            {
                RetireCandidateLocked(stale: false);
                _errorCode = assessment.ErrorCode;
                _form.NotifyAfterMutation(RaiseStateChanged);
                return new(false, assessment.ErrorCode ?? "secret-generation-policy-invalid");
            }

            RetireCandidateLocked(stale: false);
            _isBusy = true;
            _errorCode = null;
            revision = _form.InputRevision;
            generation = _form.CurrentSecretGeneration;
            _form.NotifyAfterMutation(RaiseStateChanged);
        }

        SecretGenerationResult? generated = null;
        try
        {
            await Task.Yield();
            if (cancellationToken.IsCancellationRequested)
                return FailBeforePublish("secret-cancelled");
            if (cooperativeYield is not null)
                await cooperativeYield(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return FailBeforePublish("secret-cancelled");

            generated = SecretGenerator.Generate(_specification, _options);

            using (_form.BeginMutation())
            {
                _isBusy = false;
                if (_disposed || _formDisposed || !(_form.CurrentSecretGeneration == generation && _form.InputRevision == revision) ||
                    !_form.CanGenerateSecretFromSession(_path) || _form.GetEditorIdentity(_path) != _identity)
                {
                    generated.Dispose();
                    generated = null;
                    _stale = true;
                    _errorCode = "secret-stale";
                    if (!_disposed && !_formDisposed) _form.NotifyAfterMutation(RaiseStateChanged);
                    return new(false, "secret-stale");
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    generated.Dispose();
                    generated = null;
                    _errorCode = "secret-cancelled";
                    _form.NotifyAfterMutation(RaiseStateChanged);
                    return new(false, "secret-cancelled");
                }
                if (!generated.Succeeded)
                {
                    string code = generated.ErrorCode ?? "secret-generation-failed";
                    generated.Dispose();
                    generated = null;
                    _errorCode = code;
                    _form.NotifyAfterMutation(RaiseStateChanged);
                    return new(false, code);
                }

                JsonElement previewValue = JsonSerializer.SerializeToElement(generated.Output!.CopyValue());
                DefinitionNode? schema = _form.GetEditorSchema(_path.Segments);
                if (schema is null || !DefinitionValidator.ValueValid(schema, previewValue))
                {
                    generated.Dispose();
                    generated = null;
                    _errorCode = "secret-generated-value-invalid";
                    _form.NotifyAfterMutation(RaiseStateChanged);
                    return new(false, "secret-generated-value-invalid");
                }

                _candidate = generated;
                generated = null;
                _capturedGeneration = generation;
                _capturedRevision = revision;
                _hasCandidate = true;
                _stale = false;
                _errorCode = "secret-preview-ready";
                _form.NotifyAfterMutation(RaiseStateChanged);
                return new(true, "secret-preview-ready", _candidate.GeneratedPassword is not null);
            }
        }
        catch (OperationCanceledException)
        {
            generated?.Dispose();
            return FailBeforePublish("secret-cancelled");
        }
        finally
        {
            generated?.Dispose();
        }

        DefinitionSecretOperationResult FailBeforePublish(string code)
        {
            using (_form.BeginMutation())
            {
                _isBusy = false;
                _errorCode = code;
                if (!_disposed && !_formDisposed) _form.NotifyAfterMutation(RaiseStateChanged);
                return new(false, code);
            }
        }
    }

    /// <summary>Explicit caller custody transfer of the preview value without writing it to form state or any clipboard.</summary>
    public string? CopyPreview()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_form.SyncGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_formDisposed || _stale || !_hasCandidate || _candidate?.Output is null || !_form.CanGenerateSecretFromSession(_path))
                return null;
            return _candidate.Output.CopyValue();
        }
    }

    public Task<DefinitionSecretOperationResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        using (_form.BeginMutation(PresentationStrings.Get("UndoActionGenerateSecret", _form.Locale)))
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (cancellationToken.IsCancellationRequested)
            {
                RetireCandidateLocked(stale: false);
                _errorCode = "secret-cancelled";
                if (!_formDisposed) _form.NotifyAfterMutation(RaiseStateChanged);
                return Task.FromResult(new DefinitionSecretOperationResult(false, "secret-cancelled"));
            }
            if (_isBusy)
                return Task.FromResult(new DefinitionSecretOperationResult(false, "secret-busy"));
            if (_formDisposed || !_form.CanGenerateSecretFromSession(_path))
            {
                bool staleFromEdit = _stale;
                RetireCandidateLocked(stale: staleFromEdit);
                string code = staleFromEdit ? "secret-stale" : "secret-operation-unavailable";
                _errorCode = code;
                if (!_formDisposed) _form.NotifyAfterMutation(RaiseStateChanged);
                return Task.FromResult(new DefinitionSecretOperationResult(false, code));
            }
            if (_stale || _candidate is null || !_hasCandidate ||
                _capturedGeneration != _form.CurrentSecretGeneration ||
                _capturedRevision != _form.InputRevision ||
                _form.GetEditorIdentity(_path) != _identity)
            {
                RetireCandidateLocked(stale: true);
                _errorCode = "secret-stale";
                _form.NotifyAfterMutation(RaiseStateChanged);
                return Task.FromResult(new DefinitionSecretOperationResult(false, "secret-stale"));
            }

            SecretGenerationResult? staged = _candidate;
            _candidate = null;
            _hasCandidate = false;
            try
            {
                DefinitionSecretOperationResult result = _form.CommitGeneratedSecretResult(_path, _identity, ref staged, cancellationToken);
                _errorCode = result.Code;
                if (!_formDisposed) _form.NotifyAfterMutation(RaiseStateChanged);
                return Task.FromResult(result);
            }
            finally
            {
                staged?.Dispose();
            }
        }
    }

    public void Cancel()
    {
        using (_form.BeginMutation())
        {
            if (_disposed) return;
            RetireCandidateLocked(stale: false);
            _errorCode = "secret-cancelled";
            if (!_formDisposed) _form.NotifyAfterMutation(RaiseStateChanged);
        }
    }

    internal void RetireFromForm(bool consentChanged, bool formDisposed)
    {
        if (consentChanged)
        {
            RetireCandidateLocked(stale: false);
            _errorCode = _form.CanGenerateSecretFromSession(_path) ? null : "secret-operation-unavailable";
        }
        else if (_hasCandidate || _candidate is not null)
        {
            RetireCandidateLocked(stale: !formDisposed && _hasCandidate);
        }
        if (formDisposed) _formDisposed = true;
        if (!_disposed && (!_formDisposed || consentChanged)) _form.NotifyAfterMutation(RaiseStateChanged);
    }

    public void Dispose()
    {
        using (_form.BeginMutation())
        {
            if (_disposed) return;
            _disposed = true;
            RetireCandidateLocked(stale: false);
            _form.UnregisterSecretSession(this);
        }
    }

    public override string ToString() =>
        $"{nameof(DefinitionSecretGeneratorSession)}:HasCandidate={HasCandidate}:EntropyBits={EstimatedEntropyBits:F1}:Stale={_stale}";

    private void RetireCandidateLocked(bool stale)
    {
        _candidate?.Dispose();
        _candidate = null;
        _hasCandidate = false;
        _stale = stale;
    }

    private void RaiseStateChanged() => OnPropertyChanged(string.Empty);
}
