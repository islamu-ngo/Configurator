namespace Configurator.Cli;

public sealed record CliIo
{
    public CliIo(ICliInput input, ICliWriter output, ICliWriter error,
        ICliAuthoringFiles authoringFiles, int maximumOutputBytes, int maximumBytes)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Error = error ?? throw new ArgumentNullException(nameof(error));
        AuthoringFiles = authoringFiles ?? throw new ArgumentNullException(nameof(authoringFiles));
        if (maximumOutputBytes is < 1 or > 65_536 || maximumBytes is < 1 or > 4 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        MaximumOutputBytes = maximumOutputBytes;
        MaximumBytes = maximumBytes;
    }
    public ICliInput Input { get; }
    public ICliWriter Output { get; }
    public ICliWriter Error { get; }
    public ICliAuthoringFiles AuthoringFiles { get; }
    public int MaximumOutputBytes { get; }
    public int MaximumBytes { get; }
    public override string ToString() => $"{nameof(CliIo)}:OutputBytes={MaximumOutputBytes}:Bytes={MaximumBytes}";
}

