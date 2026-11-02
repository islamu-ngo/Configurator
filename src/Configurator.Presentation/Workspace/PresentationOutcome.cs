namespace Configurator.Presentation;

using System.Security.Cryptography;

/// <summary>Owns a private copy of operation output; disposal clears that copy.</summary>
public sealed class PresentationOutcome : IDisposable
{
    private byte[] _output;

    public PresentationOutcome(object coreResult, ReadOnlyMemory<byte> output)
    {
        ArgumentNullException.ThrowIfNull(coreResult);
        CoreResult = coreResult;
        _output = output.ToArray();
    }

    public object CoreResult { get; }

    public ReadOnlyMemory<byte> Output => _output;

    public void Dispose() => CryptographicOperations.ZeroMemory(Interlocked.Exchange(ref _output, []));

    public override string ToString() => nameof(PresentationOutcome);
}
