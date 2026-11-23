using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace Configurator.BrowserQualification;

/// <summary>
/// Captures genuine trusted-input Chromium CDP interaction timings at 1x and 4x CPU throttling
/// conforming strictly to <c>eng/configurator/MeasureInteractiveWork.cs</c> (<c>synthetic-interaction-0.0.1</c>)
/// plus empirical <c>QF01</c> near-limit preparation latency metrics.
/// </summary>
internal static class CompoundRootBenchmarks
{
    internal const string InteractionFormat = "synthetic-interaction-0.0.1";
    internal const string Qf01Format = "configurator-qf01-preparation-0.0.1";
    internal const int RequiredSamplesPerCohort = 46;
    internal const int MinimumRequiredCohorts = 36;
    internal const double Qf01Budget1xMs = 100.0;
    internal const double Qf01Budget4xMs = 200.0;

    internal static readonly string[] CoreJourneys =
    [
        "fresh-environment",
        "switch-manifest",
        "ten-scalars",
        "ten-short-choices",
        "obscure-correction",
        "import-compare",
        "deferred-draft",
        "bulk-edit-malformed-recovery",
        "consent-revoke-editor"
    ];

    internal static readonly string[] ExtendedCompoundJourneys =
    [
        "few-root-deep-tree",
        "wide-nested-object",
        "large-map-list",
        "multi-artifact",
        "compact-600"
    ];

    private static readonly int[] CpuRates = [1, 4];

    private const string MeasurementBootstrapScript =
        """
        () => {
            if (window.__cfgMeasureInstalled) return;
            window.__cfgMeasureInstalled = true;
            window.__cfgLongTasks = [];
            try {
                const observer = new PerformanceObserver((list) => {
                    for (const entry of list.getEntries()) {
                        window.__cfgLongTasks.push({ start: entry.startTime, end: entry.startTime + entry.duration, duration: entry.duration });
                        if (window.__cfgLongTasks.length > 5000) window.__cfgLongTasks.shift();
                    }
                });
                observer.observe({ entryTypes: ['longtask'] });
            } catch (_) {
                // PerformanceObserver longtask is supported on Chromium; ignore if unavailable.
            }
            window.__cfgArm = (selector, eventTypes) => {
                const target = document.querySelector(selector);
                if (!target) throw new Error('arm-target-missing:' + selector);
                window.__cfgPending = { captured: false, trusted: false, startMs: 0, feedbackMs: 0 };
                const handler = (ev) => {
                    if (window.__cfgPending.captured) return;
                    const now = performance.now();
                    window.__cfgPending.captured = true;
                    window.__cfgPending.trusted = Boolean(ev.isTrusted);
                    window.__cfgPending.startMs = now;
                    requestAnimationFrame(() => {
                        window.__cfgPending.feedbackMs = Math.max(now, performance.now());
                    });
                    for (const t of eventTypes) target.removeEventListener(t, handler, true);
                };
                for (const t of eventTypes) target.addEventListener(t, handler, true);
            };
            window.__cfgAwaitSettled = (predicateSource, minStartMs) => new Promise((resolve, reject) => {
                const predicate = new Function('return (' + predicateSource + ')');
                const deadline = performance.now() + 20000;
                const check = () => {
                    const pending = window.__cfgPending;
                    if (!pending || !pending.captured) {
                        if (performance.now() > deadline) { reject(new Error('trusted-event-timeout')); return; }
                        requestAnimationFrame(check);
                        return;
                    }
                    let ready = false;
                    try { ready = Boolean(predicate()); } catch (_) { ready = false; }
                    if (!ready) {
                        if (performance.now() > deadline) { reject(new Error('content-predicate-timeout')); return; }
                        requestAnimationFrame(check);
                        return;
                    }
                    requestAnimationFrame(() => {
                        requestAnimationFrame(() => {
                            const contentMs = performance.now();
                            const startMs = Math.max(minStartMs, pending.startMs);
                            const feedbackMs = Math.max(startMs, pending.feedbackMs || contentMs);
                            const finalContentMs = Math.max(feedbackMs, contentMs);
                            const completionMs = Math.max(finalContentMs, performance.now());
                            requestAnimationFrame(() => {
                                const settledMs = Math.max(completionMs, performance.now());
                                let maxTask = 0;
                                for (const task of window.__cfgLongTasks) {
                                    if (task.end >= startMs && task.start <= settledMs && task.duration > maxTask) {
                                        maxTask = task.duration;
                                    }
                                }
                                resolve({
                                    trusted: pending.trusted,
                                    startMs,
                                    feedbackMs,
                                    contentMs: finalContentMs,
                                    completionMs,
                                    settledMs,
                                    maximumTaskMs: maxTask
                                });
                            });
                        });
                    });
                };
                requestAnimationFrame(check);
            });
        }
        """;

    /// <summary>
    /// Executes Chromium CDP 1x and 4x trusted-input cohorts across all 9 mandatory core journeys
    /// plus any available extended compound-root/compact fixtures, and optionally captures <c>QF01</c> preparation timings.
    /// </summary>
    internal static async Task<(string InteractionJson, string Qf01Json, bool Qf01Passed)> RunAsync(
        Uri baseUrl,
        string? fixturesDirectory,
        string fixtureDigest,
        string payloadDigest,
        string hostDigest,
        bool includeExtended)
    {
        if (!QualificationReceiptWriter.IsHex(fixtureDigest, 64)
            || !QualificationReceiptWriter.IsHex(payloadDigest, 64)
            || !QualificationReceiptWriter.IsHex(hostDigest, 64))
        {
            throw new ArgumentException("Benchmark identity digests must be 64-character lowercase hexadecimal.");
        }

        string baseOrigin = baseUrl.GetLeftPart(UriPartial.Authority);
        byte[] standardDefinition = QualificationChecks.BuildStandardDefinitionBytes();

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var cohorts = new JsonArray();
        var qf01Measurements = new JsonArray();
        bool qf01Passed = true;

        // Run all 9 mandatory core journeys on a clean Chromium context with CDP CPU throttling.
        await using (IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 840 }
        }))
        {
            context.SetDefaultTimeout(30_000f);
            IPage page = await context.NewPageAsync();
            ICDPSession cdp = await context.NewCDPSessionAsync(page);

            await QualificationChecks.OpenSyntheticWorkspaceAsync(page, baseOrigin, standardDefinition);
            await page.EvaluateAsync(MeasurementBootstrapScript);

            foreach (int rate in CpuRates)
            {
                await SetCpuThrottleAsync(cdp, rate);
                foreach (string journey in CoreJourneys)
                {
                    (JsonObject forward, JsonObject reverse) = await MeasureCoreJourneyPairAsync(page, journey, rate);
                    cohorts.Add(forward);
                    cohorts.Add(reverse);
                    Console.WriteLine($"INTERACTIVE_COHORT journey={journey} rate={rate}x directions=forward,reverse samples={RequiredSamplesPerCohort}");
                }
            }

            await SetCpuThrottleAsync(cdp, 1);
        }

        // Run extended compound-root and compact journeys if requested and fixtures exist.
        if (includeExtended && !string.IsNullOrWhiteSpace(fixturesDirectory) && Directory.Exists(fixturesDirectory))
        {
            foreach (string journey in ExtendedCompoundJourneys)
            {
                string definitionPath = Path.Combine(fixturesDirectory, journey, "definition.json");
                if (!File.Exists(definitionPath)) continue;
                byte[] definitionBytes = await File.ReadAllBytesAsync(definitionPath);

                await using IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    ViewportSize = new ViewportSize { Width = 1280, Height = 840 }
                });
                context.SetDefaultTimeout(30_000f);
                IPage page = await context.NewPageAsync();
                ICDPSession cdp = await context.NewCDPSessionAsync(page);

                await QualificationChecks.OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes, journey + ".json");
                await page.EvaluateAsync(MeasurementBootstrapScript);

                foreach (int rate in CpuRates)
                {
                    await SetCpuThrottleAsync(cdp, rate);
                    (JsonObject forward, JsonObject reverse) = await MeasureExtendedJourneyPairAsync(page, journey, rate);
                    cohorts.Add(forward);
                    cohorts.Add(reverse);
                    Console.WriteLine($"INTERACTIVE_COHORT journey={journey} rate={rate}x directions=forward,reverse samples={RequiredSamplesPerCohort}");
                }

                // Capture QF01 near-limit preparation latency when a valid expected artifact is available for autofill.
                string expectedArtifactPath = Path.Combine(fixturesDirectory, journey, "expected-diagnostic-00.json");
                if (journey != "compact-600" && File.Exists(expectedArtifactPath))
                {
                    await SetCpuThrottleAsync(cdp, 1);
                    byte[] artifactBytes = await File.ReadAllBytesAsync(expectedArtifactPath);
                    bool autofilled = await TryAutofillFixtureAnswersAsync(page, artifactBytes);
                    if (autofilled)
                    {
                        foreach (int rate in CpuRates)
                        {
                            await SetCpuThrottleAsync(cdp, rate);
                            (JsonObject qf01Row, bool rowPassed) = await MeasureQf01PreparationAsync(page, journey, rate);
                            qf01Measurements.Add(qf01Row);
                            qf01Passed &= rowPassed;
                        }
                    }
                }

                await SetCpuThrottleAsync(cdp, 1);
            }
        }

        var interactionRoot = new JsonObject
        {
            ["format"] = InteractionFormat,
            ["synthetic"] = true,
            ["fixture"] = fixtureDigest,
            ["payload"] = payloadDigest,
            ["host"] = hostDigest,
            ["status"] = "complete",
            ["cohorts"] = cohorts
        };

        var qf01Root = new JsonObject
        {
            ["format"] = Qf01Format,
            ["recordedAt"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["budget1xMs"] = Qf01Budget1xMs,
            ["budget4xMs"] = Qf01Budget4xMs,
            ["passed"] = qf01Passed,
            ["measurements"] = qf01Measurements
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        return (interactionRoot.ToJsonString(jsonOptions) + "\n", qf01Root.ToJsonString(jsonOptions) + "\n", qf01Passed);
    }

    /// <summary>
    /// Fail-closed structural validator for <c>synthetic-interaction-0.0.1</c> payloads used by <c>--self-test</c>.
    /// </summary>
    internal static bool ValidateInteractionPayload(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (root.GetProperty("format").GetString() != InteractionFormat) return false;
            if (!root.GetProperty("synthetic").GetBoolean()) return false;
            if (root.GetProperty("status").GetString() != "complete") return false;
            if (!QualificationReceiptWriter.IsHex(root.GetProperty("fixture").GetString() ?? string.Empty, 64)) return false;
            if (!QualificationReceiptWriter.IsHex(root.GetProperty("payload").GetString() ?? string.Empty, 64)) return false;
            if (!QualificationReceiptWriter.IsHex(root.GetProperty("host").GetString() ?? string.Empty, 64)) return false;

            JsonElement cohorts = root.GetProperty("cohorts");
            if (cohorts.ValueKind != JsonValueKind.Array) return false;
            int cohortCount = cohorts.GetArrayLength();
            if (cohortCount < MinimumRequiredCohorts || cohortCount > 160) return false;

            var seen = new HashSet<(string Journey, string Direction, int Rate)>();
            foreach (JsonElement cohort in cohorts.EnumerateArray())
            {
                string journey = cohort.GetProperty("journey").GetString() ?? string.Empty;
                string direction = cohort.GetProperty("direction").GetString() ?? string.Empty;
                int rate = cohort.GetProperty("rate").GetInt32();
                if (direction is not ("forward" or "reverse") || rate is not (1 or 4)) return false;
                if (!seen.Add((journey, direction, rate))) return false;
                if (cohort.GetProperty("warmups").GetInt32() != 5) return false;
                if (cohort.GetProperty("status").GetString() != "complete") return false;

                JsonElement samples = cohort.GetProperty("samples");
                if (samples.ValueKind != JsonValueKind.Array || samples.GetArrayLength() != RequiredSamplesPerCohort)
                    return false;

                double previousSettled = 0;
                int ordinal = 0;
                foreach (JsonElement sample in samples.EnumerateArray())
                {
                    if (!sample.GetProperty("trusted").GetBoolean()) return false;
                    if (sample.GetProperty("status").GetString() != "complete") return false;
                    double start = sample.GetProperty("startMs").GetDouble();
                    double feedback = sample.GetProperty("feedbackMs").GetDouble();
                    double content = sample.GetProperty("contentMs").GetDouble();
                    double completion = sample.GetProperty("completionMs").GetDouble();
                    double settled = sample.GetProperty("settledMs").GetDouble();
                    if (!(previousSettled <= start && start <= feedback && feedback <= content && content <= completion && completion <= settled))
                        return false;
                    previousSettled = settled;
                    ordinal++;
                }
            }

            foreach (string requiredJourney in CoreJourneys)
                foreach (int rate in CpuRates)
                    foreach (string direction in new[] { "forward", "reverse" })
                        if (!seen.Contains((requiredJourney, direction, rate)))
                            return false;

            return true;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static async Task SetCpuThrottleAsync(ICDPSession cdp, int rate) =>
        await cdp.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = rate });

    private static async Task<(JsonObject Forward, JsonObject Reverse)> MeasureCoreJourneyPairAsync(
        IPage page,
        string journey,
        int rate)
    {
        var forwardSamples = new JsonArray();
        var reverseSamples = new JsonArray();

        // Ensure baseline Configure stage before each journey.
        await EnsureConfigureStageAsync(page);

        double forwardLastSettled = 0;
        double reverseLastSettled = 0;

        for (int ordinal = 0; ordinal < RequiredSamplesPerCohort; ordinal++)
        {
            SampleTiming forwardTiming = await ExecuteJourneyStepAsync(page, journey, isForward: true, forwardLastSettled);
            forwardLastSettled = forwardTiming.SettledMs;
            forwardSamples.Add(ToSampleJson(ordinal, forwardTiming));

            SampleTiming reverseTiming = await ExecuteJourneyStepAsync(page, journey, isForward: false, reverseLastSettled);
            reverseLastSettled = reverseTiming.SettledMs;
            reverseSamples.Add(ToSampleJson(ordinal, reverseTiming));
        }

        return (BuildCohortJson(journey, "forward", rate, forwardSamples), BuildCohortJson(journey, "reverse", rate, reverseSamples));
    }

    private static async Task<(JsonObject Forward, JsonObject Reverse)> MeasureExtendedJourneyPairAsync(
        IPage page,
        string journey,
        int rate)
    {
        var forwardSamples = new JsonArray();
        var reverseSamples = new JsonArray();

        await EnsureConfigureStageAsync(page);
        double forwardLastSettled = 0;
        double reverseLastSettled = 0;

        for (int ordinal = 0; ordinal < RequiredSamplesPerCohort; ordinal++)
        {
            // Forward: navigate Configure -> Review (measures full compound-root projection & rendering).
            SampleTiming forwardTiming = await MeasureTrustedClickAsync(
                page,
                "[data-workflow-step='review']",
                "Boolean(document.querySelector(\"[data-workflow-panel='review']\"))",
                forwardLastSettled);
            forwardLastSettled = forwardTiming.SettledMs;
            forwardSamples.Add(ToSampleJson(ordinal, forwardTiming));

            // Reverse: return Review -> Configure.
            SampleTiming reverseTiming = await MeasureTrustedClickAsync(
                page,
                "[data-workflow-step='configure']",
                "Boolean(document.querySelector(\".configuration-configure-panel\"))",
                reverseLastSettled);
            reverseLastSettled = reverseTiming.SettledMs;
            reverseSamples.Add(ToSampleJson(ordinal, reverseTiming));
        }

        return (BuildCohortJson(journey, "forward", rate, forwardSamples), BuildCohortJson(journey, "reverse", rate, reverseSamples));
    }

    private static async Task<SampleTiming> ExecuteJourneyStepAsync(IPage page, string journey, bool isForward, double minStartMs) =>
        journey switch
        {
            "fresh-environment" => isForward
                ? await MeasureTrustedClickAsync(page, "[data-action='toggle-advanced-options']", "document.querySelector(\"[data-action='toggle-advanced-options']\")?.getAttribute('aria-expanded') === 'true'", minStartMs)
                : await MeasureTrustedClickAsync(page, "[data-action='toggle-advanced-options']", "document.querySelector(\"[data-action='toggle-advanced-options']\")?.getAttribute('aria-expanded') === 'false'", minStartMs),

            "switch-manifest" => await MeasureSelectToggleAsync(page, "#configuration-experience", isForward ? "extended" : "standard", minStartMs),

            "ten-scalars" => await MeasureInputToggleAsync(page, "[data-field='serviceName'] input", isForward ? "scalar-forward" : "scalar-reverse", minStartMs),

            "ten-short-choices" => isForward
                ? await MeasureTrustedClickAsync(page, "[data-field='logLevel'] input[type='radio']:nth-of-type(1)", "true", minStartMs, radioIndex: 1)
                : await MeasureTrustedClickAsync(page, "[data-field='logLevel'] input[type='radio']:nth-of-type(1)", "true", minStartMs, radioIndex: 0),

            "obscure-correction" => isForward
                ? await MeasureTrustedClickAsync(page, "[data-workflow-step='review']", "Boolean(document.querySelector(\"[data-workflow-panel='review']\"))", minStartMs)
                : await MeasureTrustedClickAsync(page, "[data-workflow-step='configure']", "Boolean(document.querySelector(\".configuration-configure-panel\"))", minStartMs),

            "import-compare" => isForward
                ? await MeasureTrustedClickAsync(page, "[data-entry-choice='import']", "document.querySelector(\"[data-entry-choice='import']\")?.getAttribute('aria-pressed') === 'true'", minStartMs)
                : await MeasureTrustedClickAsync(page, "[data-entry-choice='fresh']", "document.querySelector(\"[data-entry-choice='fresh']\")?.getAttribute('aria-pressed') === 'true'", minStartMs),

            "deferred-draft" => isForward
                ? await MeasureTrustedClickAsync(page, "[data-workflow-step='prepare']", "Boolean(document.querySelector(\"[data-files-state]\"))", minStartMs)
                : await MeasureTrustedClickAsync(page, "[data-workflow-step='configure']", "Boolean(document.querySelector(\".configuration-configure-panel\"))", minStartMs),

            "bulk-edit-malformed-recovery" => await MeasureInputToggleAsync(page, "[data-field='workerPort'] input", isForward ? "not-an-int" : "8080", minStartMs),

            "consent-revoke-editor" => await MeasureConsentToggleAsync(page, isForward, minStartMs),

            _ => throw new InvalidOperationException("Unsupported journey: " + journey)
        };

    private static async Task<SampleTiming> MeasureTrustedClickAsync(
        IPage page,
        string selector,
        string predicateJs,
        double minStartMs,
        int? radioIndex = null)
    {
        if (radioIndex.HasValue)
        {
            ILocator radio = page.Locator("[data-field='logLevel'] input[type='radio']").Nth(radioIndex.Value);
            await page.EvaluateAsync(
                "([idx]) => { const el = document.querySelectorAll(\"[data-field='logLevel'] input[type='radio']\")[idx]; el.setAttribute('data-bench-target', '1'); }",
                new[] { radioIndex.Value });
            await page.EvaluateAsync("() => window.__cfgArm(\"[data-bench-target='1']\", ['pointerdown', 'mousedown', 'click'])");
            await radio.ClickAsync();
            JsonElement rawRadio = await page.EvaluateAsync<JsonElement>(
                "([pred, min]) => window.__cfgAwaitSettled(pred, min)",
                new object[] { predicateJs, minStartMs });
            await page.EvaluateAsync("() => { const el = document.querySelector(\"[data-bench-target='1']\"); if (el) el.removeAttribute('data-bench-target'); }");
            return ParseTiming(rawRadio);
        }

        await page.EvaluateAsync("([sel]) => window.__cfgArm(sel, ['pointerdown', 'mousedown', 'click'])", new[] { selector });
        await page.Locator(selector).First.ClickAsync();
        JsonElement raw = await page.EvaluateAsync<JsonElement>(
            "([pred, min]) => window.__cfgAwaitSettled(pred, min)",
            new object[] { predicateJs, minStartMs });
        return ParseTiming(raw);
    }

    private static async Task<SampleTiming> MeasureSelectToggleAsync(IPage page, string selector, string optionValue, double minStartMs)
    {
        // Ensure advanced panel is open so #configuration-experience is visible.
        ILocator advancedToggle = page.Locator("[data-action='toggle-advanced-options']");
        if (await advancedToggle.GetAttributeAsync("aria-expanded") != "true")
            await advancedToggle.ClickAsync();

        await page.Locator(selector).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.EvaluateAsync("([sel]) => window.__cfgArm(sel, ['change', 'input'])", new[] { selector });
        await page.Locator(selector).SelectOptionAsync(optionValue);
        string predicate = $"document.querySelector('{selector}')?.value === '{optionValue}'";
        JsonElement raw = await page.EvaluateAsync<JsonElement>(
            "([pred, min]) => window.__cfgAwaitSettled(pred, min)",
            new object[] { predicate, minStartMs });
        return ParseTiming(raw);
    }

    private static async Task<SampleTiming> MeasureInputToggleAsync(IPage page, string selector, string value, double minStartMs)
    {
        ILocator input = page.Locator(selector).First;
        await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.EvaluateAsync("([sel]) => window.__cfgArm(sel, ['input', 'keydown', 'focus'])", new[] { selector });
        await input.ClickAsync();
        await input.FillAsync(value);
        string predicate = $"document.querySelector(\"{selector}\")?.value === \"{value}\"";
        JsonElement raw = await page.EvaluateAsync<JsonElement>(
            "([pred, min]) => window.__cfgAwaitSettled(pred, min)",
            new object[] { predicate, minStartMs });
        return ParseTiming(raw);
    }

    private static async Task<SampleTiming> MeasureConsentToggleAsync(IPage page, bool enable, double minStartMs)
    {
        ILocator details = page.Locator("[data-secret-consent]");
        if (await details.GetAttributeAsync("open") is null)
            await details.Locator("summary").ClickAsync();

        ILocator checkbox = page.Locator("#secret-consent");
        await checkbox.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.EvaluateAsync("() => window.__cfgArm('#secret-consent', ['pointerdown', 'click', 'change'])");
        if (enable)
            await checkbox.CheckAsync();
        else
            await checkbox.UncheckAsync();

        string predicate = enable
            ? "Boolean(document.querySelector(\"[data-field='apiSecret'] input\"))"
            : "Boolean(document.querySelector(\"[data-field='apiSecret'] [data-secret-denied]\"))";
        JsonElement raw = await page.EvaluateAsync<JsonElement>(
            "([pred, min]) => window.__cfgAwaitSettled(pred, min)",
            new object[] { predicate, minStartMs });
        return ParseTiming(raw);
    }

    private static async Task EnsureConfigureStageAsync(IPage page)
    {
        ILocator configureButton = page.Locator("[data-workflow-step='configure']");
        if (await configureButton.GetAttributeAsync("aria-current") != "step")
        {
            await configureButton.ClickAsync();
            await page.Locator(".configuration-configure-panel").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        }

        ILocator advancedToggle = page.Locator("[data-action='toggle-advanced-options']");
        if (await advancedToggle.GetAttributeAsync("aria-expanded") == "true")
            await advancedToggle.ClickAsync();
    }

    private static async Task<bool> TryAutofillFixtureAnswersAsync(IPage page, byte[] artifactBytes)
    {
        await EnsureConfigureStageAsync(page);
        ILocator importChoice = page.Locator("[data-entry-choice='import']");
        if (await importChoice.CountAsync() == 0) return false;
        await importChoice.ClickAsync();
        await page.Locator("#import-purpose").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Locator("#import-purpose").SelectOptionAsync("fill");
        await page.Locator("#artifact-import").SetInputFilesAsync(new FilePayload
        {
            Name = "diagnostic-00.json",
            MimeType = "application/json",
            Buffer = artifactBytes
        });
        await page.WaitForFunctionAsync("() => { const s = document.querySelector('[data-status-code]'); return Boolean(s && s.getAttribute('data-status-code') !== 'workspace-ready'); }");
        string? statusCode = await page.Locator("[data-status-code]").GetAttributeAsync("data-status-code");
        return statusCode == "import-autofilled";
    }

    private static async Task<(JsonObject Row, bool Passed)> MeasureQf01PreparationAsync(IPage page, string fixtureId, int rate)
    {
        await page.Locator("[data-workflow-step='prepare']").ClickAsync();
        await page.Locator("#prepare-complete").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        const int warmups = 3;
        const int measured = 10;
        var durations = new List<double>(measured);
        double minStart = 0;

        for (int index = 0; index < warmups + measured; index++)
        {
            // Clear any prior prepared artifact first when present so #prepare-complete does real composition work.
            if (await page.Locator("[data-prepared-file]").CountAsync() > 0)
            {
                await page.Locator("[data-workflow-step='configure']").ClickAsync();
                await page.Locator("[data-workflow-step='prepare']").ClickAsync();
            }

            SampleTiming timing = await MeasureTrustedClickAsync(
                page,
                "#prepare-complete",
                "document.querySelectorAll('[data-prepared-file]').length > 0",
                minStart);
            minStart = timing.SettledMs;
            if (index >= warmups)
                durations.Add(timing.ContentMs - timing.StartMs);
        }

        durations.Sort();
        double p50 = durations[(int)Math.Ceiling(durations.Count * 0.50) - 1];
        double p95 = durations[(int)Math.Ceiling(durations.Count * 0.95) - 1];
        double max = durations[^1];
        double budget = rate == 1 ? Qf01Budget1xMs : Qf01Budget4xMs;
        bool passed = p95 <= budget;

        Console.WriteLine($"QF01_PREP fixture={fixtureId} rate={rate}x p50Ms={p50:F2} p95Ms={p95:F2} maxMs={max:F2} budgetMs={budget:F0} passed={passed}");

        var row = new JsonObject
        {
            ["fixture"] = fixtureId,
            ["rate"] = rate,
            ["sampleCount"] = durations.Count,
            ["p50Ms"] = Math.Round(p50, 2),
            ["p95Ms"] = Math.Round(p95, 2),
            ["maximumMs"] = Math.Round(max, 2),
            ["budgetMs"] = budget,
            ["passed"] = passed
        };
        return (row, passed);
    }

    private static SampleTiming ParseTiming(JsonElement element) =>
        new(
            element.GetProperty("trusted").GetBoolean(),
            element.GetProperty("startMs").GetDouble(),
            element.GetProperty("feedbackMs").GetDouble(),
            element.GetProperty("contentMs").GetDouble(),
            element.GetProperty("completionMs").GetDouble(),
            element.GetProperty("settledMs").GetDouble(),
            element.GetProperty("maximumTaskMs").GetDouble());

    private static JsonObject ToSampleJson(int ordinal, SampleTiming timing) =>
        new()
        {
            ["phase"] = ordinal == 0 ? "first" : ordinal <= 5 ? "warmup" : "measured",
            ["index"] = ordinal == 0 ? 1 : ordinal <= 5 ? ordinal : ordinal - 5,
            ["trusted"] = timing.Trusted,
            ["status"] = "complete",
            ["startMs"] = Math.Round(timing.StartMs, 3),
            ["feedbackMs"] = Math.Round(timing.FeedbackMs, 3),
            ["contentMs"] = Math.Round(timing.ContentMs, 3),
            ["completionMs"] = Math.Round(timing.CompletionMs, 3),
            ["settledMs"] = Math.Round(timing.SettledMs, 3),
            ["maximumTaskMs"] = Math.Round(timing.MaximumTaskMs, 3)
        };

    private static JsonObject BuildCohortJson(string journey, string direction, int rate, JsonArray samples) =>
        new()
        {
            ["journey"] = journey,
            ["direction"] = direction,
            ["rate"] = rate,
            ["status"] = "complete",
            ["warmups"] = 5,
            ["contentBudgetMs"] = 100 * rate,
            ["samples"] = samples
        };

    private sealed record SampleTiming(
        bool Trusted,
        double StartMs,
        double FeedbackMs,
        double ContentMs,
        double CompletionMs,
        double SettledMs,
        double MaximumTaskMs);
}
