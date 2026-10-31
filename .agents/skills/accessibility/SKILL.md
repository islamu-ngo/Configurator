---
name: accessibility
description: "Load for Blazor/MudBlazor UI changes involving forms, dialogs, focus, keyboard navigation, landmarks, ARIA, color contrast, RTL-safe styling, or WCAG 2.2 AA tests; not for backend-only changes."
type: guardrail
enforcement: block
priority: high
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- ABOUTME: Accessibility guardrail for project Blazor and MudBlazor UI changes. -->
<!-- ABOUTME: Routes agents to local WCAG 2.2 AA rules, implementation patterns, and verification evidence. -->

## Resources
- Accessibility Standards: `docs/internal/ACCESSIBILITY.md` (Event source, historical only; use the transferred resources below for reusable guidance).
- [Accessibility Resources](resources/index.md) - select the affected interaction or verification topic.
- [Blazor UI Conventions](../blazor-ui-conventions/SKILL.md) - when component communication or hosting changes.

## Top 5 Invariants
1. Prefer native semantic HTML and MudBlazor's native control output; add ARIA only when semantics are missing, and keep each control's accessible name, role, state, value, and visible-label text accurate.
2. Every routable page has a descriptive `PageTitle`, exactly one project-required `h1`, sequential headings, and logical DOM order while relying on `MainLayout` for skip navigation, landmarks, live regions, direction, and navigation focus.
3. Every action is keyboard operable with its native or WAI-ARIA pattern, uses no positive `tabindex`, keeps focus visible and unobscured, and restores focus after dialogs or drawers through the existing target focus mechanism. `IAccessibilityFocusService` below is an Event example, not a required Configurator service.
4. Every form control has a programmatic label and connected help/error text, while dynamic status is announced once with polite or assertive urgency only when focus does not already convey the change.
5. Images, color, contrast, motion, pointer targets, dragging alternatives, reflow, zoom, forced colors, and RTL logical properties use the transferred WCAG guidance and receive risk-proportionate rendered and manual verification; the absent Event accessibility document is not a target gate.

## Top 5 Anti-Patterns
1. **Simulated native control:** A clickable `div`, `span`, or card recreates button or link semantics and leaves keyboard behavior incomplete.
2. **ARIA guesswork:** Redundant, unsupported, or stale roles and `aria-*` values misrepresent the rendered interface to assistive technology.
3. **Component-name trust:** Assuming MudBlazor is accessible without inspecting its rendered DOM misses missing names, descriptions, states, or version-specific behavior.
4. **Focus and announcement noise:** Moving focus and announcing the same update, or using assertive live regions for routine status, creates duplicate or disruptive output.
5. **Automation-as-certification:** Treating architecture tests, bUnit assertions, or an automated scanner as WCAG proof leaves keyboard, zoom, visual, and assistive-technology failures undiscovered.

## Minimal Examples

Historical Event labels and focus-service usage illustrate the interaction
mechanism; use Configurator's actual controls and services.
```razor
<PageTitle>Event details</PageTitle>
<MudText Typo="Typo.h4" HtmlTag="h1">Event details</MudText>

<MudIconButton Icon="@Icons.Material.Filled.Delete"
               aria-label="Delete event"
               OnClick="DeleteAsync" />
```

```csharp
await Focus.SaveFocusAsync();
try
{
    var options = new DialogOptions { CloseOnEscapeKey = true };
    var dialog = await Dialogs.ShowAsync<DeleteDialog>("Delete event", options);
    await dialog.Result;
}
finally
{
    await Focus.RestoreFocusAsync("#delete-event-trigger");
}
```

## Verification Hooks
- Select affected Browser/Presentation component checks from [PROJECTS.md](../../../PROJECTS.md). No product suites for prose-only skill edits.
- `git diff --check -- .agents/skills/accessibility`
- Manual: complete the risk-based keyboard, zoom/reflow, theme, RTL, motion, browser accessibility-tree, and screen-reader checks in [verification.md](resources/verification.md).

## Related Skills
- [Blazor UI Conventions](../blazor-ui-conventions/SKILL.md)
- [Blazor CSS Isolation](../blazor-css-isolation/SKILL.md)
- [Design System](../design-system/SKILL.md)
- [Agentic Research](../agentic-research/SKILL.md)
