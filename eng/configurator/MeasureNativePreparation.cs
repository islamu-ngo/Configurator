using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
using Avalonia;
using Avalonia.Threading;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
#else
using Configurator.Terminal;
using global::Terminal.Gui.ViewBase;
#endif
#else
return NativePreparationFixture.Run(args);
#endif

#if CONFIGURATOR_NATIVE_IMPORT_PROBE
namespace Configurator.Engineering;
#endif
    /// <summary>Fixed public synthetic inputs; this tool never accepts operator answers.</summary>
    internal static class NativePreparationFixture
    {
        internal static byte[] Create()
        {
            string payload = new('x', 65536);
            return Encoding.UTF8.GetBytes("""
                {
                  "schemaVersion":"0.0.1","profile":"bounded-v0.0.1",
                  "familyId":"beff1b0f-970a-4cdf-a2de-d090c626184c","revision":1,
                  "project":{"id":"phase4.synthetic","name":"Phase4 synthetic","publisher":"first-party diagnostic"},
                  "applicationVersions":["1.0"],"entryPoints":["setup"],"locales":["ar","en"],
                  "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl"}}},
                  "requiredCapabilities":["projection.copy","projection.object","schema.object","schema.string","values.initial"],
                  "configuration":{"type":"object","additionalProperties":false,
                    "x-configurator":{"sensitivity":"public"},
                    "properties":{"payload":{"type":"string","title":"Synthetic payload",
                      "x-configurator":{"sensitivity":"public","initialValue":
                """ + JsonSerializer.Serialize(payload) + """
                }}}},
                  "artifacts":[{"id":"config","format":"json","fileName":"phase4.json",
                    "targetUpdateMode":"create","restartRequirement":"none",
                    "projection":{"op":"object","members":{
                      "a":{"op":"copy","reference":{"scope":"root","path":"/payload"}},
                      "b":{"op":"copy","reference":{"scope":"root","path":"/payload"}},
                      "c":{"op":"copy","reference":{"scope":"root","path":"/payload"}},
                      "d":{"op":"copy","reference":{"scope":"root","path":"/payload"}}
                    }}}]
                }
                """);
        }

        internal static byte[] CreatePartialSave()
        {
            var source = System.Text.Json.Nodes.JsonNode.Parse(Create())!.AsObject();
            source["familyId"] = "12783fea-739b-438c-a41f-90574f4fae72";
            source["project"]!["id"] = "phase4.partial.synthetic";
            var first = source["artifacts"]![0]!.DeepClone();
            first["id"] = "first";
            first["fileName"] = "phase4-first.json";
            var second = first.DeepClone();
            second["id"] = "second";
            second["fileName"] = "phase4-second.json";
            source["artifacts"] = new System.Text.Json.Nodes.JsonArray(first, second);
            return JsonSerializer.SerializeToUtf8Bytes(source);
        }

#if !CONFIGURATOR_NATIVE_IMPORT_PROBE
        private static readonly string[] CopyMemberNames = ["a", "b", "c", "d"];

        internal static int Run(string[] arguments)
        {
            if (arguments is ["--self-test-partial-save"])
            {
                using JsonDocument partial = JsonDocument.Parse(CreatePartialSave());
                JsonElement source = partial.RootElement;
                JsonElement artifacts = source.GetProperty("artifacts");
                bool valid = source.GetProperty("familyId").GetGuid() == new Guid("12783fea-739b-438c-a41f-90574f4fae72")
                    && source.GetProperty("configuration").GetProperty("properties").GetProperty("payload")
                        .GetProperty("x-configurator").GetProperty("initialValue").GetString() == new string('x', 65536)
                    && artifacts.GetArrayLength() == 2
                    && artifacts[0].GetProperty("id").GetString() == "first"
                    && artifacts[1].GetProperty("id").GetString() == "second"
                    && artifacts[0].GetProperty("fileName").GetString() == "phase4-first.json"
                    && artifacts[1].GetProperty("fileName").GetString() == "phase4-second.json"
                    && artifacts.EnumerateArray().All(artifact =>
                    {
                        JsonElement members = artifact.GetProperty("projection").GetProperty("members");
                        return members.EnumerateObject().Select(member => member.Name).SequenceEqual(CopyMemberNames)
                            && members.EnumerateObject().All(member => member.Value.GetProperty("op").GetString() == "copy"
                                && member.Value.GetProperty("reference").GetProperty("scope").GetString() == "root"
                                && member.Value.GetProperty("reference").GetProperty("path").GetString() == "/payload");
                    })
                    && CreatePartialSave().SequenceEqual(CreatePartialSave())
                    && !Create().SequenceEqual(CreatePartialSave());
                Console.WriteLine(valid ? "phase4-partial-fixture:self-test-passed" : "phase4-partial-fixture:self-test-failed");
                return valid ? 0 : 1;
            }
            byte[] bytes = Create();
            if (arguments is ["--self-test"])
            {
                using JsonDocument document = JsonDocument.Parse(bytes);
                bool valid = document.RootElement.GetProperty("configuration").GetProperty("properties")
                    .GetProperty("payload").GetProperty("x-configurator").GetProperty("initialValue").GetString()!.Length == 65536
                    && SHA256.HashData(bytes).SequenceEqual(SHA256.HashData(Create()));
                Console.WriteLine(valid ? "phase4-fixture:self-test-passed" : "phase4-fixture:self-test-failed");
                return valid ? 0 : 1;
            }
            if (arguments is ["--write-partial-save-fixture", var partialPath])
            {
                bytes = CreatePartialSave();
                arguments = ["--write-fixture", partialPath];
            }
            if (arguments is not ["--write-fixture", var path]) return 64;
            try
            {
                using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(bytes);
                Console.WriteLine(Convert.ToHexStringLower(SHA256.HashData(bytes)));
                return 0;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("phase4-fixture:write-refused");
                return 1;
            }
        }
#endif
    }

#if CONFIGURATOR_NATIVE_IMPORT_PROBE
    /// <summary>Opted-in actual native observations; timestamps are not normal-package diagnostics.</summary>
    internal static class MeasureNativePreparation
    {
        private static readonly object Gate = new();
        private static readonly List<object> Samples = [];
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private static readonly List<IDisposable> Subscriptions = [];
        private static object? _root;
        private static List<Stamp>? _stamps;
        private static TaskCompletionSource? _worker;
        private static CancellationTokenSource? _requested;
        private static int _workerThread;
        private static bool _overflow;
        private static string _stage = "startup";
        private static string? _destination;
#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
        private static TerminalPaintObservation? _terminalPaint;
        private static double? _inheritedLoopCadence;
        private static double? _defaultLoopCadence;

        internal static void ObserveLoopCadence(double inherited, double frameworkDefault)
        {
            _inheritedLoopCadence = inherited;
            _defaultLoopCadence = frameworkDefault;
        }
#endif

        internal static CancellationToken? RequestedToken => _requested?.Token;
        internal static void CancelRequestedToken() => _requested?.Cancel();

        internal static void Mark(string stage)
        {
            lock (Gate)
            {
                if (_stamps is null) return;
                if (_stamps.Count == 128) { _overflow = true; return; }
                long pauseBefore = GC.GetTotalPauseDuration().Ticks;
                var collectionsBefore = new CollectionCounts(
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                long timestamp = Stopwatch.GetTimestamp();
                var collectionsAfter = new CollectionCounts(
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                long pauseAfter = GC.GetTotalPauseDuration().Ticks;
                _stamps.Add(new(stage, timestamp, Environment.CurrentManagedThreadId,
                    GC.GetAllocatedBytesForCurrentThread(),
                    pauseBefore, pauseAfter, collectionsBefore, collectionsAfter));
                if (stage == "worker-start")
                {
                    _workerThread = Environment.CurrentManagedThreadId;
                    _worker?.TrySetResult();
                }
            }
        }

        internal static void Attach(object root)
        {
            _root = root;
            // The observer must not queue its own continuations behind the UI work it measures.
            _ = Task.Run(RunAsync);
        }

        private static async Task RunAsync()
        {
            string? output = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) { Environment.Exit(64); return; }
            string scenario = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PREPARATION_SCENARIO") ?? "prepare";
            string gcLatencyModeAtStart = System.Runtime.GCSettings.LatencyMode.ToString();
            object? error = null;
            byte[] expectedArtifact = ExpectedArtifact();
            byte[] actualArtifact = new byte[expectedArtifact.Length];
            try
            {
                if (scenario is not ("prepare" or "save" or "cancel-prepare" or "cancel-save"))
                    throw new InvalidOperationException();
                string locale = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_UI_CULTURE") ?? "en";
                if (locale is not ("en" or "ar")) throw new InvalidOperationException();
                System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(locale);
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.CurrentUICulture;
                byte[] fixture = NativePreparationFixture.Create();
                Definition definition = DefinitionCodec.ParseJson(fixture);
                await UiAsync(() =>
                {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                    if (((MainWindow)_root!).TryGetPlatformHandle()?.HandleDescriptor != "XID")
                        throw new InvalidOperationException();
                    if (!Shell.Loader.LoadDefinition(fixture)) throw new InvalidOperationException();
                    Shell.Publisher.ArtifactId = "config";
#else
                    if (Form.Definition.Digest != definition.Digest) throw new InvalidOperationException();
                    Window.SelectProbeArtifact("config");
#endif
                    if (!Form.SelectLocale(locale)) throw new InvalidOperationException();
                }).ConfigureAwait(false);
                Subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(new ListenerObserver()));
                // A distinct process/scenario retains first use and every warmup/measured attempt.
                for (int index = 0; index < 46; index++)
                {
                    _stage = "setup";
                    await UiAsync(() =>
                    {
                        if (!Form.SetValue(["payload"], JsonSerializer.SerializeToElement(new string('x', 65536))))
                            throw new InvalidOperationException();
                        string directory = Path.Combine(Environment.CurrentDirectory, "phase4-" + scenario + "-" + index);
                        if (Directory.Exists(directory)) throw new InvalidOperationException();
                        Directory.CreateDirectory(directory);
                        _destination = directory;
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                        Shell.Publisher.OutputDirectory = directory;
#else
                        Window.ProbeOutputDirectory = directory;
#endif
                    }).ConfigureAwait(false);
                    object? setupObservation = null;
                    if (scenario is "save" or "cancel-save")
                    {
                        Task prepared = Task.CompletedTask;
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                        await UiAsync(() => prepared = Prepare()).ConfigureAwait(false);
                        await prepared.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                        await UiAsync(() => { if (!Prepared) throw new InvalidOperationException(); }).ConfigureAwait(false);
#else
                        TerminalPaintObservation? setupPaint = null;
                        try
                        {
                            await UiAsync(() =>
                            {
                                setupPaint = new TerminalPaintObservation(Window, -index - 1);
                                prepared = Prepare();
                            }).ConfigureAwait(false);
                            await prepared.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                            Task setupCycle = Task.CompletedTask;
                            await UiAsync(() =>
                            {
                                if (!Prepared) throw new InvalidOperationException();
                                setupCycle = setupPaint!.ObserveLatest("setup-ready-frame");
                            }).ConfigureAwait(false);
                            await setupCycle.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                        }
                        finally
                        {
                            setupPaint?.Dispose();
                            setupObservation = setupPaint?.Snapshot();
                        }
#endif
                    }
                    await PaintAsync().ConfigureAwait(false);
                    await ObserveAsync(scenario, index, setupObservation, expectedArtifact, actualArtifact).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                error = new { code = "observation-failed", type = exception.GetType().Name, stage = _stage };
            }
            finally
            {
                foreach (IDisposable subscription in Subscriptions) subscription.Dispose();
                _requested?.Dispose();
            }
            var host = new
            {
                os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                runtimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                processors = Environment.ProcessorCount,
                locale = System.Globalization.CultureInfo.CurrentUICulture.Name,
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                surface = "X11-XID"
#else
                surface = "PTY-required-by-entrypoint",
                mainLoop = new
                {
                    inheritedMaximumIterationsPerSecond = _inheritedLoopCadence,
                    frameworkDefaultMaximumIterationsPerSecond = _defaultLoopCadence,
                    configuredMaximumIterationsPerSecond = global::Terminal.Gui.App.Application.MaximumIterationsPerSecond
                }
#endif
            };
            var receipt = new
            {
                schemaVersion = "0.0.1",
                diagnosticCandidate = true,
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                observerRecipe = "off-owner-prearmed-feedback-gc-brackets-core-stages",
#else
                observerRecipe = "terminal-passive-status-draw-cycle-deferred-sentinel-v1",
#endif
                scenario,
                fixtureSha256 = Convert.ToHexStringLower(SHA256.HashData(NativePreparationFixture.Create())),
                expectedArtifactSha256 = Convert.ToHexStringLower(SHA256.HashData(expectedArtifact)),
                candidateAssemblies = new[]
                {
                    typeof(MeasureNativePreparation).Assembly, typeof(DefinitionArtifactComposer).Assembly,
                    typeof(DefinitionFormModel).Assembly, typeof(ProtectedArtifactWriter).Assembly
                }.Select(assembly => new
                {
                    name = assembly.GetName().Name,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location)))
                }).ToArray(),
                host,
                hostSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(host))),
                stopwatchFrequency = Stopwatch.Frequency,
                gc = new
                {
                    serverGC = System.Runtime.GCSettings.IsServerGC,
                    latencyModeAtStart = gcLatencyModeAtStart,
                    latencyModeAtReceipt = System.Runtime.GCSettings.LatencyMode.ToString(),
                    pauseCounter = "GC.GetTotalPauseDuration; cumulative runtime-reported process GC suspension time",
                    pauseUnit = "TimeSpan ticks; 100 ns; distinct from Stopwatch ticks",
                    readOrder = "pause-before, generation counts before, Stopwatch timestamp, generation counts after, pause-after",
                    intervalPauseLowerBound = "max(0, end.GcPauseBeforeTimeSpanTicks - start.GcPauseAfterTimeSpanTicks)",
                    intervalPauseUpperBound = "max(0, end.GcPauseAfterTimeSpanTicks - start.GcPauseBeforeTimeSpanTicks)",
                    intervalCollectionBounds = "per generation: max(0, end.GcCollectionsBefore - start.GcCollectionsAfter) through max(0, end.GcCollectionsAfter - start.GcCollectionsBefore); do not sum generations",
                    interpretation = "bounds describe increases in process-wide reported counters across timestamp brackets; runtime reporting can lag actual suspension, and these are not exact overlapping pause intervals or allocation-source attribution",
                    remainder = "wall time minus reported suspension is not product CPU time; scheduling, JIT, lock waits, concurrent GC work and host interference remain unseparated",
                    instrumentation = "two pause reads, six collection-count reads and one current-thread allocation read per stamp; no collection is forced and no GC setting is changed",
                    allocationCounter = "GC.GetAllocatedBytesForCurrentThread is cumulative for the recorded managed thread; subtract only same-thread endpoints; callback gaps are not allocator stack or CPU attribution"
                },
                stageSemantics = new
                {
                    validation = "ValidationStarted to ValidationCompleted brackets real validation with scopeObservation null; owner validation separately bracketed; ValidationCompleted precedes validation-result construction",
                    validationSetup = "worker-start to ValidationStarted includes caller/composer setup before real validation",
                    validationResultRemainder = "ValidationCompleted to ProjectionStarted includes validation-result construction, caller return, artifact eligibility and projection setup; these cannot be separately attributed from the current events",
                    projection = "ProjectionStarted to ProjectionCompleted brackets Project and its argument lookup; omitted projection can complete without serialization",
                    serialization = "SerializationStarted to SerializationCompleted brackets intermediate JSON creation/parsing, canonicalization, format serialization, draft prefix and final size/cancellation checks; excludes result construction and cleanup",
                    historicalCombinedIntervals = "historical worker-start to ValidationCompleted combines setup and validation; ValidationCompleted to ArtifactComposed combines result construction, caller/projection setup, projection and serialization",
                    artifactRemainder = "ArtifactComposed to composition-end includes artifact/result construction, byte clearing, JSON disposal and bundle accounting; existing ArtifactComposed semantics unchanged",
                    missingCompletions = "failure or early return may leave a start without completion; never infer or substitute a missing stage endpoint",
                    feedback = "queue sentinel and scene observation armed on-owner before operation-trigger; feedback-owner timestamps sentinel entry; neither proves physical presentation",
                    feedbackQueue = "feedback-request to feedback-owner includes the command turn and queued owner work, without waiting for worker completion",
                    feedbackScene = "Desktop uses two animation callbacks; Terminal feedback-frame is the first application completion following an actual correlated status draw, which may show a result after coalescing progress; sentinel and draw ordering are independent",
#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
                    terminalDispatch = "all diagnostic UI actions use one-shot AddTimeout(TimeSpan.Zero) callbacks on the owner loop, including calls made while already on-owner; feedback includes owner-loop timeout cadence",
                    terminalPaint = "numeric operation/mutation/owner-generation identities only; progress/result categories contain no text; superseded mutations without an application-completed draw are coalesced; result-frame/cancel-frame records reference an already completed matching cycle when available and are observer-observation timestamps, not new frames; no forced invalidation or drawing",
                    terminalSetup = "save scenarios await the actual prepared-status draw/application cycle before starting the first measured save; setupObservation records that prerequisite without invoking or warming Save; all first/warmup/measured save attempts remain",
                    terminalRootDraw = "numbered root View.DrawComplete callbacks localize framework drawing order relative to status and application completion; they do not by themselves establish descendant completion or terminal presentation",
#endif
                    artifactOracle = "one independent literal expectation and one equal-sized read buffer allocated before all scenarios; each save compares the complete file length and bytes after measured stages, without regenerating large oracle buffers between samples",
                    cancellation = "Desktop uses Shell.Cancel; Terminal preparation uses the diagnostic requested token and existing invalidation; Terminal save requests workspace cancellation",
                    cancellationWindow = "observed-pending describes the awaited completion task at cancel-owner-enter, which can include owner processing after physical settlement; compare physical-settled with cancel-owner-enter separately before claiming interruption of active work",
                    cleanup = "consumer-end to physical-settled includes cleanup and settlement bookkeeping; exceptions can omit consumer-end",
                    settlementObservation = "physical-settled is the native hook; physical-observed is the later observer continuation after feedback and is not a physical completion timestamp",
                    close = "not driven; parent NativeHost/PTY close journeys remain separate"
                },
                samples = Samples,
                executionComplete = error is null && Samples.Count == 46,
                timingAcceptance = "not-evaluated",
                error
            };
            try
            {
                using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, receipt, JsonOptions).ConfigureAwait(false);
            }
            catch (Exception) { Environment.Exit(1); return; }
            Environment.Exit(error is null ? 0 : 1);
        }

        private static async Task ObserveAsync(string scenario, int index, object? setupObservation,
            byte[] expectedArtifact, byte[] actualArtifact)
        {
            _requested = new CancellationTokenSource();
            _worker = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (Gate) { _stamps = []; _overflow = false; _workerThread = 0; }
            _stage = "operation";
            Task operation = Task.CompletedTask;
            Task physical = Task.CompletedTask;
            Task feedback = Task.CompletedTask;
            bool cancelPending = false;
            bool ready = false;
            object? publication = null;
            bool completed = false;
            bool? artifactMatches = null;
#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
            object? statusObservation = null;
#endif
            try
            {
                Mark("request");
                await UiAsync(() =>
                {
                    Mark("owner-enter");
#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
                    _terminalPaint?.Dispose();
                    _terminalPaint = new TerminalPaintObservation(Window, index);
#endif
                    feedback = ArmFeedback();
                    Mark("operation-trigger");
                    operation = scenario is "save" or "cancel-save" ? Save() : Prepare();
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                    physical = Shell.ActiveCompletion;
#else
                    physical = operation;
#endif
                    Mark("owner-return");
                }).ConfigureAwait(false);
                if (scenario.StartsWith("cancel-", StringComparison.Ordinal))
                {
                    await Task.WhenAny(_worker.Task, operation).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                    Mark("cancel-request");
                    await UiAsync(() =>
                    {
                        cancelPending = !physical.IsCompleted;
                        Mark("cancel-owner-enter");
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                        Shell.Cancel();
#else
                        if (scenario == "cancel-save") _ = Window.CancelProbeSave();
                        else Window.CancelProbePreparation();
#endif
                        Mark("cancel-owner-return");
                    }).ConfigureAwait(false);
                    await PaintAsync("cancel-frame").ConfigureAwait(false);
                }
                await feedback.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                try { await operation.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
                catch (OperationCanceledException) when (scenario.StartsWith("cancel-", StringComparison.Ordinal)) { }
                await physical.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                Mark("physical-observed");
                await UiAsync(() =>
                {
                    ready = Prepared;
                    if (scenario is "prepare" && !ready) throw new InvalidOperationException();
                    if (cancelPending && ready) throw new InvalidOperationException();
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                    ProtectedArtifactPublicationReceipt? receipt = Shell.Publisher.LatestPublication;
#else
                    ProtectedArtifactPublicationReceipt? receipt = Window.ProbeReceipt;
#endif
                    if (scenario is "save" or "cancel-save")
                    {
                        if (receipt is null || !receipt.IsSettled) throw new InvalidOperationException();
                        publication = new
                        {
                            settled = receipt.IsSettled, unknown = receipt.IsUnknown, failed = receipt.Failed,
                            cleanupFailed = receipt.CleanupFailed, completion = receipt.CompletionCode?.ToString(),
                            outcomes = receipt.Outcomes.Select(item => new
                            {
                                attempted = item.Result is not null, status = item.Result?.Status.ToString(),
                                installed = item.Result?.InstallationEstablished,
                                verified = item.Result?.VerificationSucceeded
                            }).ToArray()
                        };
                        if (scenario == "save" && (receipt.IsUnknown || receipt.Failed ||
                            receipt.Outcomes.Any(item => item.Result is not { InstallationEstablished: true, VerificationSucceeded: true })))
                            throw new InvalidOperationException();
                    }
                }).ConfigureAwait(false);
                await PaintAsync("result-frame").ConfigureAwait(false);
                if (_overflow) throw new InvalidOperationException();
                if (scenario == "save")
                {
                    // Independent literal expectation; outside the measured application stages.
                    await using var actual = new FileStream(Path.Combine(_destination!, "phase4.json"),
                        FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
                    artifactMatches = actual.Length == expectedArtifact.Length;
                    if (artifactMatches == true)
                    {
                        await actual.ReadExactlyAsync(actualArtifact).ConfigureAwait(false);
                        artifactMatches = actualArtifact.SequenceEqual(expectedArtifact);
                    }
                    if (artifactMatches != true) throw new InvalidOperationException();
                }
                completed = true;
            }
            finally
            {
#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
                if (_terminalPaint is { } paint)
                {
                    paint.Dispose();
                    statusObservation = paint.Snapshot();
                    _terminalPaint = null;
                }
#endif
                Stamp[] events;
                lock (Gate) { events = _stamps!.ToArray(); _stamps = null; }
                Samples.Add(new
                {
                    sample = index, phase = index == 0 ? "first" : index <= 5 ? "warmup" : "measured",
                    scenario, cancellationWindow = scenario.StartsWith("cancel-", StringComparison.Ordinal)
                        ? cancelPending ? "observed-pending" : "missed" : "not-requested",
                    preparedAfter = ready, publication, artifactMatches, completed, overflow = _overflow, events,
                    setupObservation,
#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
                    statusObservation,
#endif
                });
                _requested.Dispose();
                _requested = null;
            }
        }

        /// <summary>Registers owner and scene callbacks in the command turn before starting native work.</summary>
        private static Task ArmFeedback()
        {
            Mark("feedback-request");
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            Task queued = UiAsync(() => Mark("feedback-owner"));
            var scene = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ((MainWindow)_root!).RequestAnimationFrame(firstFrame =>
            {
                Mark("feedback-first-animation-callback");
                ((MainWindow)_root!).RequestAnimationFrame(secondFrame =>
                {
                    Mark("feedback-frame");
                    scene.TrySetResult();
                });
            });
            Task feedback = Task.WhenAll(queued, scene.Task);
#else
            Task feedback = Task.WhenAll(UiAsync(() => Mark("feedback-owner")), _terminalPaint!.Feedback);
#endif
            Mark("feedback-scene-armed");
            return feedback;
        }

        private static byte[] ExpectedArtifact()
        {
            string value = new('x', 65536);
            return Encoding.UTF8.GetBytes("{\"a\":\"" + value + "\",\"b\":\"" + value +
                "\",\"c\":\"" + value + "\",\"d\":\"" + value + "\"}");
        }

#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        private static Task<DesktopPreparationOutcome> Prepare()
        {
            return Shell.Publisher.PrepareBundleAsync(_requested?.Token ?? CancellationToken.None);
#else
        private static Task Prepare()
        {
            return Window.StartProbePreparation();
#endif
        }

#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        private static Task<ProtectedArtifactBundleResult?> Save()
        {
            return Shell.Publisher.SaveBundleAsync();
#else
        private static Task Save()
        {
            return Window.StartProbeSave();
#endif
        }

        private static Task UiAsync(Action action)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Run()
            {
                try { action(); done.TrySetResult(); }
                catch (Exception exception) { done.TrySetException(exception); }
            }
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            Dispatcher.UIThread.Post(Run);
#else
            Window.ProbeApplication.AddTimeout(TimeSpan.Zero, () =>
            {
                Run();
                return false;
            });
#endif
            return done.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }

#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
        /// <summary>Observes product status mutations and natural drawing without invalidating any view.</summary>
        internal sealed class TerminalPaintObservation : IDisposable
        {
            private readonly object _gate = new();
            private readonly TerminalWindow _window;
            private readonly int _operation;
            private readonly int _generation;
            private readonly int _ownerThread = Environment.CurrentManagedThreadId;
            private readonly List<Mutation> _mutations = [];
            private readonly View[] _children;
            private readonly List<(int Mutation, int Child, long Ticks, long ThreadAllocatedBytes)> _childDraws = [];
            private bool _childOverflow;
            private readonly TaskCompletionSource _feedback = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private Mutation? _pendingDraw;
            private bool _retired;
            private int? _feedbackMutation;
            private Func<bool>? _currentResult;
            private TaskCompletionSource? _resultCycle;

            internal TerminalPaintObservation(TerminalWindow window, int operation)
            {
                _window = window;
                _operation = operation;
                _generation = window.ProbeOwnerGeneration;
                _children = window.SubViews.ToArray();
                foreach (View child in _children) child.DrawComplete += ChildDrawn;
                window.ProbeStatusMutation += Mutated;
                window.ProbeStatusView.DrawComplete += Drawn;
                window.DrawComplete += RootDrawn;
                window.ProbeApplication.LayoutAndDrawComplete += CycleCompleted;
                window.Disposing += Disposing;
            }

            internal Task Feedback => _feedback.Task;

            private bool IsCurrent => !_retired && _window.ProbeOwnerGeneration == _generation
                && Environment.CurrentManagedThreadId == _ownerThread;

            private void Mutated(int sequence, int generation, bool progress)
            {
                lock (_gate)
                {
                    if (!IsCurrent || generation != _generation) { Dispose(); return; }
                    if (_mutations.LastOrDefault() is { Cycle: false } previous)
                    {
                        previous.Coalesced = true;
                        previous.Completion.TrySetCanceled();
                        Mark("status-coalesced-" + previous.Sequence);
                    }
                    var mutation = new Mutation(sequence, progress);
                    _mutations.Add(mutation);
                    _pendingDraw = null;
                    Mark("status-mutation-" + sequence);
                }
            }

            private void Drawn(object? sender, DrawEventArgs args)
            {
                lock (_gate)
                {
                    if (!IsCurrent) { Dispose(); return; }
                    if (_mutations.LastOrDefault() is not { Drawn: false } mutation) return;
                    mutation.Drawn = true;
                    _pendingDraw = mutation;
                    Mark("status-draw-" + mutation.Sequence);
                }
            }

            private void RootDrawn(object? sender, DrawEventArgs args)
            {
                lock (_gate)
                {
                    if (!IsCurrent) { Dispose(); return; }
                    if (_mutations.LastOrDefault() is not { } mutation) return;
                    Mark("root-draw-" + mutation.Sequence + "-" + ++mutation.RootDraws);
                }
            }

            private void ChildDrawn(object? sender, DrawEventArgs args)
            {
                lock (_gate)
                {
                    if (!IsCurrent || _mutations.LastOrDefault() is not { } mutation) return;
                    if (_childDraws.Count == 512) { _childOverflow = true; return; }
                    _childDraws.Add((mutation.Sequence, Array.IndexOf(_children, sender), Stopwatch.GetTimestamp(),
                        GC.GetAllocatedBytesForCurrentThread()));
                }
            }

            private void CycleCompleted(object? sender, EventArgs args)
            {
                lock (_gate)
                {
                    if (!IsCurrent) { Dispose(); return; }
                    if (_pendingDraw is not { } mutation) return;
                    _pendingDraw = null;
                    mutation.Cycle = true;
                    mutation.CycleTicks = Stopwatch.GetTimestamp();
                    Mark("status-cycle-" + mutation.Sequence);
                    foreach (string observation in mutation.Observations) Mark(observation);
                    mutation.Completion.TrySetResult();
                    if (!mutation.Progress && _currentResult?.Invoke() == true) _resultCycle!.TrySetResult();
                    if (_feedbackMutation is null)
                    {
                        _feedbackMutation = mutation.Sequence;
                        Mark("feedback-frame");
                        _feedback.TrySetResult();
                    }
                }
            }

            internal Task ObserveLatest(string stage)
            {
                lock (_gate)
                {
                    if (!IsCurrent) throw new InvalidOperationException("status-observation-retired");
                    if (_mutations.LastOrDefault() is not { } mutation)
                        throw new InvalidOperationException("status-mutation-missing");
                    mutation.Observations.Add(stage);
                    if (mutation.Cycle) Mark(stage);
                    return mutation.Completion.Task;
                }
            }

            internal bool CurrentResultCycleObserved
            {
                get
                {
                    lock (_gate)
                        return IsCurrent && _mutations.LastOrDefault() is { Progress: false, Cycle: true, Coalesced: false };
                }
            }

            internal Task ObserveCurrentResult(Func<bool> currentResult)
            {
                lock (_gate)
                {
                    if (!IsCurrent) throw new InvalidOperationException("status-observation-retired");
                    _currentResult = currentResult;
                    _resultCycle ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (_mutations.LastOrDefault() is { Progress: false, Cycle: true, Coalesced: false }
                        && currentResult()) _resultCycle.TrySetResult();
                    return _resultCycle.Task;
                }
            }

            internal object Snapshot()
            {
                lock (_gate)
                    return new
                    {
                        operation = _operation, ownerGeneration = _generation, ownerThread = _ownerThread,
                        retired = _retired, feedbackMutation = _feedbackMutation,
                        childOverflow = _childOverflow,
                        childTypes = _children.Select(child => child.GetType().Name).ToArray(),
                        childDraws = _childDraws.Select(draw => new
                        {
                            mutation = draw.Mutation, child = draw.Child, ticks = draw.Ticks,
                            threadAllocatedBytes = draw.ThreadAllocatedBytes
                        }).ToArray(),
                        mutations = _mutations.Select(mutation => new
                        {
                            mutation.Sequence, kind = mutation.Progress ? "progress" : "result",
                            mutation.Drawn, mutation.Cycle, mutation.CycleTicks, mutation.Coalesced, mutation.RootDraws,
                            missing = !mutation.Cycle && !mutation.Coalesced,
                            observations = mutation.Observations.ToArray()
                        }).ToArray()
                    };
            }

            internal bool ProvesNaturalCycle(bool coalescing, Stamp[] events, int ownerThread)
            {
                lock (_gate)
                {
                    if (_mutations.Count != (coalescing ? 2 : 1)) return false;
                    Mutation last = _mutations[^1];
                    if (!last.Cycle || last.Progress || last.Coalesced) return false;
                    if (coalescing && !(_mutations[0] is { Progress: true, Drawn: false, Cycle: false, Coalesced: true }))
                        return false;
                    int previous = Array.FindIndex(events, stamp => stamp.Stage == "operation-trigger");
                    foreach (string stage in new[] { "status-mutation-", "status-draw-", "status-cycle-" })
                    {
                        int position = Array.FindIndex(events, stamp => stamp.Stage == stage + last.Sequence);
                        if (position <= previous || events[position].Thread != ownerThread) return false;
                        previous = position;
                    }
                    return _feedbackMutation == last.Sequence;
                }
            }

            private void Disposing(object? sender, EventArgs args) => Dispose();

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_retired) return;
                    _retired = true;
                    foreach (View child in _children) child.DrawComplete -= ChildDrawn;
                    _window.ProbeStatusMutation -= Mutated;
                    _window.ProbeStatusView.DrawComplete -= Drawn;
                    _window.DrawComplete -= RootDrawn;
                    _window.ProbeApplication.LayoutAndDrawComplete -= CycleCompleted;
                    _window.Disposing -= Disposing;
                    _feedback.TrySetCanceled();
                    _resultCycle?.TrySetCanceled();
                    _currentResult = null;
                    foreach (Mutation mutation in _mutations) mutation.Completion.TrySetCanceled();
                }
            }

            private sealed class Mutation(int sequence, bool progress)
            {
                internal int Sequence { get; } = sequence;
                internal bool Progress { get; } = progress;
                internal bool Drawn { get; set; }
                internal bool Cycle { get; set; }
                internal long? CycleTicks { get; set; }
                internal int RootDraws { get; set; }
                internal bool Coalesced { get; set; }
                internal List<string> Observations { get; } = [];
                internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>Exercises the same feedback dispatch on a running test owner loop without collecting timing samples.</summary>
        internal static async Task<bool> VerifyTerminalFeedbackOrderingAsync(
            TerminalWindow window, Action trigger, bool expectCoalescing)
        {
            _root = window;
            lock (Gate) { _stamps = []; _overflow = false; _workerThread = 0; }
            Task feedback = Task.CompletedTask;
            try
            {
                await UiAsync(() =>
                {
                    _terminalPaint = new TerminalPaintObservation(window, 0);
                    feedback = ArmFeedback();
                    Mark("operation-trigger");
                    trigger();
                    Mark("owner-return");
                }).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await feedback.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Stamp[] events;
                lock (Gate) events = _stamps!.ToArray();
                string[] order = ["feedback-request", "feedback-scene-armed", "operation-trigger",
                    "owner-return", "feedback-owner"];
                int previous = -1;
                int ownerThread = events.Single(item => item.Stage == "operation-trigger").Thread;
                foreach (string stage in order)
                {
                    int position = Array.FindIndex(events, item => item.Stage == stage);
                    if (position <= previous || events[position].Thread != ownerThread) return false;
                    previous = position;
                }
                return !_overflow && _terminalPaint!.ProvesNaturalCycle(expectCoalescing, events, ownerThread);
            }
            finally
            {
                _terminalPaint?.Dispose();
                _terminalPaint = null;
                lock (Gate) _stamps = null;
                _root = null;
            }
        }
#endif

        private static async Task PaintAsync(string? stage = null)
        {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            for (int frame = 0; frame < 2; frame++)
            {
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await UiAsync(() => ((MainWindow)_root!).RequestAnimationFrame(_ =>
                {
                    if (frame == 1 && stage is not null) Mark(stage);
                    done.TrySetResult();
                })).ConfigureAwait(false);
                await done.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
#else
            if (stage is null)
            {
                // Setup barrier only; the normal loop owns rendering.
                await UiAsync(() => { }).ConfigureAwait(false);
                return;
            }
            Task observed = Task.CompletedTask;
            await UiAsync(() => observed = _terminalPaint!.ObserveLatest(stage)).ConfigureAwait(false);
            await observed.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
#endif
        }

#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        private static DesktopShellViewModel Shell => (DesktopShellViewModel)((MainWindow)_root!).DataContext!;
        private static DefinitionFormModel Form => Shell.Workspace.Form!;
        private static bool Prepared => Shell.Publisher.CanSaveBundle;
#else
        private static TerminalWindow Window => (TerminalWindow)_root!;
        private static DefinitionFormModel Form => Window.ProbeForm;
        private static bool Prepared => Window.ProbePrepared;
#endif

        internal sealed record Stamp(string Stage, long Ticks, int Thread, long ThreadAllocatedBytes,
            long GcPauseBeforeTimeSpanTicks, long GcPauseAfterTimeSpanTicks,
            CollectionCounts GcCollectionsBefore, CollectionCounts GcCollectionsAfter);

        internal readonly record struct CollectionCounts(int Gen0, int Gen1, int Gen2);

        private sealed class ListenerObserver : IObserver<DiagnosticListener>
        {
            public void OnNext(DiagnosticListener value)
            {
                if (value.Name == "Configurator.Core.Preparation")
                    Subscriptions.Add(value.Subscribe(new PreparationObserver(),
                        name => name is "ValidationStarted" or "ValidationCompleted"
                            or "ProjectionStarted" or "ProjectionCompleted"
                            or "SerializationStarted" or "SerializationCompleted" or "ArtifactComposed"));
            }
            public void OnCompleted() { }
            public void OnError(Exception error) { }
        }

        private sealed class PreparationObserver : IObserver<KeyValuePair<string, object?>>
        {
            public void OnNext(KeyValuePair<string, object?> value)
            {
                // Never retain the diagnostic payload, including artifact identifiers.
                if (Environment.CurrentManagedThreadId == Volatile.Read(ref _workerThread)) Mark(value.Key);
            }
            public void OnCompleted() { }
            public void OnError(Exception error) { }
        }
    }
#endif
