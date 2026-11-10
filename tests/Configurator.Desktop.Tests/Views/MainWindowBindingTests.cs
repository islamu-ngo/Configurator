namespace Configurator.Desktop.Tests.Views;

using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

public sealed class MainWindowBindingTests
{
    private static readonly string[] PathEditorNames = ["DefinitionUrl", "DefinitionPath", "ExistingFilePath", "OutputDirectory", "FileName"];

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [NotInParallel]
    public async Task ImportPurposeRemainsSelectedWhenWorkspaceLocaleChanges(int purpose)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowBindingTests));
        try
        {
            var state = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root();
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes.ToArray());
                    shell.Navigation.ReviewCommand.Execute(null);
                    window.UpdateLayout();
                    window.GetNamedControl<Expander>("OptionalImport").IsExpanded = true;
                    shell.ImportReview.ImportPurposeIndex = purpose;
                    Dispatcher.UIThread.RunJobs();
                    shell.Workspace.SelectedLocale = "ar";
                    Dispatcher.UIThread.RunJobs();
                    ComboBox selector = window.GetNamedControl<ComboBox>("ImportPurpose");
                    var arabic = (selector.SelectedIndex, shell.ImportReview.ImportPurposeIndex,
                        selector.SelectionBoxItem is not null,
                        ControlAutomationPeer.CreatePeerForElement((ComboBoxItem)selector.SelectedItem!)?.GetName() ==
                            shell.ImportReview.ImportPurposes[purpose]);
                    shell.Workspace.SelectedLocale = "en";
                    Dispatcher.UIThread.RunJobs();
                    return (arabic, english: (selector.SelectedIndex, shell.ImportReview.ImportPurposeIndex,
                        selector.SelectionBoxItem is not null,
                        ControlAutomationPeer.CreatePeerForElement((ComboBoxItem)selector.SelectedItem!)?.GetName() ==
                            shell.ImportReview.ImportPurposes[purpose]));
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(state.arabic).IsEqualTo((purpose, purpose, true, true));
            await Assert.That(state.english).IsEqualTo((purpose, purpose, true, true));
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task WindowChromeRefreshesLocaleAndErrorPaletteStaysReadable()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowBindingTests));
        try
        {
            var state = await session.Dispatch(() =>
            {
                var messenger = new WeakReferenceMessenger();
                var dialogs = new NoDialogs();
                using var loader = new DefinitionLoaderViewModel(messenger, dialogs);
                using var workspace = new FormWorkspaceViewModel(messenger);
                using var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                using var importReview = new DesktopImportReviewViewModel(messenger, dialogs, publisher);
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher, importReview);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    loader.LoadDefinition(ViewModels.DefinitionEditingTests.Definition().CanonicalBytes.ToArray());
                    Dispatcher.UIThread.RunJobs();
                    string? english = window.GetNamedControl<Button>("Validate").Content?.ToString();
                    string englishPin = loader.DefinitionPin;
                    object? selectedExperience = window.GetNamedControl<ComboBox>("Experience").SelectedItem;
                    workspace.SelectedLocale = "ar";
                    Dispatcher.UIThread.RunJobs();
                    string? arabic = window.GetNamedControl<Button>("Validate").Content?.ToString();
                    string arabicPin = loader.DefinitionPin;
                    var experience = window.GetNamedControl<ComboBox>("Experience");
                    bool experienceSelected = selectedExperience is not null &&
                        ReferenceEquals(experience.SelectedItem, selectedExperience) && experience.SelectionBoxItem is not null;
                    shell.Navigation.ReviewCommand.Execute(null);
                    window.UpdateLayout();
                    window.GetNamedControl<Expander>("OptionalImport").IsExpanded = true;
                    window.UpdateLayout();
                    bool pathsLeftToRight = PathEditorNames
                        .All(name => window.GetNamedControl<TextBox>(name).FlowDirection == FlowDirection.LeftToRight);
                    bool readable = true;
                    foreach (ThemeVariant variant in new[] { ThemeVariant.Light, ThemeVariant.Dark, DesktopTheme.HighContrast })
                    {
                        window.RequestedThemeVariant = variant;
                        Dispatcher.UIThread.RunJobs();
                        if (!window.TryFindResource("ConfiguratorErrorBrush", window.ActualThemeVariant, out object? error) ||
                            !window.TryFindResource("ConfiguratorSurfaceBrush", window.ActualThemeVariant, out object? surface) ||
                            error is not SolidColorBrush foreground || surface is not SolidColorBrush background)
                            readable = false;
                        else readable &= Contrast(foreground.Color, background.Color) >= 4.5;
                    }
                    return (english, arabic, englishPin, arabicPin, experienceSelected, pathsLeftToRight, readable);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(state.arabic).IsNotEqualTo(state.english);
            await Assert.That(state.arabic!.Any(character => character is >= '\u0600' and <= '\u06ff')).IsTrue();
            await Assert.That(state.arabicPin).IsNotEqualTo(state.englishPin);
            await Assert.That(state.arabicPin.Any(character => character is >= '\u0600' and <= '\u06ff')).IsTrue();
            await Assert.That((state.experienceSelected, state.pathsLeftToRight)).IsEqualTo((true, true));
            await Assert.That(state.readable).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static readonly string[] ActionNames =
    [
        "FetchDefinition", "LoadDefinition", "Validate", "PrepareSelection", "SaveSelection",
        "Import", "Cancel", "SelectDefinition", "SelectImport", "SelectOutput"
    ];

    [Test]
    [NotInParallel]
    public async Task EmbeddedStoreCardsRenderFilterAndConfigureDirectly()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowBindingTests));
        try
        {
            var result = await session.Dispatch(async () =>
            {
                var messenger = new WeakReferenceMessenger();
                var dialogs = new NoDialogs();
                using var storeProvider = new Configurator.Presentation.Store.EmbeddedDefinitionStoreProvider();
                using var loader = new DefinitionLoaderViewModel(messenger, dialogs, storeProvider: storeProvider);
                using var workspace = new FormWorkspaceViewModel(messenger);
                using var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                using var importReview = new DesktopImportReviewViewModel(messenger, dialogs, publisher);
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher, importReview);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    await loader.LoadStoreAsync();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var cards = window.GetNamedControl<ItemsControl>("StoreProjects");
                    int initialCount = cards.ItemCount;
                    var search = window.GetNamedControl<TextBox>("StoreSearchQuery");
                    search.Text = "minimal-self-hosted";
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    int filteredCount = cards.ItemCount;
                    Button configure = cards.GetVisualDescendants().OfType<Button>().Single();
                    await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)configure.Command!).ExecuteAsync(configure.CommandParameter);
                    Dispatcher.UIThread.RunJobs();
                    return (initialCount, filteredCount,
                        stage: shell.Navigation.Stage,
                        familyId: shell.Workspace.Form?.Definition.FamilyId);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.initialCount).IsGreaterThanOrEqualTo(2);
            await Assert.That(result.filteredCount).IsEqualTo(1);
            await Assert.That(result.stage).IsEqualTo(DesktopWorkspaceStage.Configure);
            await Assert.That(result.familyId).IsNotNull();
        }
        finally { await Task.Run(session.Dispose); }
    }

    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task RenderedActionsUseCommandsAndFieldsUseDeclarativeItems()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowBindingTests));
        try
        {
            var state = await session.Dispatch(() =>
            {
                var messenger = new WeakReferenceMessenger();
                var dialogs = new NoDialogs();
                using var loader = new DefinitionLoaderViewModel(messenger, dialogs);
                using var workspace = new FormWorkspaceViewModel(messenger);
                using var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                using var importReview = new DesktopImportReviewViewModel(messenger, dialogs, publisher);
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher, importReview);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    loader.LoadDefinition(DesktopApplicationTests.Definition());
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    shell.Navigation.ReviewCommand.Execute(null);
                    window.UpdateLayout();
                    window.GetNamedControl<Expander>("OptionalImport").IsExpanded = true;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    return (
                        commands: ActionNames
                            .All(name => window.GetNamedControl<Button>(name).Command is not null),
                        declarative: window.FindNamedControl<ItemsControl>("Fields") is { ItemCount: > 0 },
                        artifactLabel: window.GetNamedControl<ComboBox>("Artifact").SelectedIndex >= 0 &&
                            window.GetNamedControl<ComboBox>("Artifact").SelectionBoxItem is not null &&
                            window.GetNamedControl<ComboBox>("Artifact").GetVisualDescendants()
                                .OfType<TextBlock>().Any(control => control.Text == publisher.ArtifactId &&
                                    control.IsEffectivelyVisible && control.Bounds.Width > 0),
                        gated: !window.GetNamedControl<Button>("SaveSelection").IsEffectivelyEnabled);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(state.commands).IsTrue();
            await Assert.That(state.declarative).IsTrue();
            await Assert.That(state.artifactLabel).IsTrue();
            await Assert.That(state.gated).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private sealed class NoDialogs : IStorageDialogService
    {
        public Task<StorageSelection<Uri>> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<Uri>(StorageSelectionStatus.Cancelled));

        public Task<StorageSelection<BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<BoundedFileInput>(StorageSelectionStatus.Cancelled));
    }
}
