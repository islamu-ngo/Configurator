using System.Security.Cryptography;

namespace Configurator.Cli;

/// <summary>Owns the supplied byte array and clears it, including unused capacity, on disposal.</summary>
public sealed class SetupCliInputBuffer : IDisposable
{
    private byte[]? _ownedBytes;
    private readonly int _length;

    public SetupCliInputBuffer(byte[] ownedBytes) : this(ownedBytes, ownedBytes?.Length ?? 0) { }

    internal SetupCliInputBuffer(byte[] ownedBytes, int length)
    {
        ArgumentNullException.ThrowIfNull(ownedBytes);
        if ((uint)length > (uint)ownedBytes.Length) throw new ArgumentOutOfRangeException(nameof(length));
        _ownedBytes = ownedBytes;
        _length = length;
    }

    public ReadOnlyMemory<byte> Bytes =>
        (Volatile.Read(ref _ownedBytes) ?? throw new ObjectDisposedException(nameof(SetupCliInputBuffer))).AsMemory(0, _length);

    public void Dispose()
    {
        byte[]? bytes = Interlocked.Exchange(ref _ownedBytes, null);
        if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
    }
}
