namespace Configurator.Terminal;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.Text;
using global::Terminal.Gui.ViewBase;

internal sealed partial class TerminalWindow
{
    private void EditorChanged()
    {
        if (_loading || _disposed || Volatile.Read(ref _closeRequested) != 0) return;
        _draftEdited = true;
        _confirmRemove = false;
        _importOperation.RequestCancellationAsync();
        _operation.Invalidate();
        if (_slot is { IsMap: true })
        {
            _draftEdited = false;
            Status("MapKeyHelp");
            return;
        }
        Status(CommitField() ? "InputChanged" : "DraftInvalid");
        ShowFieldLabel(VisibleSlots.Length);
        ShowHelp();
        RefreshCommands();
    }

    internal bool CommitField()
    {
        if (Volatile.Read(ref _closeRequested) != 0) return false;
        if (_slot is null) return true;
        if (_slot.IsMap) { _draftEdited = false; return true; }
        if (!_draftEdited) return _draftValid;
        _draftEdited = false;
        byte[] bytes = [];
        try
        {
            string text = _value.Text;
            if (_slot.RequiresMasking)
            {
                bytes = _secret.Count == 0 ? [] : _secret.CopyUtf8Bytes();
                text = Encoding.UTF8.GetString(bytes);
            }
            _draftValid = TerminalFieldEditor.Write(_slot, text, _itemIndex);
            if (!_draftValid)
            {
                Status("DraftInvalid");
                return false;
            }
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal void MoveField(int delta)
    {
        CommitField();
        var slots = VisibleSlots;
        if (slots.Length == 0) { ShowField(); return; }
        int current = _slot is null ? 0 : slots.ToList().FindIndex(slot =>
            slot.Identity == _slot.Identity);
        _fieldIndex = (Math.Max(0, current) + delta + slots.Length) % slots.Length;
        if (_slot is { IsObjectList: true } collection && delta > 0)
        {
            string[] rowPath = [.. collection.Location.Segments, _itemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            int firstRowField = slots.ToList().FindIndex(slot =>
                slot.Location.Segments.Count > rowPath.Length && slot.Location.Segments.Take(rowPath.Length).SequenceEqual(rowPath));
            if (firstRowField >= 0) _fieldIndex = firstRowField;
        }
        _itemIndex = 0;
        _helpPage = 0;
        ShowField(preserveLocation: false);
        FocusInput();
    }

    private void MoveItem(int delta)
    {
        if (_slot is null || !_slot.IsList) return;
        CommitField();
        int requested = _itemIndex + delta;
        _itemIndex = Math.Clamp(requested, 0, TerminalFieldEditor.LastItemPosition(_slot));
        _helpPage = 0;
        ShowField();
        if (requested > _itemIndex) Status("ListBound");
        FocusInput();
    }

    private void RemoveItem()
    {
        if (_slot is null || !_slot.IsList) return;
        if (!_confirmRemove && TerminalFieldEditor.HasItem(_slot, _itemIndex))
        {
            _confirmRemove = true;
            Status("ConfirmRemove");
            return;
        }
        TerminalFieldEditor.Remove(_slot, _itemIndex);
        _itemIndex = Math.Min(_itemIndex, Math.Max(0, TerminalFieldEditor.ItemCount(_slot) - 1));
        ShowField();
        FocusInput();
    }

    private void AddItem()
    {
        if (_slot is { IsMap: true } map)
        {
            byte[] keyBytes = _secret.Count == 0 ? [] : _secret.CopyUtf8Bytes();
            try
            {
                string key = Encoding.UTF8.GetString(keyBytes);
                if (!TerminalFieldEditor.AddMapEntry(map, key)) { Status("MapKeyInvalid"); return; }
                DefinitionPath path = DefinitionPath.FromSegments([.. map.Location.Segments, key]);
                _operation.Form.FocusEditor(path);
                var slots = VisibleSlots;
                _fieldIndex = slots.ToList().FindIndex(slot => slot.Location.Equals(path));
                ShowField(preserveLocation: false);
                FocusInput();
            }
            finally { CryptographicOperations.ZeroMemory(keyBytes); }
            return;
        }
        if (_slot is not { IsList: true } collection || !CommitField()) return;
        if (TerminalFieldEditor.ItemCount(collection) >= TerminalFieldEditor.MaximumItems(collection))
        {
            Status("ListBound");
            return;
        }
        if (!collection.IsObjectList)
        {
            if (!TerminalFieldEditor.AddItem(collection)) { Status("DraftInvalid"); return; }
            _itemIndex = TerminalFieldEditor.ItemCount(collection) - 1;
            ShowField();
            FocusInput();
            return;
        }
        if (!TerminalFieldEditor.AddItem(collection)) { Status("DraftInvalid"); return; }
        _itemIndex = TerminalFieldEditor.ItemCount(collection) - 1;
        MoveField(1);
    }

    private void SupplyContainer()
    {
        if (!CommitField()) return;
        if (_slot is null || !TerminalFieldEditor.Supply(_slot)) { Status("DraftInvalid"); return; }
        ShowField();
        FocusInput();
    }

    private void ClearContainer()
    {
        if (_slot is null || !TerminalFieldEditor.Clear(_slot)) { Status("DraftInvalid"); return; }
        _itemIndex = 0;
        ShowField();
        FocusInput();
    }

    private void ShowField(bool preserveLocation = true)
    {
        RefreshLocale();
        ShowArtifact();
        _loading = true;
        try
        {
            _confirmRemove = false;
            _draftEdited = false;
            _draftValid = true;
            _masked.ClearSensitiveState();
            string publicText = string.Empty;
            var slots = VisibleSlots;
            if (preserveLocation && _slot is not null)
            {
                int current = slots.ToList().FindIndex(slot => slot.Identity == _slot.Identity);
                if (current >= 0) _fieldIndex = current;
            }
            _fieldIndex = Math.Clamp(_fieldIndex, 0, Math.Max(0, slots.Length - 1));
            _slot = slots.Length == 0 ? null : slots[_fieldIndex];
            if (_slot?.IsList == true) _itemIndex = Math.Clamp(_itemIndex, 0, TerminalFieldEditor.LastItemPosition(_slot));
            ShowFieldLabel(slots.Length);
            ShowHelp();
            if (_slot is not null)
            {
                string text = TerminalFieldEditor.Read(_slot, _itemIndex);
                _draftValid = !_operation.Form.HasInputError(TerminalFieldEditor.EditLocation(_slot, _itemIndex));
                if (_slot.RequiresMasking && text.Length > 0) _masked.TryReplaceSensitiveInput(text);
                else if (!_slot.RequiresMasking) publicText = text;
            }
            if (_value.Text != publicText) _value.Text = publicText;
            SetEditorVisibility();
            var experience = _operation.Form.DisplayExperiences.Single(item => item.Id == _operation.Form.SelectedExperienceId);
            _experience.Text = Safe(experience.Title);
            _experience.Enabled = _operation.Form.Experiences.Count > 1;
            _optionalEditors.Text = _operation.Form.OptionalEditorsExpanded
                ? LocalizedText("DefaultDisclosure") : LocalizedText("OptionalEditors");
            _locale.Text = LocalizedText("Locale") + ": " + _operation.Form.Locale + " (" + _operation.Form.Direction + ")";
            _locale.Enabled = _operation.Form.Definition.Locales.Count > 1;
            bool rtl = _operation.Form.Direction == "rtl";
            // Align RTL metadata without reversing Latin fallback or technical text.
            _fieldLabel.TextDirection = _help.TextDirection = _experience.TextDirection = TextDirection.LeftRight_TopBottom;
            _fieldLabel.TextAlignment = _help.TextAlignment = _experience.TextAlignment = rtl ? Alignment.End : Alignment.Start;
            RefreshCommands();
            if (_operation.Form.ImportComparison is not null) ShowComparison();
        }
        finally { _loading = false; }
    }

    private void ShowFieldLabel(int count) => _fieldLabel.Text = _slot is null ? LocalizedText("NoFields")
        : Safe($"{LocalizedNumber(_fieldIndex + 1)}/{LocalizedNumber(count)} {_slot.Title} ({_slot.ScalarType})"
            + (_slot.IsList ? $" [{LocalizedNumber(_itemIndex + 1)}/{LocalizedNumber(TerminalFieldEditor.ItemCount(_slot) + 1)}]" : "")
            + (_operation.Form.GetEditorState(_slot.Location)?.IsRequired == true ? " | " + LocalizedText("Required") : "")
            + (TerminalFieldEditor.Value(_slot) is { ValueKind: JsonValueKind.Null } ? " | " + LocalizedText("NullValue")
                : _operation.Form.AnswerOrigin(_slot.Location) switch
                {
                    DefinitionAnswerOrigin.Omitted => " | " + LocalizedText("Omitted"),
                    DefinitionAnswerOrigin.Generated => " | " + LocalizedText("GeneratedValue"),
                    _ => ""
                }));

    private void SetEditorVisibility()
    {
        bool scalar = !_tooSmall && _slot is { IsObject: false, IsObjectList: false }
            && (!_slot.IsList || TerminalFieldEditor.MaximumItems(_slot) > 0);
        _value.Visible = scalar && _slot is { RequiresMasking: false };
        _masked.Visible = scalar && _slot is { RequiresMasking: true } || !_tooSmall && _slot is { IsMap: true };
        _addItem.Visible = !_tooSmall && (_slot is { IsList: true } or { IsMap: true });
        _addItem.Text = LocalizedText(_slot is { IsMap: true } ? "AddMapEntry" : "AddItem");
        _supplyContainer.Visible = !_tooSmall && _slot is { IsContainer: true } && _slot.Schema.Variants.Count == 0;
        _clearContainer.Visible = !_tooSmall && _slot is not null && _slot.Location.Segments.Count > 0;
        _clearContainer.Text = LocalizedText("OmitValue");
        _choice.Visible = !_tooSmall && _slot is not null &&
            (_slot.Schema.Variants.Count > 0 || TerminalFieldEditor.Choices(_slot).Count > 0);
        _choice.Text = LocalizedText(_slot is not null && _slot.Schema.Variants.Count > 0 ? "ChooseVariant" : "Choose");
        DefinitionFieldValidation? facts = _slot is null ? null : _operation.Form.GetFieldValidation(_slot.Location);
        _value.ReadOnly = _masked.ReadOnly = facts?.CanEdit != true;
        _addItem.Enabled = _supplyContainer.Enabled = _choice.Enabled = facts?.CanEdit == true;
        _clearContainer.Enabled = facts?.CanOmit == true;
        _null.Visible = !_tooSmall && facts?.CanSetNull == true;
        _reset.Visible = !_tooSmall && facts?.CanReset == true;
        _suggest.Visible = !_tooSmall && facts?.CanApplySuggestion == true;
        for (int index = 0; index < _itemActions.Length; index++)
            _itemActions[index].Visible = !_tooSmall && _slot is { IsList: true, IsObjectList: false } &&
                TerminalFieldEditor.HasItem(_slot, _itemIndex) &&
                (index == 0 ? _operation.Form.GetFieldValidation(TerminalFieldEditor.EditLocation(_slot, _itemIndex)).CanSetNull
                    : _operation.Form.GetFieldValidation(TerminalFieldEditor.EditLocation(_slot, _itemIndex)).CanApplySuggestion);
        foreach (var button in _listButtons) button.Visible = !_tooSmall && _slot is { IsList: true };
        bool secret = scalar && _slot is { RequiresMasking: true };
        _generateSecret.Visible = secret;
        _revealSecret.Visible = secret;
        _copySecret.Visible = secret;
        _revealSecret.Text = LocalizedText(_masked.IsRevealed ? "HideSecret" : "ShowSecret");
    }

    private void FocusInput()
    {
        if (_slot is not null && _slot.Schema.Variants.Count > 0) _choice.SetFocus();
        else if (_slot?.IsMap == true) _masked.SetFocus();
        else if (_slot?.IsObject == true) _supplyContainer.SetFocus();
        else if (_slot?.IsObjectList == true) _addItem.SetFocus();
        else if (_slot is { IsList: true } && TerminalFieldEditor.MaximumItems(_slot) == 0) _supplyContainer.SetFocus();
        else if (_slot?.RequiresMasking == true) _masked.SetFocus();
        else _value.SetFocus();
    }

    internal void CycleExperience()
    {
        var form = _operation.Form;
        var experiences = form.Experiences;
        int current = experiences.ToList().FindIndex(item => item.Id == form.SelectedExperienceId);
        form.SelectExperience(experiences[(current + 1) % experiences.Count].Id);
    }

    internal void ToggleOptionalEditors() =>
        _operation.Form.SetAllSectionsExpanded(!_operation.Form.OptionalEditorsExpanded);

    private void FormViewChanged(object? sender, EventArgs args) =>
        DispatchToOwner(static window =>
        {
            if (Volatile.Read(ref window._closeRequested) == 0) window.ShowField();
        });

    private void CycleChoice()
    {
        if (_slot is null) return;
        if (_slot.Schema.Variants.Count > 0)
        {
            int currentVariant = _slot.Schema.Variants.ToList().FindIndex(variant => ReferenceEquals(variant, _slot.ObjectSchema));
            int nextVariant = (currentVariant + 1) % _slot.Schema.Variants.Count;
            if (!_operation.Form.SelectVariant(_slot.Location, nextVariant)) { Status("DraftInvalid"); return; }
            ShowField();
            _status.Text = Safe(_operation.Form.GetVariantTitle(_slot.Location, nextVariant) ?? "");
            FocusInput();
            return;
        }
        var choices = TerminalFieldEditor.Choices(_slot);
        if (choices.Count == 0) return;
        if (!CommitField()) return;
        int current = TerminalFieldEditor.ChoiceIndex(_slot, _itemIndex);
        if (current < 0 && (_slot.IsList ? TerminalFieldEditor.HasItem(_slot, _itemIndex)
            : TerminalFieldEditor.Value(_slot) is not null))
        {
            Status("DraftInvalid");
            return;
        }
        int next = (current + 1) % choices.Count;
        if (!TerminalFieldEditor.SelectChoice(_slot, next, _itemIndex)) { Status("DraftInvalid"); return; }
        ShowField();
        if (!_slot.RequiresMasking) _status.Text = Safe($"{LocalizedNumber(next + 1)}/{LocalizedNumber(choices.Count)} " + choices[next]);
        FocusInput();
    }

    private void EditTyped(string action, bool item = false)
    {
        if (_slot is null) return;
        DefinitionPath path = item ? TerminalFieldEditor.EditLocation(_slot, _itemIndex) : _slot.Location;
        DefinitionFieldValidation facts = _operation.Form.GetFieldValidation(path);
        if (!(action switch { "SetNull" => facts.CanSetNull, "ResetValue" => facts.CanReset, _ => facts.CanApplySuggestion }))
        {
            Status("DraftInvalid");
            return;
        }
        bool accepted = action switch
        {
            "SetNull" => _operation.Form.SetNull(path),
            "ResetValue" => _operation.Form.ResetValue(path),
            _ => _operation.Form.ApplySuggestion(path)
        };
        if (!accepted) { Status("DraftInvalid"); return; }
        ShowField();
        Status("InputChanged");
        FocusInput();
    }

    private void CycleLocale()
    {
        var form = _operation.Form;
        var locales = form.Definition.Locales;
        int current = locales.ToList().IndexOf(form.Locale);
        _helpPage = 0;
        form.SelectLocale(locales[(current + 1) % locales.Count]);
    }

    private void ShowHelp()
    {
        string text = _slot is null ? "" : _slot.Title + " (" + _slot.Type + ")\n" + _slot.Help;
        if (_slot is not null)
        {
            DefinitionPath path = _slot.IsList && TerminalFieldEditor.HasItem(_slot, _itemIndex)
                ? TerminalFieldEditor.EditLocation(_slot, _itemIndex) : _slot.Location;
            string correction = PresentationStrings.FieldCorrection(_operation.Form.GetFieldValidation(path), _operation.Form.Locale);
            if (correction.Length > 0) text += "\n" + correction;
        }
        if (_slot is not null && _slot.Schema.Variants.Count > 0)
        {
            int selected = _slot.Schema.Variants.ToList().FindIndex(variant => ReferenceEquals(variant, _slot.ObjectSchema));
            if (selected >= 0)
                text += $"\n{LocalizedNumber(selected + 1)}/{LocalizedNumber(_slot.Schema.Variants.Count)} " +
                    _operation.Form.GetVariantTitle(_slot.Location, selected);
        }
        if (_slot is { IsMap: true }) text = LocalizedText("MapKeyHelp") + "\n" + text;
        if (_slot is { IsList: true } list && TerminalFieldEditor.HasItem(list, _itemIndex))
            text += "\n" + TerminalStrings.FormatHelp(
                _operation.Form.GetEditorPresentation(TerminalFieldEditor.EditLocation(list, _itemIndex)), list.Schema.Items!.Keywords, _operation.Form.Locale);
        if (_slot is { RequiresMasking: true, IsObject: false, IsObjectList: false })
        {
            DefinitionSecretValidation rules = _operation.Form.GetSecretValidation(
                TerminalFieldEditor.EditLocation(_slot, _itemIndex));
            text += "\n" + string.Join("\n", rules.Requirements.Select(rule => rule.Message)
                .Concat(rules.Failures.Select(rule => LocalizedText("SecretRuleFailed") + " " + rule.Message)));
        }
        IReadOnlyList<string> pages = TerminalStrings.HelpPages(text, Math.Max(2, (Viewport.Width - 4) / 2));
        _helpPage %= Math.Max(1, pages.Count);
        _help.Text = pages.Count > 0 ? pages[_helpPage] : "";
        _helpNext.Text = LocalizedText("Help") + $" {LocalizedNumber(_helpPage + 1)}/{LocalizedNumber(Math.Max(1, pages.Count))}";
        _helpNext.Enabled = pages.Count > 1;
    }

    internal void FocusNextCorrection()
    {
        CommitField();
        var form = _operation.Form;
        var diagnostics = form.InputDiagnostics.Concat(form.Validate().Diagnostics).ToArray();
        for (int offset = 0; offset < diagnostics.Length; offset++)
        {
            _correctionIndex = (_correctionIndex + 1) % diagnostics.Length;
            DefinitionDiagnostic diagnostic = diagnostics[_correctionIndex];
            DefinitionPath literal;
            if (diagnostic.Location is { } location)
            {
                if (!form.TryResolveCorrectionPath(diagnostic.FieldId, location, out literal)) continue;
            }
            else
            {
                if (diagnostic.FieldId.Length == 0) literal = DefinitionPath.FromSegments(diagnostic.RelativePath);
                else literal = DefinitionPath.FromSegments([
                    form.Fields.Single(input => input.Definition.Id == diagnostic.FieldId).Definition.Name, .. diagnostic.RelativePath]);
            }
            if (!form.FocusEditor(literal)) continue;
            var slots = VisibleSlots;
            int index = slots.ToList().FindIndex(slot => slot.Location.Equals(literal));
            if (index < 0 && literal.Segments.Count > 0)
            {
                index = slots.ToList().FindIndex(slot => slot.IsList &&
                    slot.Location.Segments.SequenceEqual(literal.Segments.SkipLast(1)));
                if (index >= 0 && int.TryParse(literal.Segments[^1], out int row)) _itemIndex = row;
            }
            if (index < 0) continue;
            _fieldIndex = index;
            _helpPage = 0;
            ShowField(preserveLocation: false);
            StatusCode = diagnostic.Code;
            string guidance = PresentationStrings.FieldCorrection(form.GetFieldValidation(literal), form.Locale);
            _status.Text = guidance.Length > 0 ? guidance : PresentationStrings.Diagnostic(diagnostic.Code, form.Locale);
            FocusInput();
            return;
        }
        Status("DraftInvalid");
    }

}
