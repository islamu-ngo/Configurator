namespace Configurator.Terminal;

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

/// <summary>
/// Revision-bound public-scope document dialog enforcing the 256 KiB interactive limit,
/// pre-apply diff preview, stale-revision rejection and explicit buffer zeroization.
/// </summary>
internal sealed class TerminalDocumentDialog : Dialog
{
    internal const int MaximumDocumentUtf8Bytes = 256 * 1024;
    private readonly DefinitionFormModel _form;
    private readonly DefinitionDocumentSession _session;
    private readonly ListView _documentPreview;
    private readonly TextField _lineEditor;
    private readonly Label _diffLabel;
    private readonly Label _statusLabel;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _cancel;
    private string[] _lines = [];
    private int _selectedLine;
    private byte[] _workingBytes = [];
    private DefinitionDocumentParseResult? _lastParse;
    private DefinitionDocumentCandidate? _candidate;

    internal TerminalDocumentDialog(DefinitionFormModel form,
        DefinitionDocumentRepresentation representation = DefinitionDocumentRepresentation.Canonical,
        string? artifactId = null, IApplication? application = null)
    {
        _form = form;
        _session = form.OpenDocument(representation, artifactId);
        Title = TerminalStrings.Get("DocumentTitle", form.Locale);
        X = Pos.Center();
        Y = Pos.Center();
        Width = 72;
        Height = 18;

        _diffLabel = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = 2 };
        _documentPreview = new ListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(5),
            CanFocus = true
        };
        _lineEditor = new TextField
        {
            X = 0,
            Y = Pos.Bottom(_documentPreview),
            Width = Dim.Fill(),
            Height = 1
        };
        _statusLabel = new Label { X = 0, Y = Pos.Bottom(_lineEditor), Width = Dim.Fill(), Height = 1 };

        LoadInitialSource();

        _documentPreview.ValueChanged += (_, _) =>
        {
            if (_documentPreview.SelectedItem is { } selected && selected >= 0 && selected < _lines.Length)
            {
                _selectedLine = selected;
                _lineEditor.Text = _lines[_selectedLine];
            }
        };
        _lineEditor.ValueChanged += (_, _) =>
        {
            if (_selectedLine >= 0 && _selectedLine < _lines.Length)
                _lines[_selectedLine] = _lineEditor.Text;
            _session.InvalidateText();
            DisposeCandidate();
            _documentPreview.SetSource(new ObservableCollection<string>(_lines));
        };

        _preview = new Button { Text = TerminalStrings.Get("DialogPreview", form.Locale) };
        _preview.Accepting += (_, args) =>
        {
            args.Handled = true;
            byte[] utf8 = Encoding.UTF8.GetBytes(string.Join('\n', _lines));
            try { TryPreparePreview(utf8); }
            finally { CryptographicOperations.ZeroMemory(utf8); }
        };
        _apply = new Button { Text = TerminalStrings.Get("DialogApply", form.Locale), IsDefault = true };
        _apply.Accepting += (_, args) =>
        {
            args.Handled = true;
            DefinitionDocumentApplyResult result = ApplyPrepared();
            if (result.Succeeded) application?.RequestStop(this);
        };
        _cancel = new Button { Text = TerminalStrings.Get("DialogCancel", form.Locale) };
        _cancel.Accepting += (_, args) =>
        {
            args.Handled = true;
            ClearSensitiveBuffers();
            application?.RequestStop(this);
        };

        Add(_diffLabel, _documentPreview, _lineEditor, _statusLabel);
        AddButton(_preview);
        AddButton(_apply);
        AddButton(_cancel);
    }

    internal IReadOnlyList<DefinitionDocumentChange> PreviewChanges => _candidate?.Changes ?? [];
    internal int BufferByteLength => _workingBytes.Length;

    internal DefinitionDocumentParseResult TryPreparePreview(ReadOnlyMemory<byte> utf8,
        DefinitionDocumentApplyMode mode = DefinitionDocumentApplyMode.Merge)
    {
        DisposeCandidate();
        ZeroWorkingBytes();
        if (utf8.Length > MaximumDocumentUtf8Bytes)
        {
            _statusLabel.Text = TerminalStrings.Get("DocumentTooLarge", _form.Locale);
            return DefinitionDocumentParseResult.Reject("document-limit");
        }

        _workingBytes = utf8.ToArray();
        _lastParse = _session.Parse(_workingBytes, mode);
        _candidate = _lastParse.Candidate;
        if (_lastParse.Succeeded && _candidate is not null)
        {
            string count = string.Format(TerminalStrings.Culture(_form.Locale),
                TerminalStrings.Get("DiffSummaryFormat", _form.Locale), _candidate.Changes.Count);
            string paths = string.Join(", ", _candidate.Changes.Take(5)
                .Select(change => $"{change.Kind}:{change.Path.ToPointer()}"));
            _diffLabel.Text = Sanitize(paths.Length > 0 ? $"{count} ({paths})" : count);
            _statusLabel.Text = TerminalStrings.Get("DocumentPreviewReady", _form.Locale);
        }
        else
        {
            string codes = string.Join(", ", _lastParse.Diagnostics.Select(d => d.Code));
            _diffLabel.Text = string.Empty;
            _statusLabel.Text = Sanitize(codes);
        }
        return _lastParse;
    }

    internal DefinitionDocumentApplyResult ApplyPrepared(DefinitionDocumentApplyMode mode = DefinitionDocumentApplyMode.Merge)
    {
        if (_candidate is null)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(string.Join('\n', _lines));
            try
            {
                DefinitionDocumentParseResult preview = TryPreparePreview(utf8, mode);
                if (!preview.Succeeded || _candidate is null)
                {
                    string code = preview.Diagnostics.Count > 0 ? preview.Diagnostics[0].Code : "document-invalid";
                    return new(false, code, _form.InputRevision);
                }
            }
            finally { CryptographicOperations.ZeroMemory(utf8); }
        }

        DefinitionDocumentApplyResult result = _session.Apply(_candidate);
        if (result.Succeeded)
        {
            ClearSensitiveBuffers();
            _statusLabel.Text = TerminalStrings.Get("DialogApply", _form.Locale);
            return result;
        }
        _statusLabel.Text = result.Code == "document-stale"
            ? TerminalStrings.Get("DocumentStale", _form.Locale)
            : Sanitize(result.Code);
        return result;
    }

    internal string ExportAllVisibleText() =>
        string.Join("\n", [Title, _diffLabel.Text, _lineEditor.Text, _statusLabel.Text]);

    private void LoadInitialSource()
    {
        if (_form.SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
        {
            _statusLabel.Text = TerminalStrings.Get("DocumentStale", _form.Locale);
            return;
        }
        byte[] source = _session.ReadSource();
        try
        {
            if (source.Length > MaximumDocumentUtf8Bytes)
            {
                _statusLabel.Text = TerminalStrings.Get("DocumentTooLarge", _form.Locale);
                return;
            }
            _workingBytes = source;
            source = [];
            string initial = Encoding.UTF8.GetString(_workingBytes).Replace("\r\n", "\n", StringComparison.Ordinal);
            _lines = initial.Split('\n');
            _selectedLine = 0;
            _documentPreview.SetSource(new ObservableCollection<string>(_lines.Select(Sanitize).ToArray()));
            if (_lines.Length > 0)
            {
                _documentPreview.SelectedItem = 0;
                _lineEditor.Text = _lines[0];
            }
        }
        finally
        {
            if (source.Length > 0) CryptographicOperations.ZeroMemory(source);
        }
    }

    private void ClearSensitiveBuffers()
    {
        ZeroWorkingBytes();
        DisposeCandidate();
        Array.Clear(_lines);
        _lines = [];
        _lineEditor.Text = string.Empty;
        _lineEditor.ClearHistoryChanges();
        _documentPreview.SetSource(new ObservableCollection<string>());
        _diffLabel.Text = string.Empty;
    }

    private void DisposeCandidate()
    {
        _candidate = null;
        _lastParse?.Dispose();
        _lastParse = null;
    }

    private void ZeroWorkingBytes()
    {
        if (_workingBytes.Length == 0) return;
        CryptographicOperations.ZeroMemory(_workingBytes);
        _workingBytes = [];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearSensitiveBuffers();
            _session.Dispose();
        }
        base.Dispose(disposing);
    }

    private static string Sanitize(string text) =>
        new(text.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
}
