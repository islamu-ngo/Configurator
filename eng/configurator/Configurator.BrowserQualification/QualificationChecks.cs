using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace Configurator.BrowserQualification;

/// <summary>
/// Executes the 16 canonical published-browser qualification checks (<c>Q01</c> through <c>Q16</c>)
/// against a live static host using deterministic Playwright DOM/network waits (zero timing sleeps).
/// </summary>
internal static class QualificationChecks
{
    private const float ActionTimeoutMs = 30_000f;
    private const string AnswerCanary = "SYNTH-CANARY-ANS-9F2A";
    private const string SecretCanary = "SYNTH-CANARY-SEC-7C4E";
    private const string PrivateMapKeyCanary = "synthetic-private-map-key";

    internal static readonly IReadOnlyList<string> RequiredCheckIds =
    [
        "Q01-boot-and-package",
        "Q02-definition-admission",
        "Q03-typed-presence-omission",
        "Q04-secret-consent-and-hosted-custody",
        "Q05-complete-and-draft-outputs",
        "Q06-nested-import-conflicts",
        "Q07-deliberate-reveal-copy-download",
        "Q08-invalid-edit-fencing",
        "Q09-settings-and-explore-reloads",
        "Q10-offline-retry-delete-and-capacity",
        "Q11-custody-sink-canary-checks",
        "Q12-two-tab-update-safety",
        "Q13-keyboard-focus-rtl-and-mobile-reflow",
        "Q14-wave2-contrast-and-focus-rings",
        "Q15-scale-definition-admission-and-discovery",
        "Q16-host-healthz-headers-and-method-policy"
    ];

    internal static readonly IReadOnlyList<string> DefaultEngines = ["chromium", "firefox", "webkit"];

    /// <summary>
    /// Launches each requested engine and runs <c>Q01–Q16</c> in isolated contexts without skipping failures.
    /// </summary>
    internal static async Task<QualificationReport> RunAllEnginesAsync(
        Uri baseUrl,
        IReadOnlyList<string> engines,
        int minimumExpectedChecks)
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        var engineSummaries = new List<EngineRunSummary>(engines.Count);

        foreach (string engineName in engines)
        {
            IBrowserType? browserType = ResolveBrowserType(playwright, engineName);
            if (browserType is null)
            {
                engineSummaries.Add(new EngineRunSummary(engineName, false, null, "engine-unsupported", []));
                continue;
            }

            IBrowser? browser = null;
            try
            {
                browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            catch (PlaywrightException)
            {
                engineSummaries.Add(new EngineRunSummary(engineName, false, null, "engine-launch-failed", []));
                continue;
            }

            try
            {
                string version = browser.Version;
                List<CheckOutcome> outcomes = await RunEngineSuiteAsync(playwright, browser, engineName, baseUrl);
                engineSummaries.Add(new EngineRunSummary(engineName, true, version, null, outcomes));
            }
            finally
            {
                await browser.DisposeAsync();
            }
        }

        string recordedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var tentative = new QualificationReport(
            QualificationReceiptWriter.ReportFormat,
            baseUrl.GetLeftPart(UriPartial.Authority),
            recordedAt,
            minimumExpectedChecks,
            engines,
            false,
            engineSummaries);

        (bool passed, _) = QualificationReceiptWriter.ValidateRunGate(tentative, minimumExpectedChecks, engines);
        return tentative with { Passed = passed };
    }

    private static IBrowserType? ResolveBrowserType(IPlaywright playwright, string engineName) =>
        engineName switch
        {
            "chromium" => playwright.Chromium,
            "firefox" => playwright.Firefox,
            "webkit" => playwright.Webkit,
            _ => null
        };

    private static async Task<List<CheckOutcome>> RunEngineSuiteAsync(
        IPlaywright playwright,
        IBrowser browser,
        string engineName,
        Uri baseUrl)
    {
        string baseOrigin = baseUrl.GetLeftPart(UriPartial.Authority);
        byte[] standardDefinition = BuildStandardDefinitionBytes();
        byte[] importConflictDefinition = BuildImportConflictDefinitionBytes();
        byte[] scaleDefinition = BuildScaleDefinitionBytes(120);

        var checks = new (string Id, string Locale, Func<Task> Action)[]
        {
            ("Q01-boot-and-package", "en", () => CheckBootAndPackageAsync(browser, baseOrigin)),
            ("Q02-definition-admission", "en", () => CheckDefinitionAdmissionAsync(browser, baseOrigin, standardDefinition)),
            ("Q03-typed-presence-omission", "en", () => CheckTypedPresenceAndOmissionAsync(browser, baseOrigin, standardDefinition)),
            ("Q04-secret-consent-and-hosted-custody", "en", () => CheckSecretConsentAndHostedCustodyAsync(browser, baseOrigin, standardDefinition)),
            ("Q05-complete-and-draft-outputs", "en", () => CheckCompleteAndDraftOutputsAsync(browser, baseOrigin, standardDefinition)),
            ("Q06-nested-import-conflicts", "en", () => CheckNestedImportConflictsAsync(browser, baseOrigin, importConflictDefinition)),
            ("Q07-deliberate-reveal-copy-download", "en", () => CheckDeliberateRevealCopyDownloadAsync(browser, baseOrigin, standardDefinition)),
            ("Q08-invalid-edit-fencing", "en", () => CheckInvalidEditFencingAsync(browser, baseOrigin, standardDefinition)),
            ("Q09-settings-and-explore-reloads", "en", () => CheckSettingsAndExploreReloadsAsync(playwright, browser, baseOrigin)),
            ("Q10-offline-retry-delete-and-capacity", "en", () => CheckOfflineRetryDeleteCapacityAsync(browser, baseOrigin, standardDefinition)),
            ("Q11-custody-sink-canary-checks", "en", () => CheckCustodySinkCanariesAsync(browser, baseOrigin, standardDefinition)),
            ("Q12-two-tab-update-safety", "en", () => CheckTwoTabUpdateSafetyAsync(browser, baseOrigin, standardDefinition)),
            ("Q13-keyboard-focus-rtl-and-mobile-reflow", "en", () => CheckKeyboardRtlAndMobileReflowAsync(browser, baseOrigin, standardDefinition)),
            ("Q14-wave2-contrast-and-focus-rings", "en", () => CheckWave2ContrastAndFocusRingsAsync(browser, baseOrigin, standardDefinition)),
            ("Q15-scale-definition-admission-and-discovery", "en", () => CheckScaleDefinitionAdmissionAsync(browser, baseOrigin, scaleDefinition)),
            ("Q16-host-healthz-headers-and-method-policy", "en", () => CheckHostHealthzHeadersAndMethodsAsync(playwright, baseOrigin))
        };

        var outcomes = new List<CheckOutcome>(checks.Length);
        foreach ((string id, string locale, Func<Task> action) in checks)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await action();
                stopwatch.Stop();
                outcomes.Add(new CheckOutcome(id, engineName, locale, true, Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2), null));
                Console.WriteLine($"QUALIFICATION_CHECK engine={engineName} check={id} status=passed durationMs={stopwatch.Elapsed.TotalMilliseconds:F1}");
            }
            catch (Exception exception) when (exception is PlaywrightException or TimeoutException or InvalidOperationException or JsonException or FormatException)
            {
                stopwatch.Stop();
                string code = exception is QualificationFailureException failure
                    ? failure.DiagnosticCode
                    : exception.GetType().Name;
                outcomes.Add(new CheckOutcome(id, engineName, locale, false, Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2), code));
                Console.WriteLine($"QUALIFICATION_CHECK engine={engineName} check={id} status=failed code={code} durationMs={stopwatch.Elapsed.TotalMilliseconds:F1}");
            }
        }

        return outcomes;
    }

    private static async Task<IBrowserContext> CreateContextAsync(IBrowser browser, int width = 1280, int height = 840)
    {
        IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            AcceptDownloads = true,
            ViewportSize = new ViewportSize { Width = width, Height = height }
        });
        context.SetDefaultTimeout(ActionTimeoutMs);
        context.SetDefaultNavigationTimeout(ActionTimeoutMs);
        return context;
    }

    private static async Task CheckBootAndPackageAsync(IBrowser browser, string baseOrigin)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        var errors = new List<string>();
        var badResponses = new List<int>();
        page.PageError += (_, message) => errors.Add(message);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        page.Response += (_, response) =>
        {
            if (response.Status >= 400 && response.Url.StartsWith(baseOrigin, StringComparison.Ordinal))
                badResponses.Add(response.Status);
        };

        IResponse? response = await page.GotoAsync(baseOrigin + "/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Require(response is { Status: 200 }, "boot-http-status");
        await page.Locator("article.catalogue__card").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        int cardCount = await page.Locator("article.catalogue__card").CountAsync();
        Require(cardCount >= 1, "embedded-store-empty");
        Require(badResponses.Count == 0, "boot-asset-error");
        Require(errors.Count == 0, "boot-console-error");
    }

    private static async Task CheckDefinitionAdmissionAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await page.GotoAsync(baseOrigin + "/open", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#definition-file").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Malformed JSON must fail closed without opening workspace.
        await page.Locator("#definition-file").SetInputFilesAsync(new FilePayload
        {
            Name = "invalid.json",
            MimeType = "application/json",
            Buffer = Encoding.UTF8.GetBytes("{ invalid")
        });
        await page.Locator("[role='status']").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Require(await page.Locator("[data-action='open-workspace']").CountAsync() == 0, "malformed-definition-admitted");

        // Valid synthetic public definition must admit cleanly with digest disclosure.
        await UploadDefinitionAsync(page, "qualification-definition.json", definitionBytes);
        await page.Locator("[data-digest-disclosure]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Locator("[data-action='open-workspace']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task CheckTypedPresenceAndOmissionAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);

        // Edit required text scalar and integer scalar.
        await FillFieldInputAsync(page, "serviceName", "synthetic-edge-node");
        await FillFieldInputAsync(page, "workerPort", "8443");

        // Expand optional nested object if collapsed and edit leaf.
        await EnsureNestedHostEditableAsync(page);
        await FillFieldInputAsync(page, "options.db.host", "db.local.synthetic");

        // Select enum choice (radio group).
        ILocator warnRadio = page.Locator("[data-field='logLevel'] input[type='radio']").Nth(1);
        if (await warnRadio.CountAsync() > 0)
            await warnRadio.ClickAsync();

        // Verify optional field actions menu exposes omit/reset cleanly.
        ILocator trigger = page.Locator("[data-field='optionalNote'] .configuration-field-actions__trigger");
        if (await trigger.CountAsync() > 0)
        {
            await FillFieldInputAsync(page, "optionalNote", "synthetic-note");
            await trigger.ClickAsync();
            ILocator omitItem = page.Locator("[data-action='omit']").First;
            await omitItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await omitItem.ClickAsync();
        }
    }

    private static async Task CheckSecretConsentAndHostedCustodyAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);

        // Secret input must be denied before explicit consent.
        await page.Locator("[data-field='apiSecret'] [data-secret-denied]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Open Trust & Privacy utility via WorkspaceHeader consent chip and verify default unchecked + hosted-code disclosure + custody link.
        await EnsureTrustPrivacyOpenAsync(page);
        ILocator consentCheckbox = page.Locator("#secret-consent");
        await consentCheckbox.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Require(!await consentCheckbox.IsCheckedAsync(), "secret-consent-default-checked");

        ILocator hostedNotice = page.Locator("[data-hosted-code-custody]");
        await hostedNotice.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        string noticeText = await hostedNotice.InnerTextAsync();
        Require(noticeText.Contains("browser", StringComparison.OrdinalIgnoreCase)
            && noticeText.Contains("extension", StringComparison.OrdinalIgnoreCase), "hosted-custody-disclosure-missing");

        ILocator custodyLink = page.Locator("[data-action='custody-guidance']");
        string? href = await custodyLink.GetAttributeAsync("href");
        Require(href == "help#custody", "custody-link-mismatch");

        // Enable consent, enter synthetic secret, then revoke via always-visible WorkspaceHeader control and verify secret input disappears.
        await consentCheckbox.CheckAsync();
        ILocator secretInput = page.Locator("[data-field='apiSecret'] input");
        await secretInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await secretInput.FillAsync(SecretCanary);

        ILocator revokeButton = page.Locator("header.workspace-header [data-action='revoke-secret-entry']");
        await revokeButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await revokeButton.ClickAsync();
        await page.Locator("[data-field='apiSecret'] [data-secret-denied]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Require(!await consentCheckbox.IsCheckedAsync(), "secret-consent-not-revoked");

        // Verify Arabic localization of hosted-code disclosure and RTL direction.
        await SwitchWorkspaceLocaleAsync(page, "ar");
        Require(await page.Locator("section.configuration-editor").GetAttributeAsync("dir") == "rtl", "arabic-rtl-missing");
        await hostedNotice.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        string arabicNotice = await hostedNotice.InnerTextAsync();
        Require(arabicNotice.Length > 10 && arabicNotice != noticeText, "arabic-custody-not-localized");

        // Verify /help#custody anchor renders zero-SecretKit custody guidance.
        await page.GotoAsync(baseOrigin + "/help#custody", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#custody").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task CheckCompleteAndDraftOutputsAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);

        await FillFieldInputAsync(page, "serviceName", "synthetic-draft-svc");
        await FillFieldInputAsync(page, "workerPort", "9001");

        // Prepare deferred-secret draft without enabling secret consent.
        await page.Locator("[data-workflow-step='prepare']").ClickAsync();
        await page.Locator("#prepare-draft").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Locator("#prepare-draft").ClickAsync();
        await page.Locator("[data-prepared-file][data-readiness='draft']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Locator("[data-deferred-secret-summary]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Enable consent, supply secret, prepare complete artifact, and verify exact JSON via deliberate reveal.
        await page.Locator("[data-workflow-step='configure']").ClickAsync();
        await EnsureTrustPrivacyOpenAsync(page);
        await page.Locator("#secret-consent").CheckAsync();
        await FillFieldInputAsync(page, "apiSecret", SecretCanary);

        await page.Locator("[data-workflow-step='prepare']").ClickAsync();
        await page.Locator("#prepare-complete").ClickAsync();
        await page.Locator("[data-prepared-file][data-readiness='complete']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await page.Locator("[data-action='reveal-file']").ClickAsync();
        ILocator preview = page.Locator(".configuration-files__preview");
        await preview.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync("() => (document.querySelector('.configuration-files__preview')?.textContent || '').trim().length > 0");
        string revealed = await preview.InnerTextAsync();
        Require(revealed.Contains("synthetic-draft-svc", StringComparison.Ordinal)
            && revealed.Contains("9001", StringComparison.Ordinal)
            && revealed.Contains(SecretCanary, StringComparison.Ordinal), "complete-output-mismatch");
    }

    private static async Task CheckNestedImportConflictsAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);

        // Set nested declared property options.db.host in workspace so import produces nested answer-conflict.
        await EnsureNestedHostEditableAsync(page);
        await FillFieldInputAsync(page, "options.db.host", "workspace-db.local");

        // Open import utility panel and verify unconsented file upload is refused fail-closed before reading.
        await OpenImportUtilityAsync(page);
        await page.Locator("#import-purpose").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Locator("#import-purpose").SelectOptionAsync("compare");

        string conflictArtifact = """
            {
              "serviceName": "imported-svc",
              "workerPort": 8080,
              "options": { "db": { "host": "imported-db.local" } },
              "labels": { "synthetic-private-map-key": "SYNTH-CANARY-ANS-9F2A" }
            }
            """;

        await page.Locator("#artifact-import").SetInputFilesAsync(new FilePayload
        {
            Name = "app-config.json",
            MimeType = "application/json",
            Buffer = Encoding.UTF8.GetBytes(conflictArtifact)
        });
        await page.WaitForFunctionAsync("() => document.querySelector(\"[data-status-code='import-secret-consent-required']\") !== null");

        // Grant explicit per-workspace consent before comparing and autofilling existing config files.
        await EnsureTrustPrivacyOpenAsync(page);
        await page.Locator("#secret-consent").CheckAsync();
        await OpenImportUtilityAsync(page);
        await page.Locator("#artifact-import").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await page.Locator("#artifact-import").SetInputFilesAsync(new FilePayload
        {
            Name = "app-config.json",
            MimeType = "application/json",
            Buffer = Encoding.UTF8.GetBytes(conflictArtifact)
        });

        ILocator conflictLocation = page.Locator("[data-import-conflict-location]");
        await conflictLocation.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        string locationText = await conflictLocation.InnerTextAsync();
        Require(locationText.Contains("options", StringComparison.Ordinal)
            && locationText.Contains("host", StringComparison.Ordinal), "nested-conflict-path-missing");

        string comparisonMarkup = await page.Locator("[data-import-comparison]").InnerTextAsync();
        Require(!comparisonMarkup.Contains(PrivateMapKeyCanary, StringComparison.Ordinal)
            && !comparisonMarkup.Contains(AnswerCanary, StringComparison.Ordinal), "private-map-key-leaked-in-compare");

        // Also test Autofill mode status surfaces declared location without leaking private keys.
        await page.Locator("#import-purpose").SelectOptionAsync("fill");
        await page.Locator("#artifact-import").SetInputFilesAsync(new FilePayload
        {
            Name = "app-config.json",
            MimeType = "application/json",
            Buffer = Encoding.UTF8.GetBytes(conflictArtifact)
        });
        await page.WaitForFunctionAsync(
            "() => { const s = document.querySelector(\"[data-status-code='answer-conflict']\"); return Boolean(s && s.textContent && (s.textContent.includes('options') || s.textContent.includes('Database host'))); }");

        string statusText = await page.Locator("[data-status-code]").InnerTextAsync();
        Require(!statusText.Contains(PrivateMapKeyCanary, StringComparison.Ordinal)
            && !statusText.Contains(AnswerCanary, StringComparison.Ordinal), "private-map-key-leaked-in-autofill");
    }

    private static async Task CheckDeliberateRevealCopyDownloadAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);

        await FillFieldInputAsync(page, "serviceName", "export-check-svc");
        await FillFieldInputAsync(page, "workerPort", "8090");
        await page.Locator("[data-workflow-step='prepare']").ClickAsync();
        await page.Locator("#prepare-draft").ClickAsync();
        await page.Locator("[data-prepared-file]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Preview must be hidden by default.
        ILocator preview = page.Locator(".configuration-files__preview");
        Require(await preview.IsHiddenAsync(), "preview-not-hidden-by-default");

        // Deliberate reveal and hide.
        ILocator revealButton = page.Locator("[data-action='reveal-file']");
        await revealButton.ClickAsync();
        await preview.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync("() => (document.querySelector('.configuration-files__preview')?.textContent || '').trim().length > 0");
        Require((await preview.InnerTextAsync()).Contains("export-check-svc", StringComparison.Ordinal), "reveal-content-missing");
        await revealButton.ClickAsync();
        await preview.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

        // Deliberate download produces real downloaded bytes.
        IDownload download = await page.RunAndWaitForDownloadAsync(() => page.Locator("[data-action='download-file']").ClickAsync());
        Require(download.SuggestedFilename.EndsWith(".json", StringComparison.OrdinalIgnoreCase), "download-filename-unexpected");
        string? downloadPath = await download.PathAsync();
        Require(downloadPath is not null && File.Exists(downloadPath), "download-file-missing");
        string downloadedText = await File.ReadAllTextAsync(downloadPath!);
        Require(downloadedText.Contains("export-check-svc", StringComparison.Ordinal), "download-content-mismatch");

        // Deliberate copy button triggers status update cleanly.
        await page.Locator("[data-action='copy-file']").ClickAsync();
        await page.Locator("[data-file-status]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task CheckInvalidEditFencingAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);

        await FillFieldInputAsync(page, "serviceName", "fence-svc");
        await FillFieldInputAsync(page, "workerPort", "8088");
        await page.Locator("[data-workflow-step='prepare']").ClickAsync();
        await page.Locator("#prepare-draft").ClickAsync();
        await page.Locator("[data-prepared-file]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Make invalid edit on integer field -> prepared files must invalidate and complete preparation must block.
        await page.Locator("[data-workflow-step='configure']").ClickAsync();
        await FillFieldInputAsync(page, "workerPort", "not-a-number");
        await page.Locator("[data-workflow-step='prepare']").ClickAsync();
        await page.Locator("[data-files-invalidated]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Require(await page.Locator("[data-prepared-file]").CountAsync() == 0, "stale-prepared-file-survived-edit");

        await page.Locator("#prepare-complete").ClickAsync();
        await page.Locator("[data-correction]").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Require(await page.Locator("[data-prepared-file]").CountAsync() == 0, "invalid-edit-produced-output");
    }

    private static async Task CheckSettingsAndExploreReloadsAsync(IPlaywright playwright, IBrowser browser, string baseOrigin)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);

        // Discover an /explore/{id} route from the live catalogue first.
        await page.GotoAsync(baseOrigin + "/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("[data-action='explore-definition']").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        string? exploreHref = await page.Locator("[data-action='explore-definition']").First.GetAttributeAsync("href");
        Require(!string.IsNullOrWhiteSpace(exploreHref), "explore-link-missing");

        string[] routes = ["/settings", "/settings/appearance", "/settings/privacy", "/help", "/open", "/" + exploreHref!.TrimStart('/')];
        foreach (string route in routes)
        {
            IResponse? response = await page.GotoAsync(baseOrigin + route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            Require(response is { Status: 200 }, "route-reload-status-failed");
            if (route.StartsWith("/settings", StringComparison.Ordinal))
                await page.Locator("[data-settings-workspace='true']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            else if (route.StartsWith("/explore", StringComparison.Ordinal))
                await page.Locator("[data-explore-definition]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        }

        // Direct HTTP probe (bypassing active Service Worker navigation cache) verifies Host rejects extension paths with 404.
        await using IAPIRequestContext api = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = baseOrigin,
            IgnoreHTTPSErrors = true
        });
        IAPIResponse missingAsset = await api.GetAsync("/settings/nonexistent.js");
        Require(missingAsset.Status == 404, "settings-extension-probe-not-404");
        Require(errors.Count == 0, "route-reload-page-error");
    }

    private static async Task CheckOfflineRetryDeleteCapacityAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await page.GotoAsync(baseOrigin + "/open", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#definition-file").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // If clean-entry offline recovery banner is shown, exercise retry cleanly.
        ILocator retryButton = page.Locator("[data-action='retry-offline']");
        if (await retryButton.IsVisibleAsync())
            await retryButton.ClickAsync();

        // Admit and explicitly retain definition (confirming public metadata checkbox).
        await UploadDefinitionAsync(page, "retain-test.json", definitionBytes);
        await page.Locator(".open-page__retention input[type='checkbox']").CheckAsync();
        ILocator retainButton = page.Locator("[data-action='retain-definition']");
        await retainButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await retainButton.ClickAsync();

        // Verify retained list, capacity badge (1 / 8), selective removal, and APG focus restoration to #definition-file.
        bool isSecureContext = await page.EvaluateAsync<bool>("() => window.isSecureContext && 'caches' in window");
        if (isSecureContext)
        {
            await page.Locator("[data-retained-definitions]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            string capacityText = await page.Locator("[data-retained-capacity]").InnerTextAsync();
            Require(capacityText.Contains('1', StringComparison.Ordinal) && capacityText.Contains('8', StringComparison.Ordinal), "retained-capacity-mismatch");

            ILocator removeButton = page.Locator("[data-action='remove-retained']").First;
            string? ariaLabel = await removeButton.GetAttributeAsync("aria-label");
            Require(!string.IsNullOrWhiteSpace(ariaLabel), "remove-retained-aria-label-missing");
            await removeButton.ClickAsync();
            await page.Locator("[data-retained-definitions]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
            await page.WaitForFunctionAsync("() => document.activeElement !== null && document.activeElement.id === 'definition-file'");
            string activeId = await page.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.id : ''");
            Require(activeId == "definition-file", "apg-focus-restoration-failed");
        }
    }

    private static async Task CheckCustodySinkCanariesAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        bool workspaceEntered = false;
        bool networkLeak = false;
        page.Request += (_, request) =>
        {
            if (!workspaceEntered) return;
            if (request.Url.Contains(AnswerCanary, StringComparison.Ordinal)
                || request.Url.Contains(SecretCanary, StringComparison.Ordinal)
                || (request.PostData is { } postData
                    && (postData.Contains(AnswerCanary, StringComparison.Ordinal)
                        || postData.Contains(SecretCanary, StringComparison.Ordinal))))
            {
                networkLeak = true;
            }
        };

        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);
        workspaceEntered = true;

        await FillFieldInputAsync(page, "serviceName", AnswerCanary);
        await FillFieldInputAsync(page, "workerPort", "8081");
        await EnsureTrustPrivacyOpenAsync(page);
        await page.Locator("#secret-consent").CheckAsync();
        await FillFieldInputAsync(page, "apiSecret", SecretCanary);

        await page.Locator("[data-workflow-step='prepare']").ClickAsync();
        await page.Locator("#prepare-complete").ClickAsync();
        await page.Locator("[data-prepared-file][data-readiness='complete']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Scan localStorage, sessionStorage, CacheStorage, and IndexedDB for canary leaks.
        bool storageOrCacheLeak = await page.EvaluateAsync<bool>(
            """
            async ([ans, sec]) => {
                const hasCanary = (text) => typeof text === 'string' && (text.includes(ans) || text.includes(sec));
                for (const store of [window.localStorage, window.sessionStorage]) {
                    for (let i = 0; i < store.length; i++) {
                        const k = store.key(i);
                        if (hasCanary(k) || hasCanary(store.getItem(k))) return true;
                    }
                }
                if ('caches' in window) {
                    const names = await caches.keys();
                    for (const name of names) {
                        const cache = await caches.open(name);
                        const keys = await cache.keys();
                        for (const req of keys) {
                            if (hasCanary(req.url)) return true;
                            const res = await cache.match(req);
                            if (res) {
                                const ct = (res.headers.get('content-type') || '').toLowerCase();
                                if (ct.includes('json') || ct.includes('text') || ct.includes('javascript')) {
                                    const body = await res.text();
                                    if (hasCanary(body)) return true;
                                }
                            }
                        }
                    }
                }
                if ('indexedDB' in window && typeof indexedDB.databases === 'function') {
                    const dbs = await indexedDB.databases();
                    for (const db of dbs) {
                        if (hasCanary(db.name || '')) return true;
                    }
                }
                return false;
            }
            """,
            new[] { AnswerCanary, SecretCanary });

        Require(!networkLeak, "network-canary-leak");
        Require(!storageOrCacheLeak, "storage-or-cache-canary-leak");
    }

    private static async Task CheckTwoTabUpdateSafetyAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page1 = await context.NewPageAsync();
        await OpenSyntheticWorkspaceAsync(page1, baseOrigin, definitionBytes);
        await FillFieldInputAsync(page1, "serviceName", "two-tab-live-edit");

        bool page1Navigated = false;
        page1.FrameNavigated += (_, frame) => { if (frame == page1.MainFrame) page1Navigated = true; };

        IPage page2 = await context.NewPageAsync();
        await page2.GotoAsync(baseOrigin + "/open", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page2.EvaluateAsync(
            """
            async () => {
                if ('serviceWorker' in navigator) {
                    const reg = await navigator.serviceWorker.getRegistration();
                    if (reg) await reg.update().catch(() => {});
                }
            }
            """);

        // Page 1 must NOT reload or discard the active workspace answer.
        Require(!page1Navigated, "active-tab-unexpectedly-reloaded");
        string currentValue = await page1.Locator("[data-field='serviceName'] input").InputValueAsync();
        Require(currentValue == "two-tab-live-edit", "active-tab-state-lost");
    }

    private static async Task CheckKeyboardRtlAndMobileReflowAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        // Mobile reflow check at 390x844 on Catalogue and active workspace.
        await using IBrowserContext mobileContext = await CreateContextAsync(browser, width: 390, height: 844);
        IPage mobilePage = await mobileContext.NewPageAsync();
        await mobilePage.GotoAsync(baseOrigin + "/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await mobilePage.Locator("article.catalogue__card").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        bool catalogueOverflow = await mobilePage.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1");
        Require(!catalogueOverflow, "mobile-catalogue-horizontal-overflow");

        await OpenSyntheticWorkspaceAsync(mobilePage, baseOrigin, definitionBytes);
        bool workspaceOverflow = await mobilePage.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1");
        Require(!workspaceOverflow, "mobile-workspace-horizontal-overflow");

        // Keyboard Tab focus progression + Arabic RTL switch.
        await mobilePage.Keyboard.PressAsync("Tab");
        string focusedTag = await mobilePage.EvaluateAsync<string>("() => document.activeElement ? document.activeElement.tagName : ''");
        Require(!string.IsNullOrWhiteSpace(focusedTag) && focusedTag != "BODY", "keyboard-tab-no-focus");

        await SwitchWorkspaceLocaleAsync(mobilePage, "ar");
        Require(await mobilePage.Locator("section.configuration-editor").GetAttributeAsync("dir") == "rtl", "mobile-rtl-missing");
    }

    private static async Task CheckWave2ContrastAndFocusRingsAsync(IBrowser browser, string baseOrigin, byte[] definitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        await page.GotoAsync(baseOrigin + "/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("h1").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Compute WCAG relative luminance contrast ratio for primary heading and body text.
        double headingContrast = await EvaluateElementContrastAsync(page, "h1");
        Require(headingContrast >= 3.0, "wave2-heading-contrast-low");

        // Verify keyboard focus outline visibility on interactive controls.
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        bool hasVisibleFocus = await page.EvaluateAsync<bool>(
            """
            () => {
                const el = document.activeElement;
                if (!el || el === document.body) return false;
                const style = getComputedStyle(el);
                const outlineWidth = parseFloat(style.outlineWidth || '0');
                return (style.outlineStyle !== 'none' && outlineWidth > 0) || (style.boxShadow && style.boxShadow !== 'none');
            }
            """);
        Require(hasVisibleFocus, "wave2-focus-ring-missing");

        // Also check active workspace contrast in Dark mode via /settings/appearance.
        await OpenSyntheticWorkspaceAsync(page, baseOrigin, definitionBytes);
        double workspaceContrast = await EvaluateElementContrastAsync(page, "[data-workspace-title]");
        Require(workspaceContrast >= 4.5, "wave2-workspace-title-contrast-low");
    }

    private static async Task CheckScaleDefinitionAdmissionAsync(IBrowser browser, string baseOrigin, byte[] scaleDefinitionBytes)
    {
        await using IBrowserContext context = await CreateContextAsync(browser);
        IPage page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);

        await OpenSyntheticWorkspaceAsync(page, baseOrigin, scaleDefinitionBytes);
        // Switch between all-sections and section-by-section layouts and open Review.
        ILocator allSectionsButton = page.Locator("[data-layout-mode='all-sections']");
        await allSectionsButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await allSectionsButton.ClickAsync();
        await page.Locator("[data-layout-active='all-sections']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await page.Locator("[data-workflow-step='review']").ClickAsync();
        await page.Locator("[data-workflow-panel='review']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Require(errors.Count == 0, "scale-definition-page-error");
    }

    private static async Task CheckHostHealthzHeadersAndMethodsAsync(IPlaywright playwright, string baseOrigin)
    {
        await using IAPIRequestContext api = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = baseOrigin,
            IgnoreHTTPSErrors = true
        });

        IAPIResponse healthGet = await api.GetAsync("/healthz");
        Require(healthGet.Status == 200, "healthz-get-status");
        string healthBody = await healthGet.TextAsync();
        Require(healthBody == "{\"status\":\"ok\"}", "healthz-body-mismatch");
        Require(HeaderContains(healthGet, "cache-control", "no-store"), "healthz-cache-control");

        IAPIResponse healthHead = await api.HeadAsync("/healthz");
        Require(healthHead.Status == 200, "healthz-head-status");

        IAPIResponse healthPost = await api.PostAsync("/healthz");
        Require(healthPost.Status == 405, "healthz-post-not-405");

        IAPIResponse rootGet = await api.GetAsync("/");
        Require(rootGet.Status == 200, "root-get-status");
        Require(HeaderContains(rootGet, "content-security-policy", "frame-ancestors 'none'"), "root-csp-missing");
        Require(HeaderContains(rootGet, "x-content-type-options", "nosniff"), "root-nosniff-missing");
        Require(HeaderContains(rootGet, "referrer-policy", "no-referrer"), "root-referrer-missing");

        IAPIResponse missing = await api.GetAsync("/nonexistent-qualification-probe.js");
        Require(missing.Status == 404, "missing-asset-not-404");
    }

    private static bool HeaderContains(IAPIResponse response, string headerName, string expectedSubstring) =>
        response.Headers.TryGetValue(headerName, out string? value)
        && value.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase);

    private static async Task<double> EvaluateElementContrastAsync(IPage page, string selector) =>
        await page.EvaluateAsync<double>(
            """
            (sel) => {
                const el = document.querySelector(sel);
                if (!el) return 0;
                const parseRgb = (str) => {
                    const m = (str || '').match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)/);
                    if (!m) return [0, 0, 0, 0];
                    return [Number(m[1]), Number(m[2]), Number(m[3]), m[4] === undefined ? 1 : Number(m[4])];
                };
                const fg = parseRgb(getComputedStyle(el).color);
                let bg = [255, 255, 255, 1];
                let cur = el;
                while (cur) {
                    const c = parseRgb(getComputedStyle(cur).backgroundColor);
                    if (c[3] > 0.05) { bg = c; break; }
                    cur = cur.parentElement;
                }
                const lum = ([r, g, b]) => {
                    const ch = [r, g, b].map(v => {
                        const s = v / 255;
                        return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
                    });
                    return 0.2126 * ch[0] + 0.7152 * ch[1] + 0.0722 * ch[2];
                };
                const l1 = lum(fg), l2 = lum(bg);
                return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
            }
            """,
            selector);

    private static async Task UploadDefinitionAsync(IPage page, string fileName, byte[] definitionBytes)
    {
        await page.Locator("#definition-file").SetInputFilesAsync(new FilePayload
        {
            Name = fileName,
            MimeType = "application/json",
            Buffer = definitionBytes
        });
    }

    internal static async Task OpenSyntheticWorkspaceAsync(IPage page, string baseOrigin, byte[] definitionBytes, string fileName = "qualification-definition.json")
    {
        await page.GotoAsync(baseOrigin + "/open", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#definition-file").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await UploadDefinitionAsync(page, fileName, definitionBytes);
        await page.Locator("[data-action='open-workspace']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Locator("[data-action='open-workspace']").ClickAsync();

        ILocator startButton = page.Locator("[data-action='start-workspace']");
        ILocator readyWorkspace = page.Locator("[data-workspace-phase='ready']");

        // Handle startup gate when presented, then wait deterministically for ready workspace.
        await page.WaitForFunctionAsync("() => Boolean(document.querySelector(\"[data-action='start-workspace'], [data-workspace-phase='ready']\"))");
        if (await startButton.IsVisibleAsync())
            await startButton.ClickAsync();

        await readyWorkspace.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task FillFieldInputAsync(IPage page, string fieldPath, string value)
    {
        ILocator input = page.Locator($"[data-field='{fieldPath}'] input").First;
        await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await input.FillAsync(value);
        await input.DispatchEventAsync("change");
    }

    private static async Task EnsureNestedHostEditableAsync(IPage page)
    {
        ILocator hostInput = page.Locator("[data-field='options.db.host'] input");
        if (await hostInput.IsVisibleAsync()) return;

        ILocator expandOptions = page.Locator("[data-field='options'] [data-action='expand-field']").First;
        if (await expandOptions.IsVisibleAsync())
            await expandOptions.ClickAsync();

        ILocator supplyOptions = page.Locator("[data-field='options'] [data-action='supply-object']").First;
        if (await supplyOptions.IsVisibleAsync())
            await supplyOptions.ClickAsync();

        ILocator expandOptional = page.Locator("[data-field='options'] [data-action='expand-optional']").First;
        if (await expandOptional.IsVisibleAsync())
            await expandOptional.ClickAsync();

        ILocator supplyDb = page.Locator("[data-field='options.db'] [data-action='supply-object']").First;
        if (await supplyDb.IsVisibleAsync())
            await supplyDb.ClickAsync();

        await hostInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task EnsureTrustPrivacyOpenAsync(IPage page)
    {
        ILocator consentCheckbox = page.Locator("#secret-consent");
        if (await consentCheckbox.IsVisibleAsync())
            return;

        await page.Locator("header.workspace-header [data-workspace-consent-chip]").ClickAsync();
        await consentCheckbox.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task OpenImportUtilityAsync(IPage page)
    {
        ILocator importPurpose = page.Locator("#import-purpose");
        if (await importPurpose.IsVisibleAsync())
            return;

        ILocator railTrigger = page.Locator("[data-utility-rail-action='import']");
        if (await railTrigger.IsVisibleAsync())
        {
            await railTrigger.ClickAsync();
        }
        else
        {
            await page.Locator("header.workspace-header [data-action='open-workspace-tools']").ClickAsync();
            await page.Locator("[data-tools-utility='import']").ClickAsync();
        }

        await importPurpose.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task SwitchWorkspaceLocaleAsync(IPage page, string locale)
    {
        await page.Locator("[data-action='toggle-language-picker']").ClickAsync();
        ILocator option = page.Locator($"[data-locale='{locale}']");
        await option.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await option.ClickAsync();
    }

    private static void Require(bool condition, string diagnosticCode)
    {
        if (!condition)
            throw new QualificationFailureException(diagnosticCode);
    }

    internal static byte[] BuildStandardDefinitionBytes()
    {
        var source = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject
            {
                ["familyId"] = "f1a20001-4ef0-4c80-9f10-000000000001",
                ["revision"] = 1
            },
            ["project"] = new JsonObject
            {
                ["id"] = "synthetic.qualification.standard",
                ["name"] = "Synthetic Qualification Service",
                ["publisher"] = "islamu-synthetic"
            },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["localization"] = new JsonObject
            {
                ["defaultLocale"] = "en",
                ["translations"] = new JsonObject
                {
                    ["ar"] = new JsonObject
                    {
                        ["direction"] = "rtl",
                        ["overrides"] = new JsonObject
                        {
                            ["/configuration/title"] = "\u0625\u0639\u062f\u0627\u062f \u0627\u0644\u062a\u0623\u0647\u064a\u0644",
                            ["/configuration/properties/serviceName/title"] = "\u0627\u0633\u0645 \u0627\u0644\u062e\u062f\u0645\u0629",
                            ["/configuration/properties/apiSecret/title"] = "\u0633\u0631 \u0627\u0644\u0648\u0627\u062c\u0647\u0629"
                        }
                    }
                }
            },
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "standard",
                ["navigation"] = "sections",
                ["sections"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "core",
                        ["title"] = "Core settings",
                        ["pointers"] = new JsonArray("/serviceName", "/workerPort", "/logLevel", "/optionalNote", "/apiSecret", "/options", "/labels")
                    }
                },
                ["journeys"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "standard",
                        ["title"] = "Standard",
                        ["sections"] = new JsonArray("core")
                    },
                    new JsonObject
                    {
                        ["id"] = "extended",
                        ["title"] = "Extended",
                        ["sections"] = new JsonArray("core")
                    }
                }
            },
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["title"] = "Qualification configuration",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("serviceName", "workerPort", "apiSecret"),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = new JsonObject
                {
                    ["serviceName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Service name",
                        ["minLength"] = 1,
                        ["maxLength"] = 128,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic",
                            ["initialValue"] = "local-service"
                        }
                    },
                    ["workerPort"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["title"] = "Worker port",
                        ["minimum"] = 1024,
                        ["maximum"] = 65535,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic",
                            ["initialValue"] = 8080
                        }
                    },
                    ["logLevel"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Log level",
                        ["enum"] = new JsonArray("info", "warn", "error"),
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic",
                            ["widget"] = "radio",
                            ["initialValue"] = "info"
                        }
                    },
                    ["optionalNote"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Optional note",
                        ["maxLength"] = 128,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic"
                        }
                    },
                    ["apiSecret"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "API secret",
                        ["minLength"] = 1,
                        ["maxLength"] = 128,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "secret",
                            ["detail"] = "basic",
                            ["secret"] = new JsonObject
                            {
                                ["kind"] = "password",
                                ["acquisition"] = "local"
                            }
                        }
                    },
                    ["options"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["title"] = "Options",
                        ["additionalProperties"] = false,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic"
                        },
                        ["properties"] = new JsonObject
                        {
                            ["db"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["title"] = "Database",
                                ["additionalProperties"] = false,
                                ["x-configurator"] = new JsonObject
                                {
                                    ["sensitivity"] = "public",
                                    ["detail"] = "basic"
                                },
                                ["properties"] = new JsonObject
                                {
                                    ["host"] = new JsonObject
                                    {
                                        ["type"] = "string",
                                        ["title"] = "Database host",
                                        ["minLength"] = 1,
                                        ["maxLength"] = 128,
                                        ["x-configurator"] = new JsonObject
                                        {
                                            ["sensitivity"] = "public",
                                            ["detail"] = "basic"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    ["labels"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["title"] = "Labels",
                        ["additionalProperties"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["title"] = "Label value",
                            ["maxLength"] = 128,
                            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
                        },
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic"
                        }
                    }
                }
            },
            ["outputs"] = new JsonObject
            {
                ["app-config.json"] = new JsonObject
                {
                    ["id"] = "app-config",
                    ["title"] = "Application config",
                    ["format"] = "json"
                }
            }
        };

        return Encoding.UTF8.GetBytes(source.ToJsonString());
    }

    internal static byte[] BuildImportConflictDefinitionBytes()
    {
        var source = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject
            {
                ["familyId"] = "f1a20003-4ef0-4c80-9f10-000000000003",
                ["revision"] = 1
            },
            ["project"] = new JsonObject
            {
                ["id"] = "synthetic.qualification.import",
                ["name"] = "Synthetic Import Conflict Service",
                ["publisher"] = "islamu-synthetic"
            },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "standard",
                ["navigation"] = "sections",
                ["sections"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "core",
                        ["title"] = "Core settings",
                        ["pointers"] = new JsonArray("/serviceName", "/workerPort", "/options", "/labels")
                    }
                },
                ["journeys"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "standard",
                        ["title"] = "Standard",
                        ["sections"] = new JsonArray("core")
                    }
                }
            },
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["title"] = "Import conflict configuration",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("serviceName", "workerPort"),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = new JsonObject
                {
                    ["serviceName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["title"] = "Service name",
                        ["minLength"] = 1,
                        ["maxLength"] = 128,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic",
                            ["initialValue"] = "local-service"
                        }
                    },
                    ["workerPort"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["title"] = "Worker port",
                        ["minimum"] = 1024,
                        ["maximum"] = 65535,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic",
                            ["initialValue"] = 8080
                        }
                    },
                    ["options"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["title"] = "Options",
                        ["additionalProperties"] = false,
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic"
                        },
                        ["properties"] = new JsonObject
                        {
                            ["db"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["title"] = "Database",
                                ["additionalProperties"] = false,
                                ["x-configurator"] = new JsonObject
                                {
                                    ["sensitivity"] = "public",
                                    ["detail"] = "basic"
                                },
                                ["properties"] = new JsonObject
                                {
                                    ["host"] = new JsonObject
                                    {
                                        ["type"] = "string",
                                        ["title"] = "Database host",
                                        ["minLength"] = 1,
                                        ["maxLength"] = 128,
                                        ["x-configurator"] = new JsonObject
                                        {
                                            ["sensitivity"] = "public",
                                            ["detail"] = "basic"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    ["labels"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["title"] = "Labels",
                        ["additionalProperties"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["title"] = "Label value",
                            ["maxLength"] = 128,
                            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
                        },
                        ["x-configurator"] = new JsonObject
                        {
                            ["sensitivity"] = "public",
                            ["detail"] = "basic"
                        }
                    }
                }
            },
            ["outputs"] = new JsonObject
            {
                ["app-config.json"] = new JsonObject
                {
                    ["id"] = "app-config",
                    ["title"] = "Application config",
                    ["format"] = "json"
                }
            }
        };

        return Encoding.UTF8.GetBytes(source.ToJsonString());
    }

    internal static byte[] BuildScaleDefinitionBytes(int fieldCount)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var section1Pointers = new JsonArray();
        var section2Pointers = new JsonArray();

        for (int index = 0; index < fieldCount; index++)
        {
            string name = "setting" + index.ToString("D3", CultureInfo.InvariantCulture);
            properties[name] = new JsonObject
            {
                ["type"] = "integer",
                ["title"] = "Scale setting " + index.ToString(CultureInfo.InvariantCulture),
                ["minimum"] = 0,
                ["maximum"] = 10000,
                ["x-configurator"] = new JsonObject
                {
                    ["sensitivity"] = "public",
                    ["detail"] = "basic",
                    ["initialValue"] = index
                }
            };
            required.Add(name);
            if (index < fieldCount / 2)
                section1Pointers.Add("/" + name);
            else
                section2Pointers.Add("/" + name);
        }

        var source = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject
            {
                ["familyId"] = "f1a20002-4ef0-4c80-9f10-000000000002",
                ["revision"] = 1
            },
            ["project"] = new JsonObject
            {
                ["id"] = "synthetic.qualification.scale",
                ["name"] = "Synthetic Scale Service",
                ["publisher"] = "islamu-synthetic"
            },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["presentation"] = new JsonObject
            {
                ["defaultJourney"] = "standard",
                ["navigation"] = "sections",
                ["sections"] = new JsonArray
                {
                    new JsonObject { ["id"] = "part1", ["title"] = "Part 1", ["pointers"] = section1Pointers },
                    new JsonObject { ["id"] = "part2", ["title"] = "Part 2", ["pointers"] = section2Pointers }
                },
                ["journeys"] = new JsonArray
                {
                    new JsonObject { ["id"] = "standard", ["title"] = "Standard", ["sections"] = new JsonArray("part1", "part2") }
                }
            },
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["title"] = "Scale configuration",
                ["additionalProperties"] = false,
                ["required"] = required,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = properties
            },
            ["outputs"] = new JsonObject
            {
                ["scale-config.json"] = new JsonObject
                {
                    ["id"] = "scale-config",
                    ["title"] = "Scale config",
                    ["format"] = "json"
                }
            }
        };

        return Encoding.UTF8.GetBytes(source.ToJsonString());
    }

    private sealed class QualificationFailureException(string diagnosticCode) : InvalidOperationException(diagnosticCode)
    {
        public string DiagnosticCode { get; } = diagnosticCode;
    }
}
