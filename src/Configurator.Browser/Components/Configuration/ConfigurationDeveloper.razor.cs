using Configurator.Core.Dotenv;
namespace Configurator.Browser.Components.Configuration;

using System.Buffers;
using System.Globalization;
using System.Resources;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Environment;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

/// <summary>Owns one private editor, not a second accepted answer workspace.</summary>
public partial class ConfigurationDeveloper : ComponentBase
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter] public string? EnvironmentArtifactId { get; set; }
    [Parameter] public EventCallback<bool> DirtyChanged { get; set; }
    [Parameter] public EventCallback RequestConsent { get; set; }

    private ElementReference _heading;
    public ElementReference? Heading => _disposed ? null : _heading;

    private static readonly ResourceManager English = new("Configurator.Browser.Resources.BrowserUi", typeof(ConfigurationDeveloper).Assembly);
    private static readonly ResourceManager Arabic = new("Configurator.Browser.Resources.BrowserUi.ar", typeof(ConfigurationDeveloper).Assembly);
    private readonly string _id = "developer-" + Guid.NewGuid().ToString("N");
    private DefinitionFormModel? _observedForm;
    private DefinitionDocumentSession? _session;
    private DefinitionDocumentParseResult? _preview;
    private DefinitionDocumentRepresentation _representation;
    private DefinitionDocumentApplyMode _mode;
    private DefinitionSessionPrivacy _privacy;
    private string? _artifactId;
    private IJSObjectReference? _module;
    private IJSObjectReference? _bridge;
    private IJSObjectReference? _rendering;
    private byte[]? _mountSource;
    private DotNetObjectReference<ConfigurationDeveloper>? _callback;
    private CancellationTokenSource? _operation;
    private CancellationTokenSource _lifetime = new();
    private Task _retirement = Task.CompletedTask;
    private TaskCompletionSource<bool>? _resolution;
    private ElementReference _host;
    private ElementReference _resolutionHeading;
    private ElementReference _retiredNotice;
    private IReadOnlyList<DefinitionDocumentDiagnostic> _diagnostics = [];
    private bool _diagnosticsTruncated;
    private bool _modelReady;
    private bool _mounting;
    private bool _retired;
    private bool _mountFailed;
    private bool _busy;
    private bool _disposed;
    private bool _applying;
    private bool _historyLimited;
    private bool _focusResolution;
    private bool _focusRetired;
    private long _generation;
    private long _version;
    private long _applyingRevision;
    private long _transitionGeneration;
    private int _coveragePage;
    private string _locale = string.Empty;
    private string _status = "Ready";
    private string _statusCode = "developer-ready";
    private const int PageSize = 50;

    /// <summary>Includes provisional work; mounting a clean editor is not pending answer work.</summary>
    public bool HasWork => IsDirty || _busy || _preview?.Candidate is not null;
    public bool IsDirty { get; private set; }
    private bool Permitted => Form.SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets;
    private string RepresentationName => _representation == DefinitionDocumentRepresentation.Canonical ? "canonical" : "environment";
    private string HelpId => _id + "-help";
    private IEnumerable<(string Name, string State)> Coverage => _session?.EnvironmentScope is { } environment
        ? environment.Coverage.Select(item => (item.Key, item.Kind.ToString()))
        : (_session?.Coverage ?? []).Select(item => (item.Path.ToPointer(), item.Editable ? "Editable" : "Protected"));
    private int CoverageCount => Coverage.Count();
    private int CoveragePages => Math.Max(1, (CoverageCount + PageSize - 1) / PageSize);
    private static string LocalizedText(string key, string locale) =>
        (CultureInfo.GetCultureInfo(locale).TwoLetterISOLanguageName == "ar" ? Arabic : English)
        .GetString(key, CultureInfo.InvariantCulture)
        ?? throw new MissingManifestResourceException("Missing Developer UI resource.");
    private string Text(string key) => LocalizedText(key, Form.Locale);
    private string FormatText(string key, params object[] arguments) => string.Format(CultureInfo.GetCultureInfo(Form.Locale), Text(key), arguments);

    /// <summary>Root mode labels share the panel's owned English/Arabic resources.</summary>
    public static string ModeLabel(bool developer, string locale) =>
        LocalizedText(developer ? "DeveloperMode" : "FormMode", locale);

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_observedForm, Form))
        {
            Unsubscribe();
            _retirement = RetireAsync();
            _observedForm = Form;
            _privacy = Form.SessionPrivacy;
            _artifactId = EnvironmentArtifactId;
            _representation = DefinitionDocumentRepresentation.Canonical;
            _mode = DefinitionDocumentApplyMode.Merge;
            _locale = Form.Locale;
            _retired = false;
            _mountFailed = false;
            Form.InputChanged += InputChanged;
            Form.ViewChanged += ViewChanged;
            OpenMetadata();
            SetStatus(Permitted ? "Ready" : "ConsentPending",
                Permitted ? "developer-ready" : "developer-consent-required");
        }
        // A changed artifact is adopted only by an explicit representation transition.
        if (_representation == DefinitionDocumentRepresentation.Canonical) _artifactId = EnvironmentArtifactId;
    }

    private void OpenMetadata()
    {
        _session = Form.OpenDocument(_representation, _representation == DefinitionDocumentRepresentation.Environment ? _artifactId : null);
        _coveragePage = 0;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (_focusRetired)
        {
            _focusRetired = false;
            if (Permitted && _retired)
            {
                long generation = _generation;
                await _retiredNotice.FocusAsync();
                if (_disposed || generation != _generation) return;
            }
        }
        if (_modelReady && _locale != Form.Locale && _session is { } metadataSession && _bridge is { } metadataBridge)
        {
            long generation = _generation;
            long version = _version;
            CancellationToken metadataToken = _lifetime.Token;
            string locale = Form.Locale;
            try
            {
                await metadataBridge.InvokeVoidAsync("metadata", generation, Text("EditorLabel"), HelpId, locale, EditorPhrases());
                if (!ModelCurrent(generation, metadataSession, version, metadataToken)) return;
                await metadataBridge.InvokeVoidAsync("diagnostics", generation, version, _diagnostics);
                if (!ModelCurrent(generation, metadataSession, version, metadataToken)) return;
                _locale = locale;
            }
            catch (JSException)
            {
                if (ModelCurrent(generation, metadataSession, version, metadataToken))
                    SetStatus("Unavailable", "developer-metadata-failed");
                return;
            }
        }
        if (_focusResolution && _resolution is not null)
        {
            _focusResolution = false;
            long generation = _generation;
            await _resolutionHeading.FocusAsync();
            if (_disposed || generation != _generation) return;
        }
        if (!Permitted || _retired || _mountFailed || _mounting || _modelReady || _session is null
            || Form.InputRevision != _session.InputRevision) return;
        _mounting = true;
        long owner = ++_generation;
        DefinitionDocumentSession session = _session;
        CancellationToken token = _lifetime.Token;
        byte[]? source = null;
        DotNetObjectReference<ConfigurationDeveloper>? callback = null;
        try
        {
            await _retirement;
            if (!LeaseCurrent(owner, session, token)) return;
            if (_module is null)
            {
                IJSObjectReference module = await Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-developer.js");
                if (!LeaseCurrent(owner, session, token)) { await module.DisposeAsync(); return; }
                _module = module;
            }
            if (_bridge is null)
            {
                IJSObjectReference bridge = await _module.InvokeAsync<IJSObjectReference>("createBridge");
                if (!LeaseCurrent(owner, session, token)) { await bridge.DisposeAsync(); return; }
                _bridge = bridge;
            }
            if (!LeaseCurrent(owner, session, token)) return;
            IJSObjectReference ownedBridge = _bridge;
            if (_rendering is null)
            {
                IJSObjectReference rendering = await Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js");
                if (!LeaseCurrent(owner, session, token)) { await rendering.DisposeAsync(); return; }
                _rendering = rendering;
            }
            source = session.ReadSource(token);
            _mountSource = source;
            // Source admission and DOM construction must not consume one browser task.
            await _rendering.InvokeVoidAsync("nextFrame", token);
            if (!LeaseCurrent(owner, session, token)) return;
            callback = DotNetObjectReference.Create(this);
            _callback = callback;
            long version = await ownedBridge.InvokeAsync<long>("mount", owner, _host, callback, Options(source));
            if (!LeaseCurrent(owner, session, token))
            {
                await ownedBridge.InvokeVoidAsync("retire", _generation);
                callback.Dispose();
                return;
            }
            _version = version;
            _modelReady = true;
            SetStatus("Ready", "developer-ready");
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException or OperationCanceledException)
        {
            if (LeaseCurrent(owner, session, token))
            {
                _mountFailed = true;
                SetStatus(exception is InvalidOperationException ? "SourceUnavailable" : "Unavailable", "developer-unavailable");
            }
            callback?.Dispose();
            if (ReferenceEquals(_callback, callback)) _callback = null;
        }
        finally
        {
            if (source is not null)
            {
                CryptographicOperations.ZeroMemory(source);
                if (ReferenceEquals(_mountSource, source)) _mountSource = null;
            }
            _mounting = false;
            if (!_disposed) await InvokeAsync(StateHasChanged);
        }
    }

    private bool LeaseCurrent(long generation, DefinitionDocumentSession session, CancellationToken token) =>
        !_disposed && !_retired && !token.IsCancellationRequested && generation == _generation
        && ReferenceEquals(_observedForm, Form) && ReferenceEquals(_session, session) && Permitted
        && Form.InputRevision == session.InputRevision && Form.Definition.Digest == session.DefinitionDigest;
    private bool ModelCurrent(long generation, DefinitionDocumentSession session, long version, CancellationToken token) =>
        _modelReady && version == _version && LeaseCurrent(generation, session, token);

    private EditorOptions Options(byte[] source) => new(source, RepresentationName, Text("EditorLabel"), HelpId,
        Form.Locale, CompletionItems(), EditorPhrases());

    private Dictionary<string, string> EditorPhrases() => new()
    {
        ["Search"] = Text("Search"),
        ["Find"] = Text("Find"),
        ["Replace"] = Text("Replace"),
        ["next"] = Text("Next"),
        ["previous"] = Text("Previous"),
        ["close"] = Text("Close"),
        ["replace"] = Text("Replace"),
        ["replace all"] = Text("ReplaceAll"),
        ["all"] = Text("AllMatches"),
        ["match case"] = Text("MatchCase"),
        ["regexp"] = Text("RegularExpression"),
        ["by word"] = Text("WholeWords"),
        ["Completions"] = Text("Completions"),
        ["No matches found"] = Text("NoMatches"),
        ["historyLimited"] = Text("HistoryLimited"),
        ["invalidJson"] = Text("InvalidJson"),
        ["invalidEnvironment"] = Text("InvalidEnvironment")
    };

    private CompletionItem[] CompletionItems() => _session?.EnvironmentScope is { } environment
        ? environment.Mappings.Take(2048).Select(item => new CompletionItem(item.Key, item.Key + "=", CompletionDetail(item.Path), "property")).ToArray()
        : (_session?.Coverage ?? []).Where(item => item.Editable).Take(2048).Select(item =>
            new CompletionItem(item.Path.Segments[^1], JsonSerializer.Serialize(item.Path.Segments[^1]) + ": ",
                CompletionDetail(item.Path), "property")).ToArray();

    private static string CompletionDetail(DefinitionPath path)
    {
        string pointer = path.ToPointer();
        return pointer.Length <= 1024 ? pointer : pointer[..1021] + "...";
    }

    /// <summary>Only version/dirty/history metadata crosses back from the private editor.</summary>
    [JSInvokable]
    public async Task OnEditorChangedAsync(long generation, long version, bool dirty, bool historyLimited)
    {
        if (_disposed || _session is not { } session || !_modelReady || version < _version) return;
        CancellationToken token = _lifetime.Token;
        if (!LeaseCurrent(generation, session, token)) return;
        if (version == _version)
        {
            // markApplied and a rejected history-limited edit report metadata
            // without changing text. Neither may retire newly reopened authority.
            _historyLimited |= historyLimited;
            await InvokeAsync(StateHasChanged);
            return;
        }
        session.InvalidateText();
        CancelOperation();
        _version = version;
        _historyLimited |= historyLimited;
        _diagnostics = [];
        _diagnosticsTruncated = false;
        SetStatus("Changed", "developer-changed");
        await SetDirtyAsync(dirty);
        if (!ModelCurrent(generation, session, version, token)) return;
        await InvokeAsync(StateHasChanged);
    }

    private CancellationToken BeginOperation()
    {
        CancelOperation();
        _session?.InvalidateText();
        _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _diagnostics = [];
        _diagnosticsTruncated = false;
        _busy = true;
        return _operation.Token;
    }

    private void CancelOperation()
    {
        _operation?.Cancel();
        _operation?.Dispose();
        _operation = null;
        _preview?.Dispose();
        _preview = null;
        _busy = false;
    }

    private async Task<byte[]> ReadEditorAsync(long generation, DefinitionDocumentSession session, long version,
        IJSObjectReference bridge, CancellationToken token)
    {
        byte[]? bytes = null;
        try
        {
            if (!ModelCurrent(generation, session, version, token)) throw new OperationCanceledException(token);
            // Do not abandon a returned private buffer through interop cancellation.
            // The session clears transferred bytes, including delayed stale reads.
            try
            {
                bytes = await bridge.InvokeAsync<byte[]>("read", generation, version);
                if (!ModelCurrent(generation, session, version, token)) throw new OperationCanceledException(token);
            }
            finally { await bridge.InvokeVoidAsync("releaseRead", generation, version); }
            if (!ModelCurrent(generation, session, version, token)) throw new OperationCanceledException(token);
            byte[] transferred = bytes;
            bytes = null;
            return transferred;
        }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task PreviewAsync()
    {
        if (_busy || !_modelReady || _session is not { } session || _bridge is not { } bridge
            || _rendering is not { } rendering || !Permitted) return;
        long generation = _generation;
        long version = _version;
        CancellationToken token = BeginOperation();
        try
        {
            // Acknowledge work before acquiring source or running the bounded parser.
            await rendering.InvokeVoidAsync("nextFrame", token);
            if (!ModelCurrent(generation, session, version, token)) return;
            DefinitionDocumentParseResult parsed = await session.ParseAsync(
                cancellation => ReadEditorAsync(generation, session, version, bridge, cancellation), _mode,
                cooperativeYield: cancellation => rendering.InvokeVoidAsync("nextTask", cancellation),
                cancellationToken: token);
            if (!ModelCurrent(generation, session, version, token)) { parsed.Dispose(); return; }
            _preview = parsed;
            _diagnostics = parsed.Diagnostics;
            _diagnosticsTruncated = parsed.DiagnosticsTruncated;
            SetStatus(parsed.Succeeded ? "PreviewReady" : "Invalid", parsed.Succeeded ? "developer-preview" : "developer-invalid");
            await bridge.InvokeVoidAsync("diagnostics", generation, version, _diagnostics);
            if (!ModelCurrent(generation, session, version, token)) return;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (JSException)
        {
            if (ModelCurrent(generation, session, version, token)) SetStatus("Unavailable", "developer-unavailable");
        }
        finally
        {
            if (ModelCurrent(generation, session, version, token)) _busy = false;
        }
    }

    private async Task<bool> ApplyAsync()
    {
        if (_busy || !_modelReady || !Permitted) return false;
        if (_preview?.Candidate is null) await PreviewAsync();
        if (_busy || _preview?.Candidate is not { } candidate || _session is not { } session
            || _bridge is not { } bridge || _rendering is not { } rendering) return false;
        DefinitionDocumentParseResult preview = _preview;
        long generation = _generation;
        long version = _version;
        CancellationToken token = _operation?.Token ?? _lifetime.Token;
        if (!ModelCurrent(generation, session, version, token)) return false;
        _busy = true;
        bool committed = false;
        try
        {
            await rendering.InvokeVoidAsync("nextFrame", token);
            if (!ModelCurrent(generation, session, version, token) || !ReferenceEquals(_preview, preview)) return false;
            bool frozen = await bridge.InvokeAsync<bool>("freeze", generation, version, true);
            if (!frozen || !ModelCurrent(generation, session, version, token) || !ReferenceEquals(_preview, preview)) return false;
            DefinitionDocumentApplyResult result;
            _applying = true;
            _applyingRevision = checked(session.InputRevision + 1);
            try { result = session.Apply(candidate, token); }
            finally { _applying = false; }
            committed = result.Succeeded;
            if (!result.Succeeded)
            {
                preview.Dispose();
                _preview = null;
                SetStatus("Invalid", "developer-rejected");
                return false;
            }
            preview.Dispose();
            _preview = null;
            if (_disposed || generation != _generation || !ReferenceEquals(Form, _observedForm)
                || !Permitted || Form.InputRevision != result.InputRevision)
            {
                await RetireAsync();
                return true; // A successful commit is not a rejection, including observer failure.
            }
            OpenMetadata();
            DefinitionDocumentSession reopened = _session!;
            SetStatus(result.ObserverFailed ? "AppliedObserverFailed" : "Applied", "developer-applied");
            bool marked = await bridge.InvokeAsync<bool>("markApplied", generation, version);
            if (!marked || !ModelCurrent(generation, reopened, version, token)) return true;
            await SetDirtyAsync(false);
            if (!ModelCurrent(generation, reopened, version, token)) return true;
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return committed; }
        catch (JSException)
        {
            if (committed)
            {
                await RetireAsync();
                if (!_disposed) SetStatus("AppliedEditorUnavailable", "developer-applied");
            }
            else if (ModelCurrent(generation, session, version, token)) SetStatus("Unavailable", "developer-unavailable");
            return committed;
        }
        finally
        {
            if (!_disposed && generation == _generation && version == _version && !token.IsCancellationRequested)
            {
                try { await bridge.InvokeVoidAsync("freeze", generation, version, false); }
                catch (JSException)
                {
                    if (!_disposed && generation == _generation && version == _version && !token.IsCancellationRequested)
                        SetStatus(committed ? "AppliedEditorUnavailable" : "Unavailable",
                            committed ? "developer-applied" : "developer-unavailable");
                }
                if (!_disposed && generation == _generation && version == _version && !token.IsCancellationRequested)
                {
                    _busy = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
        }
    }

    private async Task ApplyRequestedAsync() => await ApplyAsync();

    private async Task FormatAsync()
    {
        if (_busy || !_modelReady || _session is not { } session || _bridge is not { } bridge || !Permitted) return;
        long generation = _generation;
        long version = _version;
        CancellationToken token = BeginOperation();
        byte[]? formatted = null;
        try
        {
            using DefinitionDocumentParseResult parsed = await session.ParseAsync(async cancellation =>
            {
                byte[] source = await ReadEditorAsync(generation, session, version, bridge, cancellation);
                try { formatted = FormatSource(source, session.Representation); }
                catch (Exception exception) when (exception is DefinitionException or JsonException or InvalidOperationException)
                { /* The owning session returns the value-free diagnostic; invalid text remains untouched. */ }
                return source;
            }, _mode, cancellationToken: token);
            if (!ModelCurrent(generation, session, version, token)) return;
            if (!parsed.Succeeded || formatted is null)
            {
                _diagnostics = parsed.Diagnostics;
                _diagnosticsTruncated = parsed.DiagnosticsTruncated;
                SetStatus("Invalid", "developer-invalid");
                await bridge.InvokeVoidAsync("diagnostics", generation, version, _diagnostics);
                if (!ModelCurrent(generation, session, version, token)) return;
                return;
            }
            session.InvalidateText();
            EditorState state = await bridge.InvokeAsync<EditorState>("replace", generation, version, formatted);
            if (!LeaseCurrent(generation, session, token) || state.Version < _version) return;
            _version = state.Version;
            _historyLimited = state.HistoryLimited;
            SetStatus("Formatted", "developer-formatted");
            await SetDirtyAsync(state.Dirty);
            if (!ModelCurrent(generation, session, state.Version, token)) return;
        }
        catch (JSException)
        {
            if (LeaseCurrent(generation, session, token)) SetStatus("Unavailable", "developer-unavailable");
        }
        finally
        {
            if (formatted is not null) CryptographicOperations.ZeroMemory(formatted);
            if (LeaseCurrent(generation, session, token)) _busy = false;
        }
    }

    private static byte[]? FormatSource(byte[] source, DefinitionDocumentRepresentation representation)
    {
        if (representation == DefinitionDocumentRepresentation.Environment)
        {
            DotenvParseResult parsed = DotenvCodec.Parse(source, DotenvPurpose.EditableValues);
            if (!parsed.Succeeded) return null;
            DotenvRenderResult rendered = DotenvCodec.Render(parsed.Document!, true, DotenvPurpose.EditableValues);
            return rendered.Succeeded ? rendered.Bytes.ToArray() : null;
        }
        byte[] canonical = AnswerSnapshotCodec.Serialize(AnswerSnapshotCodec.ParseJson(source));
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(canonical);
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true })) document.WriteTo(writer);
            return buffer.WrittenSpan.ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(canonical); buffer.Clear(); }
    }

    /// <summary>Waits for an explicit Apply/Discard/Stay choice; clean preferences never enter this gate.</summary>
    public async Task<bool> ResolvePendingAsync()
    {
        if (_disposed) return false;
        if (!IsDirty && !_busy) return true;
        if (_resolution is null)
        {
            _resolution = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _focusResolution = true;
        }
        Task<bool> decision = _resolution.Task;
        await InvokeAsync(StateHasChanged);
        if (_disposed) return false;
        return await decision;
    }

    private async Task ResolveAsync(string choice)
    {
        if (_resolution is not { } resolution || _busy) return;
        long transition = _transitionGeneration;
        if (choice == "apply")
        {
            bool applied = await ApplyAsync();
            if (_disposed || transition != _transitionGeneration || !ReferenceEquals(_resolution, resolution)) return;
            if (!applied || IsDirty || _busy) return;
        }
        _resolution = null;
        if (choice == "discard") await RetireAsync();
        if (_disposed) { resolution.TrySetResult(false); return; }
        resolution.TrySetResult(choice != "stay");
        if (choice == "stay") await FocusEditorAsync();
    }

    private Task PendingKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? ResolveAsync("stay") : Task.CompletedTask;
    private async Task FocusEditorAsync()
    {
        if (_session is not { } session || _bridge is not { } bridge || !_modelReady || !Permitted) return;
        long generation = _generation;
        CancellationToken token = _lifetime.Token;
        await bridge.InvokeVoidAsync("focus", generation, _version);
        if (!LeaseCurrent(generation, session, token)) return;
    }

    private async Task RepresentationChangedAsync(ChangeEventArgs args)
    {
        DefinitionDocumentRepresentation representation = args.Value?.ToString() == "environment"
            ? DefinitionDocumentRepresentation.Environment : DefinitionDocumentRepresentation.Canonical;
        if (representation == _representation || representation == DefinitionDocumentRepresentation.Environment && EnvironmentArtifactId is null) return;
        DefinitionFormModel form = Form;
        long transition = ++_transitionGeneration;
        if (!await ResolvePendingAsync() || _disposed || transition != _transitionGeneration || !ReferenceEquals(Form, form)) return;
        await RetireAsync();
        if (_disposed || transition != _transitionGeneration || !ReferenceEquals(Form, form)) return;
        _artifactId = EnvironmentArtifactId;
        _representation = representation;
        _mode = DefinitionDocumentApplyMode.Merge;
        Reopen();
    }

    private void ModeChanged(ChangeEventArgs args)
    {
        _mode = args.Value?.ToString() == "replace" ? DefinitionDocumentApplyMode.ReplaceRepresentedScope : DefinitionDocumentApplyMode.Merge;
        _session?.InvalidateText();
        CancelOperation();
        _diagnostics = [];
        SetStatus("Changed", "developer-mode-changed");
    }

    private async Task ReopenAsync()
    {
        DefinitionFormModel form = Form;
        await RetireAsync();
        if (_disposed || !ReferenceEquals(Form, form)) return;
        Reopen();
    }

    private void Reopen()
    {
        _artifactId = EnvironmentArtifactId;
        if (_representation == DefinitionDocumentRepresentation.Environment && _artifactId is null)
            _representation = DefinitionDocumentRepresentation.Canonical;
        _retired = false;
        _mountFailed = false;
        _historyLimited = false;
        SetStatus("Ready", "developer-ready");
        OpenMetadata();
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _observedForm) || !ReferenceEquals(_observedForm, Form)) return;
        if (_applying && Form.InputRevision == _applyingRevision) return;
        bool consentEnabled = _privacy == DefinitionSessionPrivacy.BrowserNoSecrets && Permitted;
        _privacy = Form.SessionPrivacy;
        _retirement = RetireAsync();
        OpenMetadata();
        if (consentEnabled) _retired = false;
        SetStatus(Permitted ? "Retired" : "ConsentPending", Permitted ? "developer-retired" : "developer-consent-required");
        _ = InvokeAsync(StateHasChanged);
    }

    private void ViewChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _observedForm)) return;
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task SetDirtyAsync(bool value)
    {
        if (IsDirty == value) return;
        IsDirty = value;
        await DirtyChanged.InvokeAsync(value);
    }

    private void SetStatus(string status, string code) { _status = status; _statusCode = code; }

    private async Task CancelEditingAsync()
    {
        Task retirement = RetireAsync();
        long generation = _generation;
        await retirement;
        if (!_disposed && generation == _generation && Permitted && _retired) _focusRetired = true;
    }

    /// <summary>Immediately invalidates managed authority, then destroys JS before releasing its callback.</summary>
    public Task RetireAsync()
    {
        if (_disposed && _observedForm is null) return _retirement;
        long generation = ++_generation;
        _focusRetired = false;
        if (_mountSource is { } source) CryptographicOperations.ZeroMemory(source);
        _mountSource = null;
        _retired = true;
        _modelReady = false;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _lifetime = new();
        CancelOperation();
        _session?.Dispose();
        _session = null;
        _diagnostics = [];
        _diagnosticsTruncated = false;
        _historyLimited = false;
        _resolution?.TrySetResult(false);
        _resolution = null;
        DotNetObjectReference<ConfigurationDeveloper>? callback = _callback;
        _callback = null;
        bool wasDirty = IsDirty;
        IsDirty = false;
        SetStatus("Retired", "developer-retired");
        return _retirement = FinishRetirementAsync(_retirement, _bridge, callback, generation, wasDirty);
    }

    private async Task FinishRetirementAsync(Task previous, IJSObjectReference? bridge,
        DotNetObjectReference<ConfigurationDeveloper>? callback, long generation, bool wasDirty)
    {
        try
        {
            // WASM's in-process invocation closes the DOM/model boundary synchronously.
            if (bridge is IJSInProcessObjectReference synchronous) synchronous.InvokeVoid("retire", generation);
            else if (bridge is not null) await bridge.InvokeVoidAsync("retire", generation);
        }
        catch (JSException) { if (!_disposed) SetStatus("Unavailable", "developer-retire-failed"); }
        finally { callback?.Dispose(); }
        await previous;
        if (_disposed || generation != _generation) return;
        if (wasDirty) await DirtyChanged.InvokeAsync(false);
        if (_disposed || generation != _generation) return;
        await InvokeAsync(StateHasChanged);
    }

    private void Unsubscribe()
    {
        if (_observedForm is null) return;
        _observedForm.InputChanged -= InputChanged;
        _observedForm.ViewChanged -= ViewChanged;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Unsubscribe();
        await RetireAsync();
        _lifetime.Dispose();
        if (_bridge is not null) await _bridge.DisposeAsync();
        if (_module is not null) await _module.DisposeAsync();
        if (_rendering is not null) await _rendering.DisposeAsync();
        _bridge = null;
        _module = null;
        _rendering = null;
        _observedForm = null;
        GC.SuppressFinalize(this);
    }

    public sealed record CompletionItem(string Label, string Apply, string Detail, string Type);
    public sealed record EditorOptions(byte[] Source, string Representation, string Label, string DescribedBy,
        string Locale, IReadOnlyList<CompletionItem> CompletionItems, Dictionary<string, string> Phrases);
    public sealed record EditorState(long Version, bool Dirty, bool HistoryLimited);
}
