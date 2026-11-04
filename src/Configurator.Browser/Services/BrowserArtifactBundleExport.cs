namespace Configurator.Browser.Services;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Configurator.Core.Composition;
using Microsoft.JSInterop;

/// <summary>Owns the bounded private delivery container for one prepared artifact snapshot.</summary>
public sealed class BrowserArtifactBundleExport(IJSRuntime javascript) : IAsyncDisposable
{
    private readonly BrowserArtifactExport _export = new(javascript);
    public bool HasOutput => _export.HasOutput;
    public bool SetOutput(IReadOnlyList<(string FileName, ReadOnlyMemory<byte> Bytes)> entries, bool archive,
        CancellationToken cancellationToken = default)
    {
        _export.SetOutput(ReadOnlyMemory<byte>.Empty);
        cancellationToken.ThrowIfCancellationRequested();
        if (entries.Count is < 1 or > 64) return false;
        long sourceBytes = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sourceBytes += entry.Bytes.Length;
            if (sourceBytes > CompositionLimits.DefaultAggregateSourceBytes || !PortableName(entry.FileName)
                || !names.Add(entry.FileName)) return false;
        }
        long deliveryBytes = sourceBytes;
        if (!archive && entries.Count > 1)
            foreach (var entry in entries) deliveryBytes += Encoding.UTF8.GetByteCount($"--- {entry.FileName} ---\n")
                + (entry.Bytes.Span.EndsWith("\n"u8) ? 1 : 2);
        if (deliveryBytes > CompositionLimits.DefaultAggregateSourceBytes) return false;
        using var output = new BoundedOutput(archive ? CompositionLimits.DefaultAggregateSourceBytes : checked((int)deliveryBytes));
        try
        {
            if (archive)
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ZipArchiveEntry file = zip.CreateEntry(entry.FileName, CompressionLevel.Fastest);
                        file.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                        file.ExternalAttributes = 0;
                        using Stream target = file.Open();
                        target.Write(entry.Bytes.Span);
                    }
                }
            }
            else if (entries.Count == 1) output.Write(entries[0].Bytes.Span);
            else foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] header = Encoding.UTF8.GetBytes($"--- {entry.FileName} ---\n");
                output.Write(header);
                output.Write(entry.Bytes.Span);
                output.Write(entry.Bytes.Span.EndsWith("\n"u8) ? "\n"u8 : "\n\n"u8);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _export.SetOutput(output.GetBuffer().AsMemory(0, checked((int)output.Length)));
            return true;
        }
        catch (DeliveryLimitException) { return false; }
        finally { CryptographicOperations.ZeroMemory(output.GetBuffer()); }
    }

    private static bool PortableName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 128 || name is "." or ".." || name.EndsWith('.')
            || !name.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')) return false;
        string stem = name.Split('.')[0];
        return !stem.Equals("CON", StringComparison.OrdinalIgnoreCase) && !stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            && !stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) && !stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            && !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && char.IsAsciiDigit(stem[3]));
    }

    private sealed class DeliveryLimitException : IOException;

    private sealed class BoundedOutput : MemoryStream
    {
        internal BoundedOutput(int capacity) : base(new byte[capacity], 0, capacity, writable: true, publiclyVisible: true)
        {
            SetLength(0);
        }
        private void Check(int count)
        {
            if (count < 0 || Position + count > Capacity) throw new DeliveryLimitException();
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
        public override void SetLength(long value)
        {
            if (value > Capacity) throw new DeliveryLimitException();
            base.SetLength(value);
        }
    }
    public Task<bool> CopyAsync() => _export.CopyAsync();
    public Task<bool> DownloadAsync() => _export.DownloadAsync("configuration.zip", "application/zip");
    public ValueTask InvalidateAsync() => _export.InvalidateAsync();
    public ValueTask DisposeAsync() => _export.DisposeAsync();
}
