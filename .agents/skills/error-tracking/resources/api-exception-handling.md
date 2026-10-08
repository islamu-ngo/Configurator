> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Centralized API exception handling rules for this project. -->
<!-- Ensures ProblemDetails responses and safe logging. -->

# API Exception Handling (Lean)

## Rules
- Use centralized exception handler (`UseExceptionHandler`).
- Return RFC 7807 `ProblemDetails` for unhandled errors.
- Log exceptions with trace/correlation IDs.
- Do not expose raw stack traces in production.

## Related
- [api-error-responses.md](api-error-responses.md)
