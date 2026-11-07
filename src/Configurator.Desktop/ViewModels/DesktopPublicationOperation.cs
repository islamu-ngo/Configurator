namespace Configurator.Desktop.ViewModels;

using System.Security.Cryptography;
using Configurator.Artifacts;

/// <summary>Retains borrowed publication storage and copied facts until the native consumer and cleanup settle.</summary>
internal sealed class DesktopPublicationOperation : IDisposable
{
    private readonly object _gate = new();
    private readonly byte[]? _bytes;
    private readonly Action<ProtectedArtifactPublicationReceipt> _changed;
    private ProtectedArtifactPublicationReceipt _receipt;
    private bool _disposed;

    internal DesktopPublicationOperation(IEnumerable<string> artifactIds,
        Action<ProtectedArtifactPublicationReceipt> changed, byte[]? bytes = null)
    {
        _bytes = bytes;
        _changed = changed;
        _receipt = new(Guid.CreateVersion7(),
            artifactIds.Select(id => new ProtectedArtifactBundleOutcome(id, null)), false);
    }

    internal ProtectedArtifactPublicationReceipt Receipt { get { lock (_gate) return _receipt; } }

    internal void Observe(ProtectedArtifactBundleOutcome outcome)
    {
        ProtectedArtifactPublicationReceipt receipt;
        lock (_gate)
        {
            receipt = new(_receipt.AttemptId, _receipt.Outcomes.Select(item =>
                item.ArtifactId == outcome.ArtifactId ? outcome : item), false,
                _receipt.CompletionCode, _receipt.CleanupFailed, _receipt.IsUnknown, _receipt.Failed);
            _receipt = receipt;
        }
        _changed(receipt);
    }

    internal void Complete(IEnumerable<ProtectedArtifactBundleOutcome> outcomes,
        ProtectedArtifactBundleCompletionCode completionCode = ProtectedArtifactBundleCompletionCode.Completed,
        bool cleanupFailed = false)
    {
        lock (_gate)
            _receipt = new(_receipt.AttemptId, outcomes, false, completionCode, cleanupFailed);
    }

    internal void MarkUnknown(bool failed = false, bool cancelled = false)
    {
        ProtectedArtifactPublicationReceipt receipt;
        lock (_gate)
        {
            receipt = new(_receipt.AttemptId, _receipt.Outcomes, false,
                cancelled ? ProtectedArtifactBundleCompletionCode.Cancelled : _receipt.CompletionCode,
                _receipt.CleanupFailed, isUnknown: true, failed: failed || _receipt.Failed);
            _receipt = receipt;
        }
        _changed(receipt);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_bytes is not null) CryptographicOperations.ZeroMemory(_bytes);
        }
        finally
        {
            ProtectedArtifactPublicationReceipt receipt;
            lock (_gate)
            {
                receipt = new(_receipt.AttemptId, _receipt.Outcomes, true, _receipt.CompletionCode,
                    _receipt.CleanupFailed, _receipt.IsUnknown, _receipt.Failed);
                _receipt = receipt;
            }
            _changed(receipt);
        }
    }
}
