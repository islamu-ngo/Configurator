> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- BEM naming rules for isolated component styles. -->
<!-- Keeps naming strict and uses MudBlazor theme vars. -->

# BEM with CSS Isolation

Use BEM naming in `.razor.css` files to keep component styling explicit and maintainable.

## Rules
- Block: `.block`
- Element: `.block__element`
- Modifier: `.block--modifier`
- One block namespace per component.
- Use MudBlazor theme variables, not hardcoded colors.
