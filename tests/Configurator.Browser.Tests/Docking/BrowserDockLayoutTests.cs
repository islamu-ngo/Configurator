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
    public async Task TabbedEndDockSwitchesBetweenInspectorAndDocumentJsonWithKeyboardRovingFocus()
    {
        await using BunitContext context = BrowserFixture.Context();
        IRenderedComponent<ConfigurationEditor> editor = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));

        await editor.Find("[data-action='toggle-inspector']").ClickAsync(new MouseEventArgs());

        IReadOnlyList<AngleSharp.Dom.IElement> tabs = editor.FindAll("[data-testid='dock-tab-strip-tab']");
        await Assert.That(tabs.Count).IsEqualTo(2);
        await Assert.That(tabs[0].GetAttribute("data-dock-tab-panel-id")).IsEqualTo(BrowserDockPanels.InspectorId.Value);
        await Assert.That(tabs[0].GetAttribute("aria-selected")).IsEqualTo("true");
        await Assert.That(tabs[0].GetAttribute("tabindex")).IsEqualTo("0");
        await Assert.That(tabs[1].GetAttribute("data-dock-tab-panel-id")).IsEqualTo(BrowserDockPanels.DocumentJsonId.Value);
        await Assert.That(tabs[1].GetAttribute("aria-selected")).IsEqualTo("false");
        await Assert.That(tabs[1].GetAttribute("tabindex")).IsEqualTo("-1");
        await Assert.That(editor.FindAll("[data-inspector-pane]")).Count().IsEqualTo(1);

        await tabs[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        tabs = editor.FindAll("[data-testid='dock-tab-strip-tab']");
        await Assert.That(tabs[1].GetAttribute("aria-selected")).IsEqualTo("true");
        await Assert.That(tabs[1].GetAttribute("tabindex")).IsEqualTo("0");
        await Assert.That(editor.FindAll("[data-developer-panel]")).Count().IsEqualTo(1);

        await tabs[1].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        tabs = editor.FindAll("[data-testid='dock-tab-strip-tab']");
        await Assert.That(tabs[0].GetAttribute("aria-selected")).IsEqualTo("true");
        await Assert.That(editor.FindAll("[data-developer-panel]")).IsEmpty();
    }

    [Test]
    public async Task ShellStartDockHostsStickyFooterAndTopConfigureRibbonLeadsConfigureStage()
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
        await Assert.That(app.FindAll("[data-configure-ribbon] [data-action='toggle-inspector']")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-configure-ribbon] [data-editing-mode='developer']")).Count().IsEqualTo(1);
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

        await app.Find("[data-action='toggle-inspector']").ClickAsync(new MouseEventArgs());

        IRenderedComponent<DockLayoutHost> shellHost = app.FindComponents<DockLayoutHost>()
            .Single(host => host.Instance.Scope == DockScope.Shell);
        IRenderedComponent<DockLayoutHost> workspaceHost = app.FindComponents<DockLayoutHost>()
            .Single(host => host.Instance.Scope == DockScope.Workspace);
        IRenderedComponent<ConfigurationEditor> editor = app.FindComponent<ConfigurationEditor>();

        await Assert.That(workspaceHost.Instance.MinimumContentWidth).IsEqualTo(790);

        var shellObserver = (IBrowserViewportObserver)shellHost.Instance;
        var workspaceObserver = (IBrowserViewportObserver)workspaceHost.Instance;

        await app.InvokeAsync(async () =>
        {
            await shellObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
                shellObserver.Id,
                new BrowserWindowSize { Width = 1050, Height = 800 },
                Breakpoint.Md,
                isImmediate: true));
            await workspaceObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
                workspaceObserver.Id,
                new BrowserWindowSize { Width = 1050, Height = 800 },
                Breakpoint.Md,
                isImmediate: true));
        });
        await app.InvokeAsync(() => { });

        await Assert.That(dockState.ViewportWidth).IsEqualTo(1050);
        await Assert.That(app.FindAll("[data-dock-scope='shell'].dock-layout-host--has-start")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-dock-scope='workspace'].dock-layout-host--has-end")).IsEmpty();

        int notifications = 0;
        dockState.Changed += () => notifications++;

        // Notify only Shell host of 1440px resize first while Workspace host still caches _viewportWidth = 1050,
        // then force parent ConfigurationEditor re-render (triggering Workspace OnParametersSet).
        await app.InvokeAsync(() => shellObserver.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(
            shellObserver.Id,
            new BrowserWindowSize { Width = 1440, Height = 900 },
            Breakpoint.Lg,
            isImmediate: false)));
        await editor.InvokeAsync(() => editor.Render());
        await app.InvokeAsync(() => { });

        await Assert.That(dockState.ViewportWidth).IsEqualTo(1440);
        await Assert.That(notifications).IsLessThanOrEqualTo(2);
        await Assert.That(app.FindAll("[data-dock-scope='shell'].dock-layout-host--has-start")).Count().IsEqualTo(1);
        await Assert.That(app.FindAll("[data-dock-scope='workspace'].dock-layout-host--has-end")).Count().IsEqualTo(1);

        // Shrink Workspace observer first to 1280px (StartNav overlays, Inspector remains docked) while Shell is stale at 1440.
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
}
