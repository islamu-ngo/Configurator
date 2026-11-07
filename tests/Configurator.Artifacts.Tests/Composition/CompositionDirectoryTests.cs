using System.Runtime.InteropServices;
using System.Text;
using Configurator.Artifacts.Composition;
using Configurator.Core.Composition;

namespace Configurator.Artifacts.Tests.Composition;

public sealed class CompositionDirectoryTests
{
    [Test]
    [Arguments(Architecture.X64, 0x10000, 0x20000)]
    [Arguments(Architecture.Arm64, 0x4000, 0x8000)]
    public async Task DirectoryFlagsMatchExplicitSupportedAbis(Architecture architecture, int directory, int noFollow)
    {
        var flags = LinuxDirectoryInterop.GetOpenFlags(architecture);
        await Assert.That(flags.Directory).IsEqualTo(directory);
        await Assert.That(flags.NoFollow).IsEqualTo(noFollow);
        await Assert.That(Marshal.SizeOf<LinuxDirectoryInterop.NativeIdentity>()).IsEqualTo(256);
    }

    [Test]
    public async Task UnknownAbiAndIncompleteStatxIdentityRefuse()
    {
        await Assert.That(() => LinuxDirectoryInterop.GetOpenFlags(Architecture.X86))
            .Throws<Configurator.Artifacts.Composition.CompositionException>();
        var missing = new LinuxDirectoryInterop.NativeIdentity { Mask = 0x7fe };
        await Assert.That(() => missing.Validate()).Throws<Configurator.Artifacts.Composition.CompositionException>();
        var invalidTimestamp = new LinuxDirectoryInterop.NativeIdentity
        {
            Mask = 0x7ff,
            ModifyNanoseconds = 1_000_000_000
        };
        await Assert.That(() => invalidTimestamp.Validate()).Throws<Configurator.Artifacts.Composition.CompositionException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AggregateYamlEventsIncludeEveryDocumentBoundary(bool exceed)
    {
        using var directory = new DirectoryFixture();
        for (int group = 0; group < 16; group++)
        {
            var yaml = new StringBuilder($"group{group}:\n");
            for (int entry = 0; entry < 2727; entry++)
                yaml.Append("  item").Append(entry).Append(": {}\n");
            yaml.Append("  last: ").Append(exceed && group == 15 ? "{}" : "value").Append('\n');
            File.WriteAllText(Path.Combine(directory.Path, $"{group:D2}.yaml"), yaml.ToString());
        }
        var barrier = new CallbackBarrier(() => { });
        CompositionResult result = await Compile(directory.Path, barrier);
        await Assert.That(result.Succeeded).IsEqualTo(!exceed);
        await Assert.That(result.Failure.Code).IsEqualTo(
            exceed ? CompositionFailureCode.LimitExceeded : CompositionFailureCode.None);
        await Assert.That(barrier.Calls).IsEqualTo(exceed ? 0 : 1);
        if (exceed) await Assert.That(result.SerializedBytes.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SerializationFailurePrecedesMutationAndCancellation(bool cancel)
    {
        using var directory = new DirectoryFixture();
        for (int file = 0; file < 2; file++)
        {
            var json = new StringBuilder("{");
            for (int entry = 0; entry < 8; entry++)
            {
                if (entry != 0) json.Append(',');
                json.Append("\"key").Append(file).Append('_').Append(entry).Append("\":\"")
                    .Append('<', 65000).Append('"');
            }
            json.Append('}');
            File.WriteAllText(Path.Combine(directory.Path, $"{file}.json"), json.ToString());
        }
        using var cancellation = new CancellationTokenSource();
        var barrier = new CallbackBarrier(() =>
        {
            File.WriteAllText(Path.Combine(directory.Path, "added.json"), "{}");
            if (cancel) cancellation.Cancel();
        });
        CompositionResult result = await Compile(directory.Path, barrier, cancellation.Token);
        await Assert.That(result.Failure.Code).IsEqualTo(CompositionFailureCode.LimitExceeded);
        await Assert.That(result.SerializedBytes.IsEmpty).IsTrue();
        await Assert.That(barrier.Calls).IsEqualTo(0);
    }

    [Test]
    [Arguments("add")]
    [Arguments("remove")]
    [Arguments("replace")]
    [Arguments("retarget")]
    [Arguments("link")]
    public async Task RealDirectoryMutationAtBarrierRejectsCandidate(string mutation)
    {
        using var directory = new DirectoryFixture();
        string file = Path.Combine(directory.Path, "a.json");
        File.WriteAllText(file, "{\"a\":1}");
        var barrier = new CallbackBarrier(() =>
        {
            switch (mutation)
            {
                case "add": File.WriteAllText(Path.Combine(directory.Path, "b.json"), "{}"); break;
                case "remove": File.Delete(file); break;
                case "replace":
                    File.Move(file, Path.Combine(directory.Path, "retained.json"));
                    File.WriteAllText(file, "{\"a\":2}");
                    break;
                case "retarget":
                    Directory.Move(directory.Path, directory.Path + "-retained");
                    Directory.CreateDirectory(directory.Path);
                    File.WriteAllText(file, "{\"a\":1}");
                    break;
                case "link":
                    File.Delete(file);
                    File.CreateSymbolicLink(file, "missing.json");
                    break;
            }
        });
        CompositionResult result = await Compile(directory.Path, barrier);
        await Assert.That(result.Failure.Code).IsEqualTo(CompositionFailureCode.SourceChanged);
        await Assert.That(result.SerializedBytes.IsEmpty).IsTrue();
        await Assert.That(barrier.Calls).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ObservedCancellationWinsBeforeRecheckAndClearsOutput(bool mutate)
    {
        using var directory = new DirectoryFixture();
        File.WriteAllText(Path.Combine(directory.Path, "a.json"), "{\"a\":1}");
        using var cancellation = new CancellationTokenSource();
        var barrier = new CallbackBarrier(() =>
        {
            if (mutate) File.Delete(Path.Combine(directory.Path, "a.json"));
            cancellation.Cancel();
        });
        CompositionResult result = await Compile(directory.Path, barrier, cancellation.Token);
        await Assert.That(result.Failure.Code).IsEqualTo(CompositionFailureCode.Cancelled);
        await Assert.That(result.SerializedBytes.IsEmpty).IsTrue();
        await Assert.That(barrier.Calls).IsEqualTo(1);
    }

    [Test]
    [Arguments("symlink")]
    [Arguments("hardlink")]
    [Arguments("fifo")]
    [Arguments("hidden")]
    [Arguments("collision")]
    public async Task UnsafeRealEntriesRefuseWithoutBarrier(string entry)
    {
        using var directory = new DirectoryFixture();
        string file = Path.Combine(directory.Path, "a.json");
        File.WriteAllText(file, "{}");
        switch (entry)
        {
            case "symlink":
                File.CreateSymbolicLink(Path.Combine(directory.Path, "b.json"), "a.json");
                break;
            case "hardlink":
                if (Link(Encoding.UTF8.GetBytes(file + '\0'),
                    Encoding.UTF8.GetBytes(Path.Combine(directory.Path, "b.json") + '\0')) != 0)
                    throw new IOException("native-link-fixture");
                break;
            case "fifo":
                if (MakeFifo(Encoding.UTF8.GetBytes(Path.Combine(directory.Path, "b.json") + '\0'), 0x180) != 0)
                    throw new IOException("native-fifo-fixture");
                break;
            case "hidden":
                File.WriteAllText(Path.Combine(directory.Path, ".hidden.json"), "{}");
                break;
            case "collision":
                File.WriteAllText(Path.Combine(directory.Path, "A.json"), "{}");
                break;
        }
        var barrier = new CallbackBarrier(() => { });
        CompositionResult result = await Compile(directory.Path, barrier);
        await Assert.That(result.Failure.Code).IsEqualTo(entry switch
        {
            "hidden" => CompositionFailureCode.UnsafePath,
            "collision" => CompositionFailureCode.PathCollision,
            _ => CompositionFailureCode.UnsafeEntry
        });
        await Assert.That(result.SerializedBytes.IsEmpty).IsTrue();
        await Assert.That(barrier.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task InvalidParseAndEntryCancellationNeverInvokeBarrier()
    {
        using var directory = new DirectoryFixture();
        File.WriteAllText(Path.Combine(directory.Path, "a.json"), "[]");
        var barrier = new CallbackBarrier(() => { });
        CompositionResult invalid = await Compile(directory.Path, barrier);
        await Assert.That(invalid.Failure.Code).IsEqualTo(CompositionFailureCode.InvalidDocument);
        await Assert.That(invalid.SerializedBytes.IsEmpty).IsTrue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        CompositionResult cancelled = await Compile(directory.Path, barrier, cancellation.Token);
        await Assert.That(cancelled.Failure.Code).IsEqualTo(CompositionFailureCode.Cancelled);
        await Assert.That(cancelled.SerializedBytes.IsEmpty).IsTrue();
        await Assert.That(barrier.Calls).IsEqualTo(0);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(byte[] source, byte[] target);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeFifo(byte[] path, uint mode);

    private static ValueTask<CompositionResult> Compile(
        string root, CallbackBarrier barrier, CancellationToken cancellationToken = default) =>
        new CompositionDirectoryCompiler().CompileAsync(root, barrier, cancellationToken);

    private sealed class CallbackBarrier(Action callback) : ICompositionPublicationCommitBarrier
    {
        internal int Calls { get; private set; }
        public ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken)
        {
            Calls++;
            callback();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DirectoryFixture : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "composition-" + Guid.NewGuid().ToString("N"));

        internal DirectoryFixture()
        {
            if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("actual-linux-required");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
            if (Directory.Exists(Path + "-retained"))
                Directory.Delete(Path + "-retained", recursive: true);
        }
    }
}
