namespace Configurator.Terminal;

using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.Text;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

internal sealed partial class TerminalWindow
{
    private TerminalTransferReviewDialog? _transferDialog;

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

    internal void MoveField(int delta) => MoveField(delta, focusInput: true);

    private void MoveField(int delta, bool focusInput)
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
        ShowField(preserveLocation: false);
        if (focusInput) FocusInput();
    }

    private void MoveToSlotBoundary(bool last)
    {
        CommitField();
        var slots = VisibleSlots;
        if (slots.Length == 0) { ShowField(); return; }
        _fieldIndex = last ? slots.Length - 1 : 0;
        _itemIndex = 0;
        ShowField(preserveLocation: false);
    }

    private void MoveItem(int delta)
    {
        if (_slot is null || !_slot.IsList) return;
        CommitField();
        int requested = _itemIndex + delta;
        _itemIndex = Math.Clamp(requested, 0, TerminalFieldEditor.LastItemPosition(_slot));
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

    private void ShowFieldLabel(int count)
    {
        if (_slot is null)
        {
            _fieldLabel.Text = LocalizedText("NoFields");
            return;
        }
        DefinitionEditorPresentation? presentation = _slot.Presentation;
        string? rowPrefix = ResolvePublicRowPrefix(_slot);
        string? wireSuffix = presentation is { HasCopyableWireToken: true, WireToken.Length: > 0 } &&
            !string.Equals(presentation.WireToken, _slot.Title, StringComparison.Ordinal)
            ? $" [{presentation.WireToken}]"
            : null;
        string valueSummary = !_slot.IsContainer && !_slot.RequiresMasking &&
            TerminalFieldEditor.ChoicePresentations(_slot).Count > 0 &&
            TerminalFieldEditor.FormatDisplayValue(_slot, _itemIndex) is { Length: > 0 } formatted &&
            !string.Equals(formatted, _value.Text, StringComparison.Ordinal)
            ? $" = {formatted}"
            : string.Empty;
        _fieldLabel.Text = Safe($"{LocalizedNumber(_fieldIndex + 1)}/{LocalizedNumber(count)} {rowPrefix}{_slot.Title} ({_slot.ScalarType}){wireSuffix}{valueSummary}"
            + (_slot.IsList ? $" [{LocalizedNumber(_itemIndex + 1)}/{LocalizedNumber(TerminalFieldEditor.ItemCount(_slot) + 1)}]" : "")
            + (_operation.Form.GetEditorState(_slot.Location)?.IsRequired == true ? " | " + LocalizedText("Required") : "")
            + (presentation?.IsDeprecated == true ? " | " + LocalizedText("DeprecatedBadge") : "")
            + (TerminalFieldEditor.Value(_slot) is { ValueKind: JsonValueKind.Null } ? " | " + LocalizedText("NullValue")
                : _operation.Form.AnswerOrigin(_slot.Location) switch
                {
                    DefinitionAnswerOrigin.Omitted => " | " + LocalizedText("Omitted"),
                    DefinitionAnswerOrigin.Generated => " | " + LocalizedText("GeneratedValue"),
                    _ => ""
                }));
    }

    private string? ResolvePublicRowPrefix(TerminalFieldEditor.Slot slot)
    {
        if (slot.RequiresMasking || slot.Location.Segments.Count < 3) return null;
        DefinitionPath parentPath = DefinitionPath.FromSegments(slot.Location.Segments.Take(slot.Location.Segments.Count - 1));
        if (_operation.Form.GetFieldValidation(parentPath).HasProtectedDescendants) return null;
        TerminalFieldEditor.Slot? parentSlot = VisibleSlots.FirstOrDefault(candidate => candidate.Location.Equals(parentPath));
        return parentSlot is { PreferFallbackTitle: true, Title.Length: > 0 } &&
            !string.Equals(parentSlot.Title, slot.Title, StringComparison.Ordinal)
            ? parentSlot.Title + " > "
            : null;
    }

    private void SetEditorVisibility()
    {
        bool guided = !_tooSmall && _stage == TerminalWorkbenchStage.Configure;
        bool review = !_tooSmall && _stage == TerminalWorkbenchStage.ReviewAndImport;
        bool files = !_tooSmall && _stage == TerminalWorkbenchStage.FilesAndSave;

        _sectionNavigator.Visible = !_tooSmall && _sidebarVisible;
        if (_sectionsButton is not null) _sectionsButton.Visible = !_tooSmall && !_sidebarVisible;

        bool scalar = guided && _slot is { IsObject: false, IsObjectList: false }
            && (!_slot.IsList || TerminalFieldEditor.MaximumItems(_slot) > 0);
        _experience.Visible = guided;
        _optionalEditors.Visible = guided;
        _locale.Visible = guided;
        _correction.Visible = guided;
        _fieldLabel.Visible = guided;
        _helpPane.Visible = guided;
        _help.Visible = guided;
        _value.Visible = scalar && _slot is { RequiresMasking: false };
        _masked.Visible = (scalar && _slot is { RequiresMasking: true }) || (guided && _slot is { IsMap: true });
        _previousField.Visible = guided;
        _nextField.Visible = guided;
        _applyField.Visible = guided;

        _addItem.Visible = guided && (_slot is { IsList: true } or { IsMap: true });
        _addItem.Text = LocalizedText(_slot is { IsMap: true } ? "AddMapEntry" : "AddItem");
        _supplyContainer.Visible = guided && _slot is { IsContainer: true } && _slot.Schema.Variants.Count == 0;
        _clearContainer.Visible = guided && _slot is not null && _slot.Location.Segments.Count > 0;
        _clearContainer.Text = LocalizedText("OmitValue");

        bool hasVariants = _slot is not null && _slot.Schema.Variants.Count > 0;
        int choiceCount = _slot is null ? 0 : TerminalFieldEditor.ChoicePresentations(_slot).Count;
        bool hasAnyChoices = hasVariants || choiceCount > 0 || (_slot is not null && TerminalFieldEditor.Choices(_slot).Count > 0);
        _choice.Visible = guided && hasAnyChoices;
        _choice.Text = LocalizedText(hasVariants ? "ChooseVariant" : "Choose");
        _choiceList.Visible = guided && !hasVariants && choiceCount > 0;

        DefinitionFieldValidation? facts = _slot is null ? null : _operation.Form.GetFieldValidation(_slot.Location);
        _value.ReadOnly = _masked.ReadOnly = facts?.CanEdit != true;
        _addItem.Enabled = _supplyContainer.Enabled = _choice.Enabled = _choiceList.Enabled = facts?.CanEdit == true;
        _clearContainer.Enabled = facts?.CanOmit == true;
        _null.Visible = guided && facts?.CanSetNull == true;
        _reset.Visible = guided && facts?.CanReset == true;
        _suggest.Visible = guided && facts?.CanApplySuggestion == true;
        _reuseValue.Visible = guided && _slot?.Schema.PresentationMetadata.SuggestedSource is not null;
        for (int index = 0; index < _itemActions.Length; index++)
            _itemActions[index].Visible = guided && _slot is { IsList: true, IsObjectList: false } &&
                TerminalFieldEditor.HasItem(_slot, _itemIndex) &&
                (index == 0 ? _operation.Form.GetFieldValidation(TerminalFieldEditor.EditLocation(_slot, _itemIndex)).CanSetNull
                    : _operation.Form.GetFieldValidation(TerminalFieldEditor.EditLocation(_slot, _itemIndex)).CanApplySuggestion);
        foreach (var button in _listButtons) button.Visible = guided && _slot is { IsList: true };
        bool secret = scalar && _slot is { RequiresMasking: true };
        _generateSecret.Visible = secret;
        _secretOptions.Visible = secret && _slot is not null && HasConfigurableSecretOptions(_slot);
        _revealSecret.Visible = secret;
        _copySecret.Visible = secret;
        _revealSecret.Text = LocalizedText(_masked.IsRevealed ? "HideSecret" : "ShowSecret");

        _reviewSummaryLabel.Visible = review;
        _reviewJumpButton.Visible = review;
        _reviewFindButton.Visible = review;
        _artifactButton.Visible = review;
        _selectOutputButton.Visible = review;
        _artifactLabel.Visible = review;
        _importFileLabel.Visible = review;
        _importPath.Visible = review;
        _import.Visible = review;
        _fillImport.Visible = review;
        _nextFinding.Visible = review;
        _comparisonLabel.Visible = review;

        _filesSummaryLabel.Visible = files;
        _validateButton.Visible = files;
        _prepare.Visible = files;
        _save.Visible = files;
        _clearPrivateButton.Visible = files;
        _nextOutcome.Visible = files;
        _nextAttempt.Visible = files;
        _receiptLabel.Visible = files;
        _nextActions.Visible = files && _operation.Form.OutputWorkflow.ProposedNextActions.Count > 0;

        LayoutCurrentStage();
    }

    private void FocusInput()
    {
        if (_stage != TerminalWorkbenchStage.Configure) return;
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
            if (Volatile.Read(ref window._closeRequested) == 0)
            {
                window._sectionNavigator.Refresh();
                window.ShowField();
            }
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
        if (action == "ApplySuggestion" && _slot.Schema.PresentationMetadata.SuggestedSource is not null)
        {
            if (CommitField()) ReviewTransfer(_operation.Form.ReviewSuggestion(path));
            return;
        }
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
        form.SelectLocale(locales[(current + 1) % locales.Count]);
    }

    private void ShowHelp()
    {
        string text = _slot is null ? "" : _slot.Help;
        if (_slot is not null)
        {
            DefinitionPath path = _slot.IsList && TerminalFieldEditor.HasItem(_slot, _itemIndex)
                ? TerminalFieldEditor.EditLocation(_slot, _itemIndex) : _slot.Location;
            string correction = PresentationStrings.FieldCorrection(_operation.Form.GetFieldValidation(path), _operation.Form.Locale);
            if (correction.Length > 0) text = text.Length > 0 ? text + "\n" + correction : correction;
        }
        if (_slot is not null && _slot.Schema.Variants.Count > 0)
        {
            int selected = _slot.Schema.Variants.ToList().FindIndex(variant => ReferenceEquals(variant, _slot.ObjectSchema));
            if (selected >= 0)
                text += $"\n{LocalizedNumber(selected + 1)}/{LocalizedNumber(_slot.Schema.Variants.Count)} " +
                    _operation.Form.GetVariantTitle(_slot.Location, selected);
        }
        if (_slot is { IsMap: true }) text = LocalizedText("MapKeyHelp") + (text.Length > 0 ? "\n" + text : "");
        if (_slot is { IsList: true } list && TerminalFieldEditor.HasItem(list, _itemIndex))
        {
            string itemHelp = TerminalStrings.FormatHelp(
                _operation.Form.GetEditorPresentation(TerminalFieldEditor.EditLocation(list, _itemIndex)), list.Schema.Items!.Keywords, _operation.Form.Locale);
            if (itemHelp.Length > 0) text += (text.Length > 0 ? "\n" : "") + itemHelp;
        }
        if (_slot is { RequiresMasking: true, IsObject: false, IsObjectList: false })
        {
            DefinitionSecretValidation rules = _operation.Form.GetSecretValidation(
                TerminalFieldEditor.EditLocation(_slot, _itemIndex));
            string secretText = string.Join("\n", rules.Requirements.Select(rule => rule.Message)
                .Concat(rules.Failures.Select(rule => LocalizedText("SecretRuleFailed") + " " + rule.Message)));
            if (secretText.Length > 0) text += (text.Length > 0 ? "\n" : "") + secretText;
        }

        string safeText = string.Join('\n', text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(Safe));
        int totalWidth = Viewport.Width > 0 ? Viewport.Width : _lastViewportSize.Width;
        int contentWidth = Math.Max(20, totalWidth - (_sidebarVisible ? 35 : 2));
        IReadOnlyList<string> wrapped = TerminalStrings.WrapLines(safeText, contentWidth);
        _help.Text = safeText;
        _helpPane.SetContentSize(new Size(contentWidth, Math.Max(1, wrapped.Count)));
        if (_helpPane.Viewport.Y > 0) _helpPane.ScrollVertical(-_helpPane.Viewport.Y);
    }

    internal void FocusNextCorrection()
    {
        if (JumpToNextActionable(errorsOnly: false)) return;
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
            if (FocusDiagnosticTarget(literal, diagnostic)) return;
        }
        Status("DraftInvalid");
    }

    private bool JumpToNextActionable(bool errorsOnly)
    {
        CommitField();
        var form = _operation.Form;
        string? preferredSection = errorsOnly
            ? form.Projection.Navigation.NextErrorSectionId
            : form.Projection.Navigation.NextRequiredSectionId;
        WorkspaceActionableTarget? target = form.ResolveNextActionableTarget(preferredSection, errorsOnly);
        if (target is null)
        {
            Status(errorsOnly ? "PreviewReady" : "DraftInvalid");
            return false;
        }
        if (!form.FocusEditor(target.DefinitionPath)) return false;
        return FocusDiagnosticTarget(target.DefinitionPath, target.Diagnostic);
    }

    private void JumpToNextRequired()
    {
        if (!JumpToNextActionable(errorsOnly: false) &&
            _operation.Form.Projection.Navigation.NextRequiredSectionId is { } sectionId)
        {
            JumpToSection(sectionId);
        }
    }

    private void JumpToNextUnvisited()
    {
        string? nextUnvisited = _operation.Form.Projection.Navigation.NextUnvisitedSectionId
            ?? _sectionNavigator.Sections.FirstOrDefault(s => s.Coverage == SectionReviewCoverage.NotVisited)?.SectionId;
        if (nextUnvisited is not null) JumpToSection(nextUnvisited);
    }

    private bool FocusDiagnosticTarget(DefinitionPath literal, DefinitionDiagnostic diagnostic)
    {
        var form = _operation.Form;
        SetStage(TerminalWorkbenchStage.Configure);
        var slots = VisibleSlots;
        int index = slots.ToList().FindIndex(slot => slot.Location.Equals(literal));
        if (index < 0 && literal.Segments.Count > 0)
        {
            index = slots.ToList().FindIndex(slot => slot.IsList &&
                slot.Location.Segments.SequenceEqual(literal.Segments.SkipLast(1)));
            if (index >= 0 && int.TryParse(literal.Segments[^1], out int row)) _itemIndex = row;
        }
        if (index < 0) return false;
        _fieldIndex = index;
        ShowField(preserveLocation: false);
        StatusCode = diagnostic.Code;
        string guidance = PresentationStrings.FieldCorrection(form.GetFieldValidation(literal), form.Locale);
        _status.Text = guidance.Length > 0 ? guidance : PresentationStrings.Diagnostic(diagnostic.Code, form.Locale);
        FocusInput();
        return true;
    }

    private bool ToggleCurrentSectionReviewed()
    {
        if (_stage != TerminalWorkbenchStage.Configure) return false;
        bool toggled = _sectionNavigator.ToggleSelectedReviewed();
        if (toggled) Status("SectionReviewedStatus");
        return toggled;
    }

    private void OpenOutputTasks()
    {
        if (_operation.IsPublishing || _preparing || !CommitField()
            || _operation.Form.Definition.Presentation?.OutputSelector is not { } selector) return;
        using var dialog = new TerminalOutputTasksDialog(_operation.Form, _application);
        _application.Run(dialog);
        if (dialog.TargetOption is { } target) ReviewTransfer(_operation.Form.ReviewOutputTransition(target));
    }

    private void ReviewInlineTransfer()
    {
        if (_operation.IsPublishing || _preparing || _slot is null || !CommitField()) return;
        ReviewTransfer(_operation.Form.ReviewSuggestion(TerminalFieldEditor.EditLocation(_slot, _itemIndex)));
    }

    private void ReviewTransfer(DefinitionTransitionTransferPlan review)
    {
        CancelImport();
        using var dialog = new TerminalTransferReviewDialog(_operation.Form, review, _application);
        _transferDialog = dialog;
        try { _application.Run(dialog); }
        finally { _transferDialog = null; }
        if (dialog.TransferResult?.Succeeded == true)
        {
            _fieldIndex = 0;
            _itemIndex = 0;
            ShowField(preserveLocation: false);
            FocusInput();
        }
        RefreshCommands();
    }
}

internal sealed class TerminalOutputTasksDialog : Dialog
{
    private const int TasksPerPage = 8;
    private readonly List<Button> _taskButtons = [];
    private readonly Button _cancel;
    private readonly Button _previousPage;
    private readonly Button _nextPage;
    private int _page;
    internal TerminalOutputTasksDialog(DefinitionFormModel form,
        global::Terminal.Gui.App.IApplication? application = null)
    {
        Title = TerminalStrings.Get("OutputTasks", form.Locale);
        Width = Dim.Percent(90);
        var options = form.Definition.Presentation!.OutputSelector!.Options;
        Height = Math.Min(TasksPerPage, options.Count) + (options.Count > TasksPerPage ? 6 : 5);
        int index = 0;
        foreach (var option in options)
        {
            string position = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var state = form.OutputWorkflow.Tasks.Single(task => task.Option == option.Value);
            string text = form.LocalizedText(DefinitionPath.FromSegments(
                ["presentation", "outputSelector", "options", position, "title"]), option.Title)!
                + " | " + PresentationStrings.OutputStatus(state, form.Locale);
            var button = new Button
            {
                X = 0,
                Y = index++,
                Width = Dim.Fill(),
                Height = 1,
                ShadowStyle = null,
                Text = new string(text.Select(character => char.IsControl(character) ? ' ' : character).ToArray()),
                HotKeySpecifier = (Rune)0xffff
            };
            _taskButtons.Add(button);
            button.Accepting += (_, args) =>
            {
                args.Handled = true;
                TargetOption = option.Value;
                application?.RequestStop(this);
            };
            Add(button);
        }
        _previousPage = new Button
        {
            X = 0,
            Y = TasksPerPage,
            Width = Dim.Fill(),
            Height = 1,
            ShadowStyle = null,
            Text = TerminalStrings.Get("PreviousTaskPage", form.Locale),
            Visible = options.Count > TasksPerPage
        };
        _previousPage.Accepting += (_, args) => { args.Handled = true; MovePage(-1); };
        _nextPage = new Button
        {
            X = 0,
            Y = TasksPerPage + 1,
            Width = Dim.Fill(),
            Height = 1,
            ShadowStyle = null,
            Text = TerminalStrings.Get("NextTaskPage", form.Locale),
            Visible = options.Count > TasksPerPage
        };
        _nextPage.Accepting += (_, args) => { args.Handled = true; MovePage(1); };
        Add(_previousPage, _nextPage);
        _cancel = new Button
        {
            X = 0,
            Y = Math.Min(TasksPerPage, options.Count) + (options.Count > TasksPerPage ? 2 : 0),
            Width = Dim.Fill(),
            Height = 1,
            ShadowStyle = null,
            Text = PresentationStrings.Get("TransferCancel", form.Locale)
        };
        _cancel.Accepting += (_, args) => { args.Handled = true; TargetOption = null; application?.RequestStop(this); };
        Add(_cancel);
        ShowPage();
    }

    internal string? TargetOption { get; private set; }

    private void MovePage(int delta)
    {
        _page = Math.Clamp(_page + delta, 0, (_taskButtons.Count - 1) / TasksPerPage);
        ShowPage();
        _taskButtons[_page * TasksPerPage].SetFocus();
    }

    private void ShowPage()
    {
        int first = _page * TasksPerPage;
        for (int index = 0; index < _taskButtons.Count; index++)
        {
            _taskButtons[index].Visible = index >= first && index < first + TasksPerPage;
            if (_taskButtons[index].Visible) _taskButtons[index].Y = index - first;
        }
        _previousPage.Enabled = _page > 0;
        _nextPage.Enabled = first + TasksPerPage < _taskButtons.Count;
    }
}

/// <summary>Retains only candidate identities and deliberate choices; previews are read when displayed.</summary>
internal sealed class TerminalTransferReviewDialog : Dialog
{
    private readonly DefinitionFormModel _form;
    private readonly DefinitionTransitionTransferPlan _review;
    private readonly global::Terminal.Gui.App.IApplication? _application;
    private readonly HashSet<int> _selected;
    private readonly Label _detail;
    private readonly Button _toggle;
    private readonly Button _apply;
    private readonly Button? _switchOnly;
    private readonly Button _cancel;
    private int _index;

    internal TerminalTransferReviewDialog(DefinitionFormModel form, DefinitionTransitionTransferPlan review,
        global::Terminal.Gui.App.IApplication? application = null)
    {
        _form = form;
        _review = review;
        _application = application;
        _selected = review.Candidates.Where(candidate => candidate.SelectedByDefault && !candidate.DestinationProvided)
            .Select(candidate => candidate.Id).ToHashSet();
        Title = ReviewText("TransferReviewTitle");
        Width = Dim.Percent(90);
        Height = 17;
        _detail = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 7,
            HotKeySpecifier = (Rune)0xffff
        };
        var next = new Button
        {
            X = 0,
            Y = 8,
            Height = 1,
            ShadowStyle = null,
            Text = TerminalStrings.Get("TransferNextCandidate", form.Locale)
        };
        next.Accepting += (_, args) => { args.Handled = true; MoveCandidate(); };
        _toggle = new Button
        {
            X = 0,
            Y = 9,
            Height = 1,
            ShadowStyle = null,
            Text = TerminalStrings.Get("TransferSelect", form.Locale)
        };
        _toggle.Accepting += (_, args) => { args.Handled = true; ToggleCandidate(); };
        _apply = new Button
        {
            X = 0,
            Y = 10,
            Width = Dim.Fill(),
            Height = 1,
            ShadowStyle = null,
            Text = ReviewText(review.TargetOption is null ? "TransferApply" : "TransferApplyAndSwitch"),
            Enabled = review.TargetOption is not null || review.Candidates.Count > 0
        };
        _apply.Accepting += (_, args) => { args.Handled = true; Apply(false); };
        Add(_detail, next, _toggle);
        Add(_apply);
        if (review.TargetOption is not null)
        {
            _switchOnly = new Button
            {
                X = 0,
                Y = 11,
                Width = Dim.Fill(),
                Height = 1,
                ShadowStyle = null,
                Text = ReviewText("TransferSwitchWithoutCopy")
            };
            _switchOnly.Accepting += (_, args) => { args.Handled = true; Apply(true); };
            Add(_switchOnly);
        }
        _cancel = new Button
        {
            X = 0,
            Y = review.TargetOption is null ? 11 : 12,
            Width = Dim.Fill(),
            Height = 1,
            ShadowStyle = null,
            Text = ReviewText("TransferCancel")
        };
        _cancel.Accepting += (_, args) => { args.Handled = true; ClearPreview(); application?.RequestStop(this); };
        Add(_cancel);
        form.InputChanged += InputChanged;
        ShowCandidate();
    }

    internal DefinitionTransferResult? TransferResult { get; private set; }
    private string ReviewText(string key) => PresentationStrings.Get(key, _form.Locale);

    internal void MoveCandidate()
    {
        if (_review.Candidates.Count == 0) return;
        _index = (_index + 1) % _review.Candidates.Count;
        ShowCandidate();
    }

    internal void ToggleCandidate()
    {
        if (_review.Candidates.Count == 0) return;
        int id = _review.Candidates[_index].Id;
        if (!_selected.Add(id)) _selected.Remove(id);
        ShowCandidate();
    }

    internal void Apply(bool switchWithoutCopy)
    {
        TransferResult = _form.ApplyTransfer(_review, switchWithoutCopy ? [] : _selected);
        ClearPreview();
        if (TransferResult.Succeeded) _application?.RequestStop(this);
        else _detail.Text = ReviewText("TransferStale");
    }

    private void ShowCandidate()
    {
        _toggle.Enabled = _review.Candidates.Count > 0;
        if (_review.Candidates.Count == 0) { _detail.Text = ReviewText("TransferUnavailable"); return; }
        var candidate = _review.Candidates[_index];
        string preview = candidate.RequiresMasking ? ReviewText("TransferMasked")
            : _form.ReadTransferPreview(_review, candidate.Id)?.ToString() ?? ReviewText("TransferUnavailable");
        _detail.Text = new string(($"{_index + 1}/{_review.Candidates.Count} "
            + $"{candidate.SourceTitle} -> {candidate.TargetTitle}\n{preview}\n"
            + (candidate.DestinationProvided ? ReviewText("TransferOverwrite") + "\n" : string.Empty)
            + TerminalStrings.Get(_selected.Contains(candidate.Id) ? "Selected" : "NotSelected", _form.Locale))
            .Select(character => char.IsControl(character) && character != '\n' ? ' ' : character).ToArray());
    }

    private void ClearPreview() => _detail.Text = string.Empty;
    internal void Retire()
    {
        ClearPreview();
        _application?.RequestStop(this);
    }
    private void InputChanged(object? sender, EventArgs args)
    {
        Retire();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _form.InputChanged -= InputChanged; ClearPreview(); _selected.Clear(); }
        base.Dispose(disposing);
    }
}
