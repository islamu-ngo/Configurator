namespace Configurator.Browser.Tests;

using System.Reflection;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Configurator.Browser.Profiles;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Core.Profiles;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

[NotInParallel]
public sealed partial class BrowserProfileVaultTests
{
    [Test]
    public async Task OpeningMetadataRequiresConsentAndPreservesExistingAnswersOnRefusal()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.SetValue(DefinitionPath.Parse("/endpoint"), JsonSerializer.SerializeToElement("current"));

        bool adopted = form.AdoptBrowserProfile(form.InputRevision, Guid.NewGuid(),
            ProfileProtectionMode.ImportUpgradeOnly, PublicValues(), [DefinitionPath.Parse("/token")], () => { }, () => { });

        await Assert.That(adopted).IsFalse();
        await Assert.That(form.Snapshot().Values["endpoint"].GetString()).IsEqualTo("current");
        await Assert.That(form.BrowserProfile).IsNull();
    }

    [Test]
    [Arguments(ProfileProtectionMode.ImportUpgradeOnly)]
    [Arguments(ProfileProtectionMode.InspectableEncrypted)]
    public async Task SafeOpeningBlocksRetainedEditsAndNeverEnablesOrdinaryReadiness(ProfileProtectionMode mode)
    {
        using var form = Opened(mode);

        bool changed = form.SetValue(DefinitionPath.Parse("/token"), JsonSerializer.SerializeToElement("synthetic-replacement"));

        await Assert.That(changed).IsFalse();
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/token"))).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        await Assert.That(form.RequiresBrowserProfileComposition).IsTrue();
        await Assert.That(form.CanUndo).IsFalse();
    }

    [Test]
    public async Task ReplacementStaysBlockedUntilWorkerRetirementCompletes()
    {
        using var form = Opened(ProfileProtectionMode.ImportUpgradeOnly);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<DefinitionPath>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> replacement = form.ReplaceBrowserRetainedValueAsync(DefinitionPath.Parse("/token"),
            JsonSerializer.SerializeToElement("synthetic-replacement"), true, (_, _) =>
            {
                entered.TrySetResult();
                return release.Task;
            });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/token"))).IsFalse();
        release.SetResult([]);
        await Assert.That(await replacement.WaitAsync(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/token"))).IsTrue();
        await Assert.That(form.Snapshot().Values["token"].GetString()).IsEqualTo("synthetic-replacement");
        form.Undo();
        await Assert.That(form.BrowserProfile!.RetainedPaths).IsEmpty();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
    }

    [Test]
    public async Task OrdinaryEditRejectsAnInFlightRetirementReply()
    {
        using var form = Opened(ProfileProtectionMode.ImportUpgradeOnly);
        var release = new TaskCompletionSource<IReadOnlyList<DefinitionPath>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> replacement = form.ReplaceBrowserRetainedValueAsync(DefinitionPath.Parse("/token"),
            JsonSerializer.SerializeToElement("synthetic-replacement"), true, (_, _) => release.Task);
        form.SetValue(DefinitionPath.Parse("/endpoint"), JsonSerializer.SerializeToElement("new-endpoint"));
        release.SetResult([]);

        await Assert.That(await replacement.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/token"))).IsFalse();
    }

    [Test]
    public async Task UnconfirmedReplacementDoesNotConsumeWorkerAuthority()
    {
        using var form = Opened(ProfileProtectionMode.ImportUpgradeOnly);
        bool called = false;
        bool replaced = await form.ReplaceBrowserRetainedValueAsync(DefinitionPath.Parse("/token"), null, false,
            (_, _) =>
            {
                called = true;
                return Task.FromResult<IReadOnlyList<DefinitionPath>>([]);
            });
        await Assert.That(replaced).IsFalse();
        await Assert.That(called).IsFalse();
        await Assert.That(form.CanEdit(DefinitionPath.Parse("/token"))).IsFalse();
    }

    [Test]
    [Arguments(ProfileProtectionMode.ImportUpgradeOnly, false)]
    [Arguments(ProfileProtectionMode.InspectableEncrypted, true)]
    public async Task OnlyExplicitInspectableDisclosureCanAdoptOrdinarySecrets(ProfileProtectionMode mode, bool expected)
    {
        int retired = 0;
        using var form = Opened(mode, () => retired++);
        Guid reference = form.BrowserProfile!.Reference;
        var disclosed = new AnswerSnapshot(form.InputRevision, new Dictionary<string, JsonElement>
        {
            ["endpoint"] = JsonSerializer.SerializeToElement("old-endpoint"),
            ["token"] = JsonSerializer.SerializeToElement("synthetic-disclosed-secret")
        });

        bool adopted = form.AdoptBrowserProfileDisclosure(reference, form.InputRevision, disclosed);

        await Assert.That(adopted).IsEqualTo(expected);
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsEqualTo(expected);
        await Assert.That(form.RequiresBrowserProfileComposition).IsEqualTo(!expected);
        await Assert.That(retired).IsEqualTo(expected ? 1 : 0);
        await Assert.That(form.CanUndo).IsFalse();
    }

    [Test]
    public async Task RevocationRetiresWorkerAndRejectsLateDisclosureWithoutClaimingCompleteness()
    {
        int retired = 0;
        using var form = Opened(ProfileProtectionMode.InspectableEncrypted, () => retired++);
        Guid reference = form.BrowserProfile!.Reference;
        long revision = form.InputRevision;
        var disclosed = new AnswerSnapshot(revision, new Dictionary<string, JsonElement>
        {
            ["token"] = JsonSerializer.SerializeToElement("synthetic-late-secret")
        });
        form.SetSecretEntryEnabled(false);

        await Assert.That(form.AdoptBrowserProfileDisclosure(reference, revision, disclosed)).IsFalse();
        await Assert.That(retired).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        await Assert.That(form.RequiresBrowserProfileComposition).IsTrue();
    }

    [Test]
    public async Task InputMutationClosesPrivateOutputBeforeReturning()
    {
        bool outputOpen = false;
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.AdoptBrowserProfile(form.InputRevision, Guid.NewGuid(), ProfileProtectionMode.ImportUpgradeOnly,
            PublicValues(), [DefinitionPath.Parse("/token")], () => { }, () => outputOpen = false);
        outputOpen = true;
        form.SetValue(DefinitionPath.Parse("/endpoint"), JsonSerializer.SerializeToElement("new-endpoint"));
        await Assert.That(outputOpen).IsFalse();
    }

    [Test]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task ComparisonFileWorksWithoutSecretConsentOrWorkerTransfer(string locale)
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.SelectLocale(locale);
        form.SetValue(DefinitionPath.Parse("/endpoint"), JsonSerializer.SerializeToElement("new-endpoint"));
        CompareBaselineSnapshot baseline = CompareBaselineCodec.Create(form.Definition,
            new AnswerSnapshot(0, new Dictionary<string, JsonElement> { ["endpoint"] = JsonSerializer.SerializeToElement("old-endpoint") }),
            Guid.NewGuid(), Guid.NewGuid(), "browser-workspace");
        byte[] bytes = CompareBaselineCodec.Serialize(baseline);
        ProfileWorkerAuthority Authority() => new(Guid.Parse(BrowserFixture.Family), 1, form.InputRevision, 1, false);
        await using var controller = new BrowserProfileVaultController(
            form,
            context.Services.GetRequiredService<IJSRuntime>(),
            context.Services.GetRequiredService<BrowserDefinitionRegistry>(),
            Authority);
        var panel = context.Render<ConfigurationVaultPanel>(parameters => parameters
            .Add(item => item.Controller, controller));

        var input = panel.FindComponents<InputFile>().Single(component =>
            component.Instance.AdditionalAttributes?["id"]?.ToString() == "profile-comparison-file");
        await panel.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary(bytes, "synthetic.cfgcompare.json")));

        await Assert.That(panel.Find("[data-profile-status]").GetAttribute("data-profile-status")).IsEqualTo("ProfileComparisonReady");
        await Assert.That(panel.Find("table").TextContent).Contains(BrowserStrings.Get("ProfileComparisonChanged", locale));
        await Assert.That(panel.Find("#profile-open-passphrase").Closest("fieldset")!.HasAttribute("disabled")).IsTrue();
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier == "import" &&
            call.Arguments.Any(argument => Equals(argument, "./js/profile-worker-host.js")))).IsFalse();
    }

    [Test]
    [Arguments("en", ProfileProtectionMode.ImportUpgradeOnly, false)]
    [Arguments("ar", ProfileProtectionMode.InspectableEncrypted, true)]
    public async Task ProfilePanelExposesOnlyModePermittedActions(string locale, ProfileProtectionMode mode, bool inspect)
    {
        await using var context = BrowserFixture.Context();
        using var form = Opened(mode);
        form.SelectLocale(locale);
        ProfileWorkerAuthority Authority() => new(Guid.Parse(BrowserFixture.Family), 1, form.InputRevision, 1, true);
        await using var controller = new BrowserProfileVaultController(
            form,
            context.Services.GetRequiredService<IJSRuntime>(),
            context.Services.GetRequiredService<BrowserDefinitionRegistry>(),
            Authority);
        var panel = context.Render<ConfigurationVaultPanel>(parameters => parameters
            .Add(item => item.Controller, controller));

        await Assert.That(panel.FindAll("[data-action='profile-inspect']").Count).IsEqualTo(inspect ? 1 : 0);
        await Assert.That(panel.FindAll("[data-action='profile-download-private']").Count).IsEqualTo(1);
        await Assert.That(panel.Find("[data-profile-incomplete]").TextContent).Contains(BrowserStrings.Get("ProfilePrivateOutputRequired", locale));
        await Assert.That(panel.Find("#profile-open-passphrase").GetAttribute("type")).IsEqualTo("password");
        await Assert.That(panel.FindAll("[data-profile-replace]").Single().TextContent).Contains(BrowserStrings.Get("ProfileReplace", locale));
        await Assert.That(panel.FindAll("pre").Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task VaultDisplaysSequentialStepsCollapsedCryptoDetailsAndPassphraseToggle(string locale)
    {
        await using var context = BrowserFixture.Context();
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.SelectLocale(locale);
        ProfileWorkerAuthority Authority() => new(Guid.Parse(BrowserFixture.Family), 1, form.InputRevision, 1, true);
        await using var controller = new BrowserProfileVaultController(
            form,
            context.Services.GetRequiredService<IJSRuntime>(),
            context.Services.GetRequiredService<BrowserDefinitionRegistry>(),
            Authority);
        var panel = context.Render<ConfigurationVaultPanel>(parameters => parameters
            .Add(item => item.Controller, controller));

        var steps = panel.Find("[data-vault-steps]");
        await Assert.That(steps.QuerySelectorAll("li").Length).IsEqualTo(3);
        var cryptoDetails = panel.Find("[data-vault-crypto-details]");
        await Assert.That(cryptoDetails.HasAttribute("open")).IsFalse();
        await Assert.That(cryptoDetails.TextContent).Contains("JWE");

        var passphraseInput = panel.Find("#profile-open-passphrase");
        var toggle = panel.Find("[data-passphrase-toggle='profile-open-passphrase']");
        await Assert.That(passphraseInput.GetAttribute("type")).IsEqualTo("password");
        await Assert.That(toggle.GetAttribute("aria-pressed")).IsEqualTo("false");

        await toggle.ClickAsync(new MouseEventArgs());
        await Assert.That(panel.Find("#profile-open-passphrase").GetAttribute("type")).IsEqualTo("text");
        await Assert.That(panel.Find("[data-passphrase-toggle='profile-open-passphrase']").GetAttribute("aria-pressed")).IsEqualTo("true");

        await panel.InvokeAsync(() => form.SetValue(DefinitionPath.Parse("/endpoint"), JsonSerializer.SerializeToElement("changed")));
        await Assert.That(panel.Find("#profile-open-passphrase").GetAttribute("type")).IsEqualTo("password");
        await Assert.That(panel.Find("[data-passphrase-toggle='profile-open-passphrase']").GetAttribute("aria-pressed")).IsEqualTo("false");
    }

    [Test]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task RetainedFieldExplainsProfileCustodyInsteadOfRequestingAlreadyGrantedConsent(string locale)
    {
        await using var context = BrowserFixture.Context();
        var editor = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, Definition()));
        await BrowserFixture.SetSecretConsentAsync(editor, true);
        var form = editor.FindComponents<ConfigurationField>()
            .Single(field => field.Instance.Path.SequenceEqual(["token"])).Instance.Form;
        bool admitted = false;
        await editor.InvokeAsync(() =>
        {
            form.SelectLocale(locale);
            admitted = form.AdoptBrowserProfile(form.InputRevision, Guid.NewGuid(),
                ProfileProtectionMode.ImportUpgradeOnly, PublicValues(),
                [DefinitionPath.Parse("/token")], () => { }, () => { });
        });

        await Assert.That(admitted).IsTrue();
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserSecretsEnabled);
        var field = editor.Find("[data-field='token']");
        await Assert.That(field.QuerySelectorAll("[data-secret-denied]").Length).IsEqualTo(0);
        await Assert.That(field.QuerySelectorAll("[data-action='field-secret-consent']").Length).IsEqualTo(0);
        await Assert.That(field.QuerySelectorAll("[data-profile-retained]").Length).IsEqualTo(1);
        await Assert.That(field.QuerySelectorAll("input").Length).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
    }

    [Test]
    public async Task RemovedIdentityStillRejectsConflictingProfileAndCatalogueAdmission()
    {
        Definition original = BrowserFixture.Definition;
        var registry = new BrowserDefinitionRegistry([original]);
        registry.Replace([]);
        ArtifactDigest conflict = ArtifactDigest.Compute("synthetic-different-content"u8);

        await Assert.That(() => registry.AdmitHistoricalIdentity(original.FamilyId, original.Revision, conflict))
            .Throws<DefinitionException>();
        registry.AdmitHistoricalIdentity(original.FamilyId, original.Revision, original.Digest);
        registry.Admit(original);
        await Assert.That(registry.Current.Definitions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CatalogueAndProfileHistoryShareOne4096IdentityBudgetIncludingRemovedEntries()
    {
        Definition original = BrowserFixture.Definition;
        var registry = new BrowserDefinitionRegistry([original]);
        registry.Replace([]);
        for (long revision = 2; revision <= 4096; revision++)
            registry.AdmitHistoricalIdentity(original.FamilyId, revision, ArtifactDigest.Compute("synthetic-history"u8));

        registry.AdmitHistoricalIdentity(original.FamilyId, original.Revision, original.Digest);
        registry.Admit(original);
        await Assert.That(() => registry.AdmitHistoricalIdentity(Guid.NewGuid(), 1, original.Digest)).Throws<DefinitionException>();
        await Assert.That(() => registry.Admit(BrowserFixture.Revision(4097))).Throws<DefinitionException>();
        await Assert.That(registry.Current.Definitions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task WorkspaceNavigationGuardExplainsTransientCustodyAndPreservesDirtyWorkOnCancel()
    {
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        await using var context = BrowserFixture.Context(registry);
        var navigation = new RendererNavigationManager(context);
        context.Services.AddSingleton<NavigationManager>(navigation);
        navigation.NavigateTo($"/launch/{BrowserFixture.Family}?appVersion=1.0");
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        var component = context.Render<App>();
        await Assert.That(component.Find("[data-session-loss-guard]").GetAttribute("data-session-loss-guard")).IsEqualTo("idle");

        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        await Assert.That(component.Find("[data-session-loss-guard]").GetAttribute("data-session-loss-guard")).IsEqualTo("active");

        await component.InvokeAsync(() => navigation.NavigateTo("/"));
        var dialog = component.Find("[role='dialog']");
        await Assert.That(dialog.TextContent).Contains(".cfgvault");
        await component.Find("[data-action='keep-workspace']").ClickAsync(new MouseEventArgs());
        await navigation.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("example.test");
        await Assert.That(component.Find("[data-session-loss-guard]").GetAttribute("data-session-loss-guard")).IsEqualTo("active");
    }

    private static DefinitionFormModel Opened(ProfileProtectionMode mode, Action? retire = null)
    {
        var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.AdoptBrowserProfile(form.InputRevision, Guid.NewGuid(), mode, PublicValues(),
            [DefinitionPath.Parse("/token")], retire ?? (() => { }), () => { });
        return form;
    }

    private static JsonElement PublicValues() => JsonSerializer.SerializeToElement(new { endpoint = "old-endpoint" });

    private static Definition Definition() => DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"b1fc3999-8668-4d78-8a66-b6c9cd5f30da","revision":1},
         "project":{"id":"synthetic.browser-profiles","name":"Synthetic Browser profiles"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,
          "x-configurator":{"sensitivity":"public"},"required":["token","endpoint"],"properties":{
           "token":{"type":"string","x-configurator":{"sensitivity":"secret"}},
           "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
         "outputs":{"settings.json":{"format":"json"}},
         "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}}
        """u8.ToArray()));
}
