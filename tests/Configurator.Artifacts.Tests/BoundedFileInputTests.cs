namespace Configurator.Artifacts.Tests;

using Configurator.Artifacts;

public sealed class BoundedFileInputTests
{
    [Test]
    public async Task NativeFileAtExactBoundRetainsOrderedBytesUntilDisposal()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, [3, 1, 4, 2]);
            var input = await BoundedFileInput.ReadAsync(path, 4);
            ReadOnlyMemory<byte> borrowed = input.Bytes;
            try { await Assert.That(borrowed.Span.SequenceEqual(new byte[] { 3, 1, 4, 2 })).IsTrue(); }
            finally { input.Dispose(); }
            input.Dispose();
            await Assert.That(borrowed.ToArray().All(value => value == 0)).IsTrue();
            await Assert.That(() => input.Bytes).Throws<ObjectDisposedException>();
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Arguments(3, 4, 8)]
    [Arguments(4, 3, 8)]
    [Arguments(9, 9, 8)]
    [Arguments(0, 0, 8)]
    public async Task GrowthTruncationOversizeAndEmptyRejectAndClearStorage(int reported, int actual, int maximum)
    {
        using var stream = new SizedStream(Enumerable.Repeat((byte)7, actual).ToArray(), reported);
        await Assert.That(async () => { using var input = await BoundedFileInput.ReadAsync(() => stream, maximum); })
            .Throws<InvalidDataException>();
        await Assert.That(stream.Closed).IsTrue();
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(stream.ReadCount == 0).IsEqualTo(reported == 0 || reported > maximum);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancelledLatePhysicalReadStaysOwnedUntilItReturnsOrFaults(bool fault)
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new HeldStream(fault, holdDisposal: false);
        Task<BoundedFileInput> reading = BoundedFileInput.ReadAsync(() => stream, 8, cancellation.Token);
        try
        {
            await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(reading.IsCompleted).IsFalse();
            await Assert.That(stream.Closed).IsFalse();
            await Assert.That(stream.Borrowed.Span[..4].SequenceEqual(new byte[] { 9, 8, 7, 6 })).IsTrue();
        }
        finally { stream.Release.TrySetResult(); }
        if (fault) await Assert.That(async () => { using var input = await reading; }).Throws<IOException>();
        else await Assert.That(async () => { using var input = await reading; }).Throws<OperationCanceledException>();
        await Assert.That(stream.Closed).IsTrue();
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task CancellationDuringPhysicalDisposalRejectsAndClearsOnlyAfterDisposal()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new HeldStream(fault: false, holdDisposal: true);
        Task<BoundedFileInput> reading = BoundedFileInput.ReadAsync(() => stream, 8, cancellation.Token);
        try
        {
            await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(reading.IsCompleted).IsFalse();
            await Assert.That(stream.Borrowed.Span[..4].SequenceEqual(new byte[] { 9, 8, 7, 6 })).IsTrue();
        }
        finally { stream.Release.TrySetResult(); }
        await Assert.That(async () => { using var input = await reading; }).Throws<OperationCanceledException>();
        await Assert.That(stream.Closed).IsTrue();
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task PrecancelledAcquisitionDoesNotOpenNativeInput()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        bool opened = false;
        await Assert.That(async () =>
        {
            using var input = await BoundedFileInput.ReadAsync(() =>
            {
                opened = true;
                return new MemoryStream([1]);
            }, 8, cancellation.Token);
        }).Throws<OperationCanceledException>();
        await Assert.That(opened).IsFalse();
    }

    private sealed class SizedStream(byte[] bytes, long reportedLength) : MemoryStream(bytes)
    {
        public override long Length => reportedLength;
        internal Memory<byte> Borrowed { get; private set; }
        internal bool Closed { get; private set; }
        internal int ReadCount { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (ReadCount++ == 0) Borrowed = buffer;
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 2)], cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Closed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class HeldStream(bool fault, bool holdDisposal) : MemoryStream([9, 8, 7, 6])
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Memory<byte> Borrowed { get; private set; }
        internal bool Closed { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, CancellationToken.None);
            if (count != 0)
            {
                Borrowed = buffer;
                if (!holdDisposal)
                {
                    Entered.TrySetResult();
                    await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                    if (fault) throw new IOException("synthetic-read-failure");
                }
            }
            return count;
        }
        public override async ValueTask DisposeAsync()
        {
            try
            {
                if (holdDisposal)
                {
                    Entered.TrySetResult();
                    await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
            finally
            {
                await base.DisposeAsync();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Closed = true;
            base.Dispose(disposing);
        }
    }
}
