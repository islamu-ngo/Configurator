namespace Configurator.Artifacts;

using System.Security.Cryptography;

/// <summary>Owns one bounded native input until its consumer disposes the cleared storage.</summary>
public sealed class BoundedFileInput : IDisposable
{
    private byte[]? _storage;
    private readonly int _length;

    private BoundedFileInput(byte[] storage, int length)
    {
        _storage = storage;
        _length = length;
    }

    public ReadOnlyMemory<byte> Bytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_storage is null, this);
            return _storage.AsMemory(0, _length);
        }
    }

    public static Task<BoundedFileInput> ReadAsync(string path, int maximumBytes,
        CancellationToken cancellationToken = default) =>
        ReadAsync(() => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan), maximumBytes, cancellationToken);

    /// <summary>
    /// Runs open, stat and read off the calling owner. Cancellation waits for physical I/O and
    /// stream disposal before clearing rejected storage; the returned task must remain observed.
    /// The opener selects native authority, not admission policy. Length changes reject.
    /// </summary>
    public static Task<BoundedFileInput> ReadAsync(Func<Stream> openRead, int maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openRead);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfEqual(maximumBytes, int.MaxValue);
        return Task.Run(async () =>
        {
            byte[] storage = [];
            try
            {
                int length;
                cancellationToken.ThrowIfCancellationRequested();
                await using (Stream stream = openRead())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long expected = stream.Length;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (expected is < 1 || expected > maximumBytes) throw new InvalidDataException();
                    length = (int)expected;
                    storage = new byte[length + 1];
                    int count = 0;
                    while (count < storage.Length)
                    {
                        int read = await stream.ReadAsync(storage.AsMemory(count), cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (read == 0) break;
                        count += read;
                    }
                    if (count != length || stream.Length != expected) throw new InvalidDataException();
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new BoundedFileInput(storage, length);
            }
            catch
            {
                CryptographicOperations.ZeroMemory(storage);
                throw;
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        byte[]? storage = Interlocked.Exchange(ref _storage, null);
        if (storage is not null) CryptographicOperations.ZeroMemory(storage);
    }
}
