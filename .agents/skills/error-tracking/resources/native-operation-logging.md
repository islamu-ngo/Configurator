> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Minimal native operation logging behavior rules. -->
<!-- Centralizes request logging and error capture. -->

# Native Operation Logging Behavior (Lean)

## Rules
- Log start/end for each request with elapsed time.
- Log exceptions with request type and traceId.
- Keep request/response payloads out of logs unless sanitized.

## Related
- [loki-logging.md](loki-logging.md)
