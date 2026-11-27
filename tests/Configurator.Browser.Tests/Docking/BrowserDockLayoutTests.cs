namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Components.Docking;
using Configurator.Browser.Layout;
using Configurator.Browser.Services.Docking;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

public sealed class BrowserDockLayoutTests
{
    private static readonly RenderFragment EmptyContent = static builder => { };

    [Test]
    public async Task DockLayoutStateEnforcesStartAndEndWidthDefaultsAndClamping()
    {
        var state = new DockLayoutState();
        state.Register(BrowserDockPanels.StartNav, EmptyContent);
        state.Register(BrowserDockPanels.Inspector, EmptyContent);
        state.Register(BrowserDockPanels.DocumentJson, EmptyContent);

        DockPanelEntry start = state.GetPanel(BrowserDockPanels.StartNavId)!;
        DockPanelEntry inspector = state.GetPanel(BrowserDockPanels.InspectorId)!;
        DockPanelEntry docJson = state.GetPanel(BrowserDockPanels.DocumentJsonId)!;

        await Assert.That(start.State.Width).IsEqualTo(256);
        await Assert.That(start.Descriptor.MinWidth).IsEqualTo(240);
        await Assert.That(start.Descriptor.MaxWidth).IsEqualTo(460);

        await Assert.That(inspector.State.Width).IsEqualTo(360);
        await Assert.That(inspector.Descriptor.MinWidth).IsEqualTo(300);
        await Assert.That(inspector.Descriptor.MaxWidth).IsEqualTo(620);

        await Assert.That(docJson.State.Width).IsEqualTo(360);

        state.Resize(BrowserDockPanels.StartNavId, 120);
        await Assert.That(state.GetPanel(BrowserDockPanels.StartNavId)!.State.Width).IsEqualTo(240);
        state.Resize(BrowserDockPanels.StartNavId, 900);
        await Assert.That(state.GetPanel(BrowserDockPanels.StartNavId)!.State.Width).IsEqualTo(460);

        state.Resize(BrowserDockPanels.InspectorId, 100);
        await Assert.That(state.GetPanel(BrowserDockPanels.InspectorId)!.State.Width).IsEqualTo(300);
        state.Resize(BrowserDockPanels.InspectorId, 999);
        await Assert.That(state.GetPanel(BrowserDockPanels.InspectorId)!.State.Width).IsEqualTo(620);
    }

    [Test]
    [Arguments(DockSide.Start, false, "ArrowRight", 256, 272)]
    [Arguments(DockSide.Start, false, "ArrowLeft", 256, 240)]
    [Arguments(DockSide.End, false, "ArrowLeft", 360, 376)]
    [Arguments(DockSide.End, false, "ArrowRight", 360, 344)]
    [Arguments(DockSide.Start, true, "ArrowLeft", 256, 272)]
    [Arguments(DockSide.Start, true, "ArrowRight", 256, 240)]
    [Arguments(DockSide.End, true, "ArrowRight", 360, 376)]
    [Arguments(DockSide.End, true, "ArrowLeft", 360, 344)]
    public async Task DockResizeHandleInvertsDeltaInRtlForKeyboardStepResize(
        DockSide side,
        bool isRtl,
        string key,
        int initialWidth,
        int expectedWidth)
    {
        await using BunitContext context = BrowserFixture.Context();
        int? requested = null;
        int min = side == DockSide.Start ? 240 : 300;
        int max = side == DockSide.Start ? 460 : 620;
        IRenderedComponent<DockResizeHandle> handle = context.Render<DockResizeHandle>(parameters => parameters
            .Add(p => p.PanelId, side == DockSide.Start ? BrowserDockPanels.StartNavId : BrowserDockPanels.InspectorId)
            .Add(p => p.Side, side)
            .Add(p => p.Width, initialWidth)
            .Add(p => p.MinWidth, min)
            .Add(p => p.MaxWidth, max)
            .Add(p => p.IsRtl, isRtl)
            .Add(p => p.OnResizeRequested, EventCallback.Factory.Create<int>(this, width => requested = width)));

        await handle.Find("[role='separator']").KeyDownAsync(new KeyboardEventArgs { Key = key });

        await Assert.That(requested).IsEqualTo(expectedWidth);
    }

    [Test]
    public async Task DockLayoutDegradesToOverlayAtMobileBreakpointAndBelow420PxContentFloor()
    {
        var state = new DockLayoutState();
        state.Register(BrowserDockPanels.StartNav, EmptyContent);
        state.Register(BrowserDockPanels.Inspector, EmptyContent);
        state.Open(BrowserDockPanels.StartNavId);
        state.Open(BrowserDockPanels.InspectorId);

        // 1280 - 256 - 360 = 664px >= 420px content floor -> both docked.
        state.UpdateViewport(1280, isMobile: false, DockScope.Workspace, minimumContentWidth: 420);
        await Assert.That(state.ViewportWidth).IsEqualTo(1280);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // 1000 - 256 - 360 = 384px < 420px content floor -> lower-priority start panel auto-degrades to overlay.
        state.UpdateViewport(1000, isMobile: false, DockScope.Workspace, minimumContentWidth: 420);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // 760px mobile breakpoint -> overlay mode everywhere.
        state.UpdateViewport(760, isMobile: true, DockScope.Shell, minimumContentWidth: 420);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsTrue();

        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<Configurator.Browser.Layout.MainLayout> shell = context.Render<Configurator.Browser.Layout.MainLayout>(parameters =>
            parameters.Add(item => item.Body, builder => builder.AddContent(0, "Workspace")));
        await Assert.That(shell.FindAll("#application-navigation .nav-menu__workspace")).Count().IsEqualTo(1);
        await Assert.That(shell.FindAll("#application-navigation .nav-menu__footer .nav-menu__local")).Count().IsEqualTo(1);
        await Assert.That(shell.FindAll("#application-navigation .nav-menu__sections")).IsEmpty();
    }

    [Test]
    public async Task UtilityRailSwitchesBetweenSingleActiveInspectorAndDocumentJsonWithRovingFocus()
    {
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<ConfigurationEditor> editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));

        await editor.Find("[data-utility-rail-action='inspector']").ClickAsync(new MouseEventArgs());

        await Assert.That(editor.FindAll("[data-testid='dock-tab-strip-tab']")).IsEmpty();
        await Assert.That(editor.Find("[data-utility-rail-action='inspector']").GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(editor.Find("[data-utility-rail-action='document']").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(editor.FindAll("[data-inspector-pane]")).Count().IsEqualTo(1);

        await editor.Find("[data-workspace-utility-rail] [role='toolbar']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await Assert.That(editor.Find("[data-utility-rail-action='document']").GetAttribute("tabindex")).IsEqualTo("0");

        await editor.Find("[data-utility-rail-action='document']").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("[data-utility-rail-action='inspector']").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(editor.Find("[data-utility-rail-action='document']").GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(editor.FindAll("[data-developer-panel]")).Count().IsEqualTo(1);
        await Assert.That(editor.FindAll("[data-inspector-pane]")).IsEmpty();

        await editor.Find("[data-utility-rail-action='inspector']").ClickAsync(new MouseEventArgs());
        await Assert.That(editor.Find("[data-utility-rail-action='inspector']").GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(editor.FindAll("[data-developer-panel]")).IsEmpty();
    }

    [Test]
    public async Task ShellStartDockHostsStickyFooterAndUtilityRailLeadsNonFormUtilities()
    {
        await using BunitContext context = BrowserFixture.Context();
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        IRenderedComponent<App> app = context.Render<App>();

        await Assert.That(app.FindAll("[data-dock-scope='shell'] [data-dock-panel-id='shell.start-nav']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("#application-navigation .nav-menu__footer")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-configure-ribbon]")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-configure-ribbon] [data-action='undo']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-configure-ribbon] [data-action='redo']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-configure-ribbon] [data-action='toggle-naming-mode']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-configure-ribbon] #configuration-density")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-configure-ribbon] [data-action='toggle-inspector']")).IsEmpty();
        await Assert.That(app.FindAll("[data-configure-ribbon] [data-editing-mode='developer']")).IsEmpty();
        await Assert.That(app.FindAll("[data-utility-rail-action='inspector']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-utility-rail-action='document']")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DockLayoutStateAllocatesCrossScopePanelsByPriorityAndClearsFloorOnUnregister()
    {
        await Assert.That(BrowserDockPanels.StartNav.DefaultWidth).IsEqualTo(256);
        await Assert.That(BrowserDockPanels.StartNav.MinWidth).IsEqualTo(240);
        await Assert.That(BrowserDockPanels.StartNav.MaxWidth).IsEqualTo(460);

        var state = new DockLayoutState();
        state.Register(BrowserDockPanels.StartNav, EmptyContent);
        state.Register(BrowserDockPanels.Inspector, EmptyContent);
        state.Open(BrowserDockPanels.StartNavId);
        state.Open(BrowserDockPanels.InspectorId);

        DockPanelEntry startNav = state.GetPanel(BrowserDockPanels.StartNavId)!;
        DockPanelEntry inspector = state.GetPanel(BrowserDockPanels.InspectorId)!;

        // 1440px with 790px Workspace floor: 1440 - 256 - 360 = 824 >= 790 -> both docked.
        state.UpdateViewport(1440, isMobile: false, DockScope.Shell, minimumContentWidth: 420);
        state.UpdateViewport(1440, isMobile: false, DockScope.Workspace, minimumContentWidth: 790);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(startNav)).IsFalse();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(inspector)).IsFalse();

        // 1280px with 790px Workspace floor: StartNav (priority 10) yields to overlay; Inspector (priority 20) stays docked (920 >= 790).
        state.UpdateViewport(1280, isMobile: false, DockScope.Workspace, minimumContentWidth: 790);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // 1149px Worst Break boundary: Inspector (360) cannot fit (1149 - 360 = 789 < 790), freeing width so StartNav (256) stays docked (893 >= 790).
        state.UpdateViewport(1149, isMobile: false, DockScope.Workspace, minimumContentWidth: 790);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        // 1050px with 790px Workspace floor: Inspector overlays (690 < 790), StartNav stays docked (794 >= 790).
        state.UpdateViewport(1050, isMobile: false, DockScope.Workspace, minimumContentWidth: 790);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        // Leaving Workspace unregisters Workspace panels and clears stale 790px floor back to 420px Shell floor.
        state.Unregister(BrowserDockPanels.InspectorId);
        state.UpdateViewport(960, isMobile: false, DockScope.Shell, minimumContentWidth: 420);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        // 760px mobile breakpoint overlays StartNav.
        state.UpdateViewport(760, isMobile: true, DockScope.Shell, minimumContentWidth: 420);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
    }

    [Test]
    public async Task DualScopeDockLayoutHostsPreserveViewportWidthAcrossAsymmetricResizeAndEditorRerenders()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        document["presentation"] = JsonNode.Parse("""
            {"defaultJourney":"quick-start","navigation":"wizard","sections":[
              {"id":"connection","title":"General","pointers":["/mode","/replicas","/hostname"]},
              {"id":"credentials","title":"Security","pointers":["/credential"]}],
             "journeys":[
              {"id":"quick-start","title":"Connect service",
               "sections":["connection","credentials"]}]}
            """);
        Definition groupedDefinition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));

        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<MainLayout> app = context.Render<MainLayout>(
            parameters => parameters.Add(item => item.Body, builder =>
            {
                builder.OpenComponent<ConfigurationEditor>(0);
                builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), groupedDefinition);
                builder.CloseComponent();
            }));
        DockLayoutState dockState = context.Services.GetRequiredService<DockLayoutState>();

        await app.Find("[data-utility-rail-action='inspector']").ClickAsync(new MouseEventArgs());

        IRenderedComponent<DockLayoutHost> shellHost = app.FindComponents<DockLayoutHost>()
            .Single(host => host.Instance.Scope == DockScope.Shell);
        IRenderedComponent<DockLayoutHost> workspaceHost = app.FindComponents<DockLayoutHost>()
            .Single(host => host.Instance.Scope == DockScope.Workspace);
        IRenderedComponent<ConfigurationEditor> editor = app.FindComponent<ConfigurationEditor>();

        // Multi-section Comfortable Configure + 44px desktop utility rail => 790 + 44 = 834px.
        await Assert.That(workspaceHost.Instance.MinimumContentWidth).IsEqualTo(834);

        var shellObserver = (IBrowserViewportObserver)shellHost.Instance;
        var workspaceObserver = (IBrowserViewportObserver)workspaceHost.Instance;

        await app.InvokeAsync(async () =>
        {
            await shellObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
                shellObserver.Id,
                new BrowserWindowSize { Width = 1100, Height = 800 },
                Breakpoint.Md,
                isImmediate: true));
            await workspaceObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
                workspaceObserver.Id,
                new BrowserWindowSize { Width = 1100, Height = 800 },
                Breakpoint.Md,
                isImmediate: true));
        });
        await app.InvokeAsync(() => { });

        await Assert.That(dockState.ViewportWidth).IsEqualTo(1100);
        await Assert.That(app.FindAll("[data-dock-scope='shell'].dock-layout-host--has-start")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-dock-scope='workspace'].dock-layout-host--has-end")).IsEmpty();

        int notifications = 0;
        dockState.Changed += () => notifications++;

        // Notify only Shell host of 1440px resize first while Workspace host still caches _viewportWidth = 1100:
        // 1440 - 256 - 360 = 824 < 834, so StartNav yields to overlay while Inspector remains docked (1080 >= 834).
        await app.InvokeAsync(() => shellObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
            shellObserver.Id,
            new BrowserWindowSize { Width = 1440, Height = 900 },
            Breakpoint.Lg,
            isImmediate: false)));
        await editor.InvokeAsync(() => editor.Render());
        await app.InvokeAsync(() => { });

        await Assert.That(dockState.ViewportWidth).IsEqualTo(1440);
        await Assert.That(notifications).IsLessThanOrEqualTo(2);
        await Assert.That(app.FindAll("[data-dock-scope='shell'].dock-layout-host--has-start")).IsEmpty();
        await Assert.That(app.FindAll("[data-dock-scope='workspace'].dock-layout-host--has-end")).Count().IsEqualTo(1);

        // Widen to 1536px: 1536 - 256 - 360 = 920 >= 834 => both StartNav and Inspector dock simultaneously.
        notifications = 0;
        await app.InvokeAsync(() => shellObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
            shellObserver.Id,
            new BrowserWindowSize { Width = 1536, Height = 900 },
            Breakpoint.Xl,
            isImmediate: false)));
        await editor.InvokeAsync(() => editor.Render());
        await app.InvokeAsync(() => { });

        await Assert.That(dockState.ViewportWidth).IsEqualTo(1536);
        await Assert.That(notifications).IsLessThanOrEqualTo(2);
        await Assert.That(app.FindAll("[data-dock-scope='shell'].dock-layout-host--has-start")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-dock-scope='workspace'].dock-layout-host--has-end")).Count().IsEqualTo(1);

        // Shrink Workspace observer first to 1280px (StartNav overlays, Inspector remains docked) while Shell is stale at 1536.
        notifications = 0;
        await app.InvokeAsync(() => workspaceObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
            workspaceObserver.Id,
            new BrowserWindowSize { Width = 1280, Height = 720 },
            Breakpoint.Lg,
            isImmediate: false)));
        await app.InvokeAsync(() => app.Render());
        await app.InvokeAsync(() => { });

        await Assert.That(dockState.ViewportWidth).IsEqualTo(1280);
        await Assert.That(notifications).IsLessThanOrEqualTo(2);
        await Assert.That(app.FindAll("[data-dock-scope='shell'].dock-layout-host--has-start")).IsEmpty();
        await Assert.That(app.FindAll("[data-dock-scope='workspace'].dock-layout-host--has-end")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task WorkspaceUtilitiesAndBrowserDockPanelsExposeFiveNonPersistedDescriptors()
    {
        await Assert.That(WorkspaceUtilities.All.Length).IsEqualTo(5);
        await Assert.That(WorkspaceUtilities.All.Select(item => item.Kind).ToArray()).IsEquivalentTo(
        [
            WorkspaceUtilityKind.Inspector,
            WorkspaceUtilityKind.DocumentJson,
            WorkspaceUtilityKind.Import,
            WorkspaceUtilityKind.Profiles,
            WorkspaceUtilityKind.TrustPrivacy
        ]);

        DockPanelDescriptor[] descriptors =
        [
            BrowserDockPanels.Inspector,
            BrowserDockPanels.DocumentJson,
            BrowserDockPanels.Import,
            BrowserDockPanels.Profiles,
            BrowserDockPanels.TrustPrivacy
        ];

        var state = new DockLayoutState();
        foreach (DockPanelDescriptor descriptor in descriptors)
        {
            await Assert.That(descriptor.Scope).IsEqualTo(DockScope.Workspace);
            await Assert.That(descriptor.Side).IsEqualTo(DockSide.End);
            await Assert.That(descriptor.PersistState).IsFalse();
            await Assert.That(descriptor.DefaultWidth).IsEqualTo(360);
            await Assert.That(descriptor.MinWidth).IsEqualTo(300);
            await Assert.That(descriptor.MaxWidth).IsEqualTo(620);
            await Assert.That(descriptor.CanClose).IsTrue();
            await Assert.That(descriptor.IsResizable).IsTrue();
            state.Register(descriptor, EmptyContent);
        }

        state.OpenExclusive(BrowserDockPanels.TrustPrivacyId);
        DockLayoutSnapshot snapshot = state.CreateSnapshot("workspace.test", DockScope.Workspace);
        await Assert.That(snapshot.Panels).IsEmpty();
    }

    [Test]
    public async Task DockLayoutOpenExclusiveSwitchesUtilitiesAtomicallyWithoutZeroWidthIntermediateRender()
    {
        var state = new DockLayoutState();
        state.Register(BrowserDockPanels.Inspector, EmptyContent);
        state.Register(BrowserDockPanels.DocumentJson, EmptyContent);
        state.Register(BrowserDockPanels.Import, EmptyContent);
        state.Register(BrowserDockPanels.Profiles, EmptyContent);
        state.Register(BrowserDockPanels.TrustPrivacy, EmptyContent);

        state.OpenExclusive(BrowserDockPanels.InspectorId);
        await Assert.That(state.GetEffectiveDockedWidth(DockScope.Workspace, DockSide.End)).IsEqualTo(360);

        int notifications = 0;
        int minObservedEndWidth = int.MaxValue;
        state.Changed += () =>
        {
            notifications++;
            minObservedEndWidth = Math.Min(minObservedEndWidth, state.GetEffectiveDockedWidth(DockScope.Workspace, DockSide.End));
        };

        state.OpenExclusive(BrowserDockPanels.ImportId);

        await Assert.That(notifications).IsEqualTo(1);
        await Assert.That(minObservedEndWidth).IsEqualTo(360);
        await Assert.That(state.GetPanel(BrowserDockPanels.InspectorId)!.State.IsOpen).IsFalse();
        await Assert.That(state.GetPanel(BrowserDockPanels.InspectorId)!.State.IsActive).IsFalse();
        await Assert.That(state.GetPanel(BrowserDockPanels.ImportId)!.State.IsOpen).IsTrue();
        await Assert.That(state.GetPanel(BrowserDockPanels.ImportId)!.State.IsActive).IsTrue();

        // Mobile overlay switch also happens in one atomic notification without dropping active overlay state.
        state.UpdateViewport(760, isMobile: true, DockScope.Workspace, DockLayoutState.DefaultMinimumContentWidth);
        state.OpenExclusive(BrowserDockPanels.ImportId);
        notifications = 0;
        bool sawNoActiveOverlay = false;
        state.Changed += () =>
        {
            bool anyActiveOverlay = state.GetPanels(DockScope.Workspace, DockSide.End)
                .Any(entry => entry.State is { IsOpen: true, IsActive: true });
            if (!anyActiveOverlay) sawNoActiveOverlay = true;
        };

        state.OpenExclusive(BrowserDockPanels.TrustPrivacyId);

        await Assert.That(notifications).IsEqualTo(1);
        await Assert.That(sawNoActiveOverlay).IsFalse();
        await Assert.That(state.GetPanel(BrowserDockPanels.TrustPrivacyId)!.State.IsOpen).IsTrue();
        await Assert.That(state.GetPanel(BrowserDockPanels.TrustPrivacyId)!.State.IsActive).IsTrue();
        await Assert.That(state.GetPanel(BrowserDockPanels.ImportId)!.State.IsOpen).IsFalse();
    }

    [Test]
    public async Task WorkspaceUtilityDockCoordinatorEnforcesSingleActiveAndGatesDirtyDocumentSwitch()
    {
        var state = new DockLayoutState();
        state.Register(BrowserDockPanels.Inspector, EmptyContent);
        state.Register(BrowserDockPanels.DocumentJson, EmptyContent);
        state.Register(BrowserDockPanels.Import, EmptyContent);
        state.Register(BrowserDockPanels.Profiles, EmptyContent);
        state.Register(BrowserDockPanels.TrustPrivacy, EmptyContent);

        bool allowDocumentLeave = true;
        int retireCount = 0;
        using var coordinator = new WorkspaceUtilityDockCoordinator(
            state,
            () => Task.FromResult(allowDocumentLeave),
            () => retireCount++);

        await Assert.That(coordinator.IsAnyOpen).IsFalse();
        await Assert.That(coordinator.ActiveUtility).IsNull();

        await Assert.That(await coordinator.OpenAsync(WorkspaceUtilityKind.Inspector)).IsTrue();
        await Assert.That(coordinator.ActiveUtility).IsEqualTo(WorkspaceUtilityKind.Inspector);

        await Assert.That(await coordinator.OpenAsync(WorkspaceUtilityKind.DocumentJson)).IsTrue();
        await Assert.That(coordinator.ActiveUtility).IsEqualTo(WorkspaceUtilityKind.DocumentJson);
        await Assert.That(state.GetPanels(DockScope.Workspace, DockSide.End).Count(entry => entry.State.IsOpen)).IsEqualTo(1);

        // Dirty Document (JSON) cancellation must leave dock state completely untouched.
        allowDocumentLeave = false;
        await Assert.That(await coordinator.OpenAsync(WorkspaceUtilityKind.Import)).IsFalse();
        await Assert.That(coordinator.ActiveUtility).IsEqualTo(WorkspaceUtilityKind.DocumentJson);
        await Assert.That(retireCount).IsEqualTo(0);
        await Assert.That(state.GetPanel(BrowserDockPanels.ImportId)!.State.IsOpen).IsFalse();

        // Allowing leave retires Document and switches atomically to Import.
        allowDocumentLeave = true;
        await Assert.That(await coordinator.OpenAsync(WorkspaceUtilityKind.Import)).IsTrue();
        await Assert.That(retireCount).IsEqualTo(1);
        await Assert.That(coordinator.ActiveUtility).IsEqualTo(WorkspaceUtilityKind.Import);
        await Assert.That(state.GetPanel(BrowserDockPanels.DocumentJsonId)!.State.IsOpen).IsFalse();

        // Toggle on active utility closes it cleanly.
        await Assert.That(await coordinator.ToggleAsync(WorkspaceUtilityKind.Import)).IsTrue();
        await Assert.That(coordinator.IsAnyOpen).IsFalse();
        await Assert.That(coordinator.ActiveUtility).IsNull();
    }

    [Test]
    public async Task WorkspaceUtilityRailRendersAccessibleVerticalToolbarWithWrappingRovingFocusAndNoAriaPressed()
    {
        await using BunitContext context = BrowserFixture.Context();
        WorkspaceUtilityKind? clicked = null;
        IRenderedComponent<WorkspaceUtilityRail> rail = context.Render<WorkspaceUtilityRail>(parameters => parameters
            .Add(item => item.Locale, "en")
            .Add(item => item.IsRtl, false)
            .Add(item => item.ActiveUtility, WorkspaceUtilityKind.Profiles)
            .Add(item => item.ImportAvailable, true)
            .Add(item => item.SecretsUnlocked, true)
            .Add(item => item.HasActiveProfile, true)
            .Add(item => item.UtilitySelected, EventCallback.Factory.Create<WorkspaceUtilityKind>(this, kind => clicked = kind)));

        AngleSharp.Dom.IElement toolbar = rail.Find("[role='toolbar']");
        await Assert.That(toolbar.GetAttribute("aria-orientation")).IsEqualTo("vertical");

        IReadOnlyList<AngleSharp.Dom.IElement> buttons = rail.FindAll("button[data-utility-rail-action]");
        await Assert.That(buttons.Select(item => item.GetAttribute("data-utility-rail-action") ?? string.Empty).ToArray())
            .IsEquivalentTo(["inspector", "document", "import", "profiles", "trust-privacy"]);

        foreach (AngleSharp.Dom.IElement button in buttons)
        {
            await Assert.That(button.HasAttribute("aria-pressed")).IsFalse();
            await Assert.That(button.GetAttribute("aria-label")).IsNotNull().And.IsNotEmpty();
            await Assert.That(button.GetAttribute("title")).IsNotNull().And.IsNotEmpty();
            await Assert.That(button.GetAttribute("aria-controls")).IsNotNull().And.IsNotEmpty();
        }

        await Assert.That(rail.Find("[data-utility-rail-action='profiles']").GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(rail.Find("[data-utility-rail-action='inspector']").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(rail.FindAll("[data-utility-rail-action='trust-privacy'] [data-utility-status]")).Count().IsEqualTo(1);
        await Assert.That(rail.FindAll("[data-utility-rail-action='profiles'] [data-utility-status]")).Count().IsEqualTo(1);

        // Wrapping roving tabindex calls FocusAsync on target buttons.
        int focusCallsBefore = context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus");
        await toolbar.KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        await Assert.That(rail.Find("[data-utility-rail-action='trust-privacy']").GetAttribute("tabindex")).IsEqualTo("0");
        await Assert.That(rail.Find("[data-utility-rail-action='inspector']").GetAttribute("tabindex")).IsEqualTo("-1");
        await Assert.That(context.JSInterop.Invocations.Count(call => call.Identifier == "Blazor._internal.domWrapper.focus"))
            .IsGreaterThan(focusCallsBefore);

        await toolbar.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await Assert.That(rail.Find("[data-utility-rail-action='inspector']").GetAttribute("tabindex")).IsEqualTo("0");

        await toolbar.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        await Assert.That(rail.Find("[data-utility-rail-action='trust-privacy']").GetAttribute("tabindex")).IsEqualTo("0");

        await toolbar.KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        await Assert.That(rail.Find("[data-utility-rail-action='inspector']").GetAttribute("tabindex")).IsEqualTo("0");

        await rail.Find("[data-utility-rail-action='import']").ClickAsync(new MouseEventArgs());
        await Assert.That(clicked).IsEqualTo(WorkspaceUtilityKind.Import);

        // When ImportAvailable is false, Import button is omitted cleanly.
        rail.Render(parameters => parameters.Add(item => item.ImportAvailable, false));
        await Assert.That(rail.FindAll("[data-utility-rail-action='import']")).IsEmpty();
        await Assert.That(rail.FindAll("button[data-utility-rail-action]")).Count().IsEqualTo(4);
    }

    [Test]
    public async Task UtilityDockWidthBudgetProtects420PxCanvasAcrossBreakpointsWith44PxRail()
    {
        var state = new DockLayoutState();
        state.Register(BrowserDockPanels.StartNav, EmptyContent);
        state.Register(BrowserDockPanels.Inspector, EmptyContent);
        state.Open(BrowserDockPanels.StartNavId);

        // 1440px multi-section Configure (834px floor = 790 + 44 rail): StartNav stays docked while utilities closed (1184 >= 834).
        state.UpdateViewport(1440, isMobile: false, DockScope.Shell, minimumContentWidth: 420);
        state.UpdateViewport(1440, isMobile: false, DockScope.Workspace, minimumContentWidth: 834);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        // Opening 360px utility at 1440px -> 1440 - 256 - 360 = 824 < 834 -> StartNav yields deliberately to overlay while End Dock stays docked.
        state.OpenExclusive(BrowserDockPanels.InspectorId);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // At 1536px -> 1536 - 256 - 360 = 920 >= 834 -> both StartNav and End Dock stay docked side by side.
        state.UpdateViewport(1536, isMobile: false, DockScope.Workspace, minimumContentWidth: 834);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // At 1280px -> StartNav overlays while End Dock stays docked (1280 - 360 = 920 >= 834).
        state.UpdateViewport(1280, isMobile: false, DockScope.Workspace, minimumContentWidth: 834);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // At 1149px -> End Dock (360) cannot fit (789 < 834), yields to overlay while StartNav stays docked (893 >= 834).
        state.UpdateViewport(1149, isMobile: false, DockScope.Workspace, minimumContentWidth: 834);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();

        // Single-section / Review / Files above 960px uses 464px floor (420 + 44 rail); 1149px fits both (1149 - 256 - 360 = 533 >= 464).
        state.UpdateViewport(1149, isMobile: false, DockScope.Workspace, minimumContentWidth: 464);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsFalse();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // At 960px (rail hidden -> 420px floor), End Dock stays docked (600 >= 420) and StartNav overlays (344 < 420).
        state.UpdateViewport(960, isMobile: false, DockScope.Workspace, minimumContentWidth: 420);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsFalse();

        // At 760px mobile -> both overlay.
        state.UpdateViewport(760, isMobile: true, DockScope.Workspace, minimumContentWidth: 420);
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.StartNavId)!)).IsTrue();
        await Assert.That(state.ShouldRenderDockedPanelAsOverlay(state.GetPanel(BrowserDockPanels.InspectorId)!)).IsTrue();
    }
}
