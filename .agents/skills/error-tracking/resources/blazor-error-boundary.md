> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Minimal guidance for Blazor ErrorBoundary usage. -->
<!-- Focuses on logging and user-safe fallback UI. -->

# Blazor ErrorBoundary (Lean)

## Rules
- Wrap error-prone UI blocks with `ErrorBoundary`.
- Log exceptions via `ILogger` (structured logging).
- Show user-safe fallback UI; avoid raw exception details.
- Rethrow caught exceptions so `ErrorBoundary` can handle them.

## Related
- [api-exception-handling.md](api-exception-handling.md)
