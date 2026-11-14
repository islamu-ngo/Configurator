namespace Configurator.Browser.Components.Configuration;

using Configurator.Core.Definitions;
using Configurator.Presentation;

public partial class ConfigurationEditor
{
    private DefinitionOutputExportContext? _outputExportContext;
    private DefinitionTransitionTransferPlan? _transferReview;
    private ConfigurationNextActionsPanel? _nextActionsPanel;

    private async Task<bool> ReviewTransitionAsync(string targetOption)
    {
        DefinitionFormModel? owner = _form;
        if (owner is null || !_ready || !await ResolvePendingAsync()
            || _disposed || !ReferenceEquals(owner, _form)) return false;
        RetireDeveloper();
        RetireView();
        DefinitionTransitionTransferPlan review = owner.ReviewOutputTransition(targetOption);
        if (review.Candidates.Count == 0)
        {
            owner.SelectOutput(targetOption);
            _selectedSection = null;
            SelectWorkflowStage(WorkflowStage.Configure);
            RequestFocus(FocusTarget.Configure);
            return true;
        }
        ++_outputChoiceKey;
        _transferReview = review;
        RequestFocus(FocusTarget.TransferReview);
        return true;
    }

    private async Task ReviewInlineSuggestionAsync(DefinitionPath target)
    {
        DefinitionFormModel? owner = _form;
        if (owner is null || !_ready || !await ResolvePendingAsync()
            || _disposed || !ReferenceEquals(owner, _form)) return;
        RetireDeveloper();
        RetireView();
        _transferReview = owner.ReviewSuggestion(target);
        RequestFocus(FocusTarget.TransferReview);
    }

    private Task ApplyReviewedTransferAsync(ConfigurationNextActionsPanel.TransferSelection selection)
    {
        DefinitionFormModel? owner = _form;
        if (_disposed || owner is null || !ReferenceEquals(selection.Review, _transferReview)) return Task.CompletedTask;
        DefinitionTransferResult result = owner.ApplyTransfer(selection.Review, selection.SelectedIds);
        if (_disposed || !ReferenceEquals(owner, _form)) return Task.CompletedTask;
        if (result.Succeeded)
        {
            // The accepted revision remains authoritative even if a notification observer failed.
            if (owner.InputRevision != result.InputRevision) return Task.CompletedTask;
            if (result.ObserverFailed) InputChanged(owner, EventArgs.Empty);
            _transferReview = null;
            _selectedSection = null;
            SelectWorkflowStage(WorkflowStage.Configure);
            RequestFocus(FocusTarget.Configure);
            SetStatus(selection.SelectedIds.Count == 0 ? "AnswersChangedStatus" : "TransferAppliedStatus");
        }
        else
        {
            RetireView();
            SetStatus("TransferRejectedStatus");
            RequestFocus(CurrentStageFocus);
        }
        _statusCode = result.Code;
        return Task.CompletedTask;
    }

    private void CancelTransferReview(DefinitionTransitionTransferPlan review)
    {
        if (!ReferenceEquals(review, _transferReview)) return;
        RetireView();
        RequestFocus(CurrentStageFocus);
    }

    private FocusTarget CurrentStageFocus => _activeWorkflowStage switch
    {
        WorkflowStage.Prepare => FocusTarget.Files,
        WorkflowStage.Review => FocusTarget.Review,
        _ => FocusTarget.Configure
    };
}
