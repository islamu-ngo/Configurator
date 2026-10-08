namespace Configurator.Terminal;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Presentation;

internal sealed record SetupTerminalArtifactResult(ProtectedArtifactStatus Status, string? Digest)
{
    public override string ToString() => $"{nameof(SetupTerminalArtifactResult)}:{Status}";
}

/// <summary>Owns the restricted prepared bytes; the shared workspace fences saves.</summary>
internal sealed class SetupTerminalArtifactOperation : ISetupPresentationOperation, IDisposable
{
    private readonly IProtectedArtifactWriter _writer;
    private readonly string _baseDirectory;
    private readonly Func<CancellationToken, Task>? _beforeCommit;
    private byte[] _bytes = [];
    private string _artifactId;
    private bool _disposed;

    internal SetupTerminalArtifactOperation(
        DefinitionFormModel form, IProtectedArtifactWriter writer, string baseDirectory,
        Func<CancellationToken, Task>? beforeCommit = null)
    {
        Form = form;
        _writer = writer;
        _baseDirectory = baseDirectory;
        _beforeCommit = beforeCommit;
        _artifactId = form.Definition.Artifacts[0].Id;
        FileName = form.Definition.Artifacts[0].FileName;
        Form.InputChanged += InputChanged;
    }

    internal DefinitionFormModel Form { get; }
    internal event EventHandler? Invalidated;
    internal DefinitionPreparationMetadata? Preparation { get; private set; }
    internal bool IsPrepared => _bytes.Length > 0 && Preparation?.InputRevision == Form.InputRevision;
    internal bool ProtectedOutputAvailable => _writer.IsAvailable;
    internal string FileName { get; set; }
    internal string ArtifactId
    {
        get => _artifactId;
        set
        {
            if (_artifactId == value) return;
            var artifact = Form.Definition.Artifacts.Single(item => item.Id == value);
            Invalidate();
            _artifactId = value;
            FileName = artifact.FileName;
        }
    }

    internal async Task<bool> PrepareAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Invalidate();
        if (Form.HasInputErrors) return false;
        var outcome = await new DefinitionPresentationOperation(
            Form.Definition, ArtifactId, Form.Snapshot()).ExecuteAsync(cancellationToken);
        try
        {
            Preparation = (DefinitionPreparationMetadata)outcome.CoreResult;
            if (!Preparation.Succeeded || Preparation.InputRevision != Form.InputRevision)
                return false;
            _bytes = outcome.Output.ToArray();
            return IsPrepared;
        }
        finally
        {
            if (MemoryMarshal.TryGetArray(outcome.Output, out ArraySegment<byte> segment))
                CryptographicOperations.ZeroMemory(segment.AsSpan());
        }
    }

    public async Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        byte[] bytes = _bytes;
        DefinitionPreparationMetadata? metadata = Preparation;
        _bytes = [];
        Preparation = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.Length == 0 || metadata?.InputRevision != Form.InputRevision
                || !SetupTerminalFileName.IsSafe(FileName))
                return Outcome(ProtectedArtifactStatus.InvalidRequest, null);
            if (!_writer.IsAvailable)
                return Outcome(ProtectedArtifactStatus.UnsupportedPlatform, null);
            string? digest = metadata.Digest?.Value;
            using ProtectedArtifactPreparation preparation = await _writer.PrepareAsync(
                SetupArtifactKind.Configuration, Path.Combine(_baseDirectory, FileName), bytes, cancellationToken);
            if (_beforeCommit is not null) await _beforeCommit(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Outcome(await preparation.CommitAsync(cancellationToken), digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static SetupPresentationOutcome Outcome(ProtectedArtifactStatus status, string? digest) =>
        new(new SetupTerminalArtifactResult(status, digest), ReadOnlyMemory<byte>.Empty);

    private void InputChanged(object? sender, EventArgs args) => Invalidate();

    internal void Invalidate()
    {
        ClearBytes();
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private void ClearBytes()
    {
        CryptographicOperations.ZeroMemory(_bytes);
        _bytes = [];
        Preparation = null;
    }

    internal void ClearPrivateState()
    {
        Invalidate();
        foreach (var input in Form.Fields.Where(field => field.RequiresMasking))
            input.Text = string.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Form.InputChanged -= InputChanged;
        ClearPrivateState();
        Invalidated = null;
    }
}

internal static class SetupTerminalFileName
{
    internal const int MaximumLength = 64;
    internal static bool IsPartialSafe(string value) => value.Length <= MaximumLength && value.All(IsAllowed);
    internal static bool IsSafe(string value) => value.Length is > 0 and <= MaximumLength
        && value.All(IsAllowed) && value is not "-" and not "." and not "..";
    private static bool IsAllowed(char value) => value is >= 'a' and <= 'z'
        or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-';
}
