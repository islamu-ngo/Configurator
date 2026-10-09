namespace Configurator.Browser;

using System.Security.Cryptography;
using Configurator.Core.Definitions;
using Microsoft.JSInterop;

/// <summary>Admits public definitions before answers and fences late source completions.</summary>
public sealed class BrowserDefinitionSource(BrowserDefinitionRegistry registry, IJSRuntime javascript, BrowserOfflinePolicy policy) : IAsyncDisposable
{
    public const int MaximumDocumentBytes = 4 * 1024 * 1024;
    private IJSObjectReference? _module;
    private CancellationTokenSource? _pending;
    private long _generation;
    private bool _disposed;

    public async Task<BrowserLaunchSelection> FetchAsync(BrowserDefinitionPointer request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        BrowserDefinitionPointer.ValidateSource(request.Source);
        // Validate constructed requests as well as parsed fragments at this public seam.
        request = BrowserDefinitionPointer.Parse("#src=" + Uri.EscapeDataString(request.Source.AbsoluteUri) + Selectors(request));
        long generation = Begin(cancellationToken, out CancellationTokenSource cancellation);
        byte[]? bytes = null;
        try
        {
            if (!policy.CanFetchDefinition)
            {
                SetupDefinition? retained = !policy.IsWorkspaceActive && request.Digest is { } digest
                    ? await policy.ReadRetainedDefinitionAsync(digest) : null;
                CheckCurrent(generation, cancellation.Token);
                if (retained is null) throw new BrowserLaunchException("source-offline-or-active");
                BrowserLaunchSelection cachedSelection = Select(retained, request);
                registry.Admit(retained);
                return cachedSelection;
            }
            _module ??= await javascript.InvokeAsync<IJSObjectReference>("import", cancellation.Token, "./browser-definition-source.js");
            CheckCurrent(generation, cancellation.Token);
            using CancellationTokenRegistration registration = cancellation.Token.Register(() => { _ = AbortRemoteAsync(); });
            bytes = await _module.InvokeAsync<byte[]>("fetchDefinition", cancellation.Token, request.Source.AbsoluteUri, MaximumDocumentBytes);
            CheckCurrent(generation, cancellation.Token);
            if (bytes is null || bytes.Length is 0 or > MaximumDocumentBytes) throw new BrowserLaunchException("source-size-invalid");
            SetupDefinition definition = SetupDefinitionCodec.ParseJson(bytes);
            BrowserLaunchSelection selection = Select(definition, request);
            CheckCurrent(generation, cancellation.Token);
            registry.Admit(definition);
            return selection;
        }
        catch (SetupDefinitionException exception) { throw new BrowserLaunchException(exception.Code); }
        catch (OperationCanceledException) { throw new BrowserLaunchException("source-cancelled"); }
        catch (JSException) { throw new BrowserLaunchException("source-unavailable"); }
        finally
        {
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            Finish(cancellation);
        }
    }

    public async Task<SetupDefinition> ReadLocalAsync(Stream input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (policy.IsWorkspaceActive) throw new BrowserLaunchException("source-active");
        long generation = Begin(cancellationToken, out CancellationTokenSource cancellation);
        byte[] bytes = new byte[MaximumDocumentBytes + 1];
        try
        {
            int length = 0;
            while (length < bytes.Length)
            {
                int read = await input.ReadAsync(bytes.AsMemory(length), cancellation.Token);
                CheckCurrent(generation, cancellation.Token);
                if (read == 0) break;
                length += read;
            }
            if (length is 0 or > MaximumDocumentBytes) throw new BrowserLaunchException("source-size-invalid");
            SetupDefinition definition = SetupDefinitionCodec.ParseJson(bytes.AsMemory(0, length));
            CheckCurrent(generation, cancellation.Token);
            registry.Admit(definition);
            return definition;
        }
        catch (SetupDefinitionException exception) { throw new BrowserLaunchException(exception.Code); }
        catch (OperationCanceledException) { throw new BrowserLaunchException("source-cancelled"); }
        catch (IOException) { throw new BrowserLaunchException("source-unavailable"); }
        finally { CryptographicOperations.ZeroMemory(bytes); Finish(cancellation); }
    }

    private static string Selectors(BrowserDefinitionPointer request)
    {
        var values = new[] { ("digest", request.Digest), ("appVersion", request.AppVersion), ("entry", request.Entry), ("locale", request.Locale) };
        return string.Concat(values.Where(item => item.Item2 is not null).Select(item => "&" + item.Item1 + "=" + Uri.EscapeDataString(item.Item2!)));
    }

    private static BrowserLaunchSelection Select(SetupDefinition definition, BrowserDefinitionPointer request) =>
        BrowserLaunchSelection.Resolve(new SetupDefinitionRegistry([definition]), definition.FamilyId.ToString("D"), Selectors(request).TrimStart('&'));

    private long Begin(CancellationToken token, out CancellationTokenSource cancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Invalidate();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _pending = cancellation;
        return _generation;
    }

    private void CheckCurrent(long generation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_disposed || generation != _generation) throw new BrowserLaunchException("source-invalidated");
    }

    private void Finish(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(_pending, cancellation)) _pending = null;
        cancellation.Dispose();
    }

    public void Invalidate()
    {
        _generation++;
        _pending?.Cancel();
    }

    private async Task AbortRemoteAsync()
    {
        if (_module is null) return;
        try { await _module.InvokeVoidAsync("cancelFetch"); }
        catch (JSException) { }
        catch (ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Invalidate();
        await AbortRemoteAsync();
        if (_module is not null) await _module.DisposeAsync();
        _module = null;
    }
}
