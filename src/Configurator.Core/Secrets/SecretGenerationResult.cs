using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Configurator.Core.Definitions;

namespace Configurator.Core.Secrets;

public sealed class SecretGenerationResult : IDisposable
{
    public SecretGenerationResult(SecretValue? output, SecretValue? generatedPassword = null, string? errorCode = null)
    {
        Output = output;
        GeneratedPassword = generatedPassword;
        ErrorCode = errorCode;
    }
    public SecretValue? Output { get; }
    public SecretValue? GeneratedPassword { get; }
    public string? ErrorCode { get; }
    public bool Succeeded => Output is not null && ErrorCode is null;
    public void Dispose() { Output?.Dispose(); GeneratedPassword?.Dispose(); }
    public override string ToString() => $"SecretGenerationResult:Succeeded={Succeeded}";
}
