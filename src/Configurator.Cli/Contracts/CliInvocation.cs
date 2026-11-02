using System.Globalization;

namespace Configurator.Cli;

public sealed record CliInvocation
{
    private readonly string[] _arguments;
    public CliInvocation(IEnumerable<string> arguments, CliMode mode, CliIo io, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = arguments.ToArray();
        if (_arguments.Any(argument => argument is null || argument.Length > 4096) || _arguments.Length > 256
            || _arguments.Sum(argument => System.Text.Encoding.UTF8.GetByteCount(argument) + 1) > 32_768)
            throw new ArgumentException("argument-bound-exceeded", nameof(arguments));
        Mode = mode;
        Io = io ?? throw new ArgumentNullException(nameof(io));
        Culture = CultureInfo.ReadOnly((CultureInfo)(culture ?? CultureInfo.CurrentUICulture).Clone());
    }
    public IReadOnlyList<string> Arguments => Array.AsReadOnly((string[])_arguments.Clone());
    public CliMode Mode { get; }
    public CliIo Io { get; }
    /// <summary>Read-only invocation snapshot for human rendering; machine output is culture-independent.</summary>
    public CultureInfo Culture { get; }
    public override string ToString() => $"{nameof(CliInvocation)}:Mode={Mode}:Count={_arguments.Length}";
}

