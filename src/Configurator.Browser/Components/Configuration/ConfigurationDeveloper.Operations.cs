namespace Configurator.Browser.Components.Configuration;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;
using Configurator.Presentation;
using Microsoft.JSInterop;

public partial class ConfigurationDeveloper
{
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
        if (_formatSource is { } source) CryptographicOperations.ZeroMemory(source);
        _formatSource = null;
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
                try { _formatSource = formatted = FormatSource(source, session.Representation); }
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
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (JSException)
        {
            if (LeaseCurrent(generation, session, token)) SetStatus("Unavailable", "developer-unavailable");
        }
        finally
        {
            if (formatted is not null) CryptographicOperations.ZeroMemory(formatted);
            if (ReferenceEquals(_formatSource, formatted)) _formatSource = null;
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

}
