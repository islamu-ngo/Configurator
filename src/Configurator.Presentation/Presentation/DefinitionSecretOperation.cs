namespace Configurator.Presentation;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

public sealed record DefinitionSecretOperationResult(bool Succeeded, string Code, bool HasGeneratedPassword = false,
    bool ObserverFailed = false);
public sealed record DefinitionSecretOperation(bool IsRunning, string Code, bool HasGeneratedPassword,
    [property: JsonIgnore] string? EditorIdentity = null)
{
    public override string ToString() => $"{nameof(DefinitionSecretOperation)}:{Code}:Running={IsRunning}";
}

public sealed partial class DefinitionFormModel
{
    private long _secretGeneration;
    private bool _secretRunning;
    private SecretGenerationResult? _secretResult;
    private DefinitionSecretOperation _secretOperation = new(false, "secret-idle", false);

    public DefinitionSecretOperation SecretOperation { get { lock (_gate) return _secretOperation; } }

    public bool CanGenerateSecret(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate) return !_secretRunning && SecretEligible(path);
    }

    private bool SecretEligible(IReadOnlyList<string> path)
    {
        if (_disposed || !TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) || !CanEdit(path) ||
            Facts.EditorState(path)?.IsActive != true ||
            node.Schema.Secret is not { Acquisition: DefinitionSecretAcquisition.Local, Generation: not null }) return false;
        if (ReadOnly(Definition.Configuration) || Omitted.Any(omitted =>
            omitted.Segments.Count < path.Count && path.Take(omitted.Segments.Count).SequenceEqual(omitted.Segments))) return false;
        for (int count = 1; count <= path.Count; count++)
            if (GetEditorSchema(path.Take(count).ToArray()) is { } ancestor && ReadOnly(ancestor)) return false;
        return true;

        static bool ReadOnly(DefinitionNode schema) => schema.Keywords.TryGetProperty("readOnly", out JsonElement readOnly)
            && readOnly.ValueKind == JsonValueKind.True;
    }

    /// <summary>Copies transient input, applies only to the captured editor revision, and owns any original password until dismissal.</summary>
    public async Task<DefinitionSecretOperationResult> GenerateSecretAsync(IReadOnlyList<string> path,
        ReadOnlyMemory<char> input = default, Func<CancellationToken, ValueTask>? cooperativeYield = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        string[] location = path.ToArray();
        DefinitionSecretSpecification specification;
        string? identity = null;
        long revision, generation;
        char[]? ownedInput = null;
        bool started = false;
        SecretGenerationResult? result = null;
        DefinitionSecretOperationResult? committed = null;
        string terminalCode = "secret-generation-failed";
        DefinitionSecretOperationResult Reject(string code)
        {
            terminalCode = code;
            return new(false, code);
        }
        try
        {
            using (BeginMutation())
            {
                if (_secretRunning) return new(false, "secret-busy");
                if (!SecretEligible(location)) return new(false, "secret-operation-unavailable");
                if (cancellationToken.IsCancellationRequested) return new(false, "secret-cancelled");
                if (input.Length > 65536 || Encoding.UTF8.GetByteCount(input.Span) > 65536)
                    return new(false, "secret-input-limit");
                RetireSecretResult();
                specification = GetEditorSchema(location)!.Secret!;
                identity = GetEditorIdentity(location);
                revision = InputRevision;
                generation = _secretGeneration;
                ownedInput = input.ToArray();
                _secretRunning = started = true;
                _secretOperation = new(true, "secret-running", false, identity);
                OnPropertyChanged(nameof(SecretOperation));
                OnViewChanged();
            }
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            result = await Task.Run(() => SecretGenerator.Generate(specification, ownedInput), cancellationToken);
            if (cooperativeYield is not null) await cooperativeYield(cancellationToken);
            using (BeginMutation())
            {
                if (cancellationToken.IsCancellationRequested) return Reject("secret-cancelled");
                if (_disposed || generation != _secretGeneration || revision != InputRevision ||
                    !SecretEligible(location) || GetEditorIdentity(location) != identity)
                    return Reject("secret-stale");
                if (!result.Succeeded) return Reject(result.ErrorCode ?? "secret-generation-failed");
                JsonElement value = JsonSerializer.SerializeToElement(result.Output!.CopyValue());
                if (!DefinitionValidator.ValueValid(GetEditorSchema(location)!, value))
                    return Reject("secret-generated-value-invalid");
                if (!SetValue(location, value))
                    return Reject("secret-apply-failed");
                Fields[_index!.FieldPositions[location[0]]].RecordOrigin(location, DefinitionAnswerOrigin.Generated);
                _facts = null;
                _secretResult = result;
                result = null;
                _secretOperation = new(false, "secret-generated", _secretResult.GeneratedPassword is not null, identity);
                committed = new(true, "secret-generated", _secretOperation.HasGeneratedPassword);
                OnPropertyChanged(nameof(SecretOperation));
                OnViewChanged();
                return committed;
            }
        }
        catch (OperationCanceledException) { return Reject("secret-cancelled"); }
        catch (AggregateException)
        {
            terminalCode = "secret-observer-failed";
            return committed is not null ? committed with { ObserverFailed = true }
                : new(false, "secret-observer-failed", ObserverFailed: true);
        }
        catch (Exception) { return committed ?? Reject("secret-generation-failed"); }
        finally
        {
            if (ownedInput is not null) CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(ownedInput.AsSpan()));
            result?.Dispose();
            if (started)
            {
                try
                {
                    using (BeginMutation())
                    {
                        _secretRunning = false;
                        if (_secretOperation.IsRunning) _secretOperation = new(false, terminalCode, false, identity);
                        if (!_disposed)
                        {
                            OnPropertyChanged(nameof(SecretOperation));
                            OnViewChanged();
                        }
                    }
                }
                catch (AggregateException) { /* Observer failure cannot undo a committed answer or prevent cleanup. */ }
            }
        }
    }

    /// <summary>Explicit custody transfer to the caller; subsequent clearing cannot erase this string or clipboard copies.</summary>
    public string? CopyGeneratedPassword()
    {
        lock (_gate) return _disposed ? null : _secretResult?.GeneratedPassword?.CopyValue();
    }

    public void DismissSecretResult()
    {
        using (BeginMutation())
        {
            RetireSecretResult();
            if (!_disposed) OnViewChanged();
        }
    }

    private void RetireSecretResult()
    {
        _secretGeneration = checked(_secretGeneration + 1);
        _secretResult?.Dispose();
        _secretResult = null;
        _secretOperation = new(_secretRunning, "secret-retired", false);
        OnPropertyChanged(nameof(SecretOperation));
    }
}
