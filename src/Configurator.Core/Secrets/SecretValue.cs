using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Configurator.Core.Definitions;

namespace Configurator.Core.Secrets;

/// <summary>Mutable operation output with explicit ownership and redacted diagnostics.</summary>
public sealed class SecretValue : IDisposable
{
    private char[]? _characters;
    public SecretValue(ReadOnlySpan<char> value) => _characters = value.ToArray();
    public string CopyValue() => new(_characters ?? throw new ObjectDisposedException(nameof(SecretValue)));
    internal ReadOnlySpan<char> Characters => _characters ?? throw new ObjectDisposedException(nameof(SecretValue));
    public byte[] CopyUtf8Bytes() => Encoding.UTF8.GetBytes(_characters ?? throw new ObjectDisposedException(nameof(SecretValue)));
    public void Dispose()
    {
        char[]? characters = Interlocked.Exchange(ref _characters, null);
        if (characters is not null) CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
    }
    public override string ToString() => "SecretValue:Redacted";
}
