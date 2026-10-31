---
name: error-tracking
description: "Load for Configurator exception handling or value-safe native diagnostics; backend Loki, Prometheus, OTEL and HTTP examples are reference-only, not a telemetry requirement."
type: guardrail
enforcement: suggest
priority: high
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


<!-- Error handling + observability rules for .NET services. -->
<!-- Read referenced resources before applying. -->

# Error Tracking & Observability Guidelines

> **Project-Agnostic Observability Patterns for .NET**
>
> Placeholders use `{Placeholder}` syntax - `docs/internal/TEMPLATE_GLOSSARY.md` is an Event source reference, historical only.

## Configurator Rules

- Keep diagnostics bounded and value-safe; do not emit answers, artifacts,
  credentials, raw exceptions or private paths.
- Preserve cancellation and explicit failure outcomes at the actual owner.
  Use the CLI/output contract or existing client status mechanism, not invented
  HTTP middleware or ProblemDetails.
- Browser has no telemetry or answer-bearing requests. Do not add an
  observability service to satisfy a transferred backend example.
- Load a linked resource only for a matching unresolved diagnostic decision;
  backend HTTP/metrics/log-stack resources below are historical Event reference.

## Historical Backend Rules (Reference Only)
- **Do not swallow exceptions.**
- Use RFC 7807 ProblemDetails for API errors.
- **Sentry is not used** — use OTEL + Prometheus + Loki.
- **Chained IExceptionHandler** (not middleware): `ValidationExceptionHandler` (handles validation exceptions → 400) → `GlobalExceptionHandler` (catch-all → 500 in prod, full detail in dev). Each returns `true` to stop the chain once handled.
- **.NET 10 handled-exception diagnostics**: `app.UseExceptionHandler()` currently runs without a `SuppressDiagnosticsCallback` override. In .NET 10, handled exceptions may suppress logs/metrics/events by default once an `IExceptionHandler` returns `true`. Treat this as an explicit observability decision when editing exception handling.
- **RFC 7807 extensions**: All ProblemDetails include `traceId` (`HttpContext.TraceIdentifier`), `timestamp` (ISO 8601), and `correlationId` when available. 500 errors hide `detail` in production.
- **CorrelationIdMiddleware**: Reads `X-Correlation-ID` or `X-Request-ID` from inbound requests, generates GUID if absent, pushes to `Serilog.LogContext`. Added to response headers.
- **RequestLoggingMiddleware**: Structured logging with method, path, status, duration, userId, tenantId, correlationId.
- **BusinessMetrics** (OpenTelemetry): Meter `"Explore.Business"` with counters: `events.created`, `events.published`, `registrations.created`, `organizations.created`, `authorization.decisions`. All tagged with `tenant_id`, `resource_type`.
- **PerformanceBehavior** (Native operation pipeline): Warning for >500ms, error for >3000ms. Includes handler name and elapsed time.
- **Rate limiting 429**: Rejection response is RFC 6585 ProblemDetails with `Retry-After` when available plus `X-RateLimit-Limit` and `X-RateLimit-Remaining`.

## Historical And Reusable Resources (Select Only The Relevant Topic)
- [api-exception-handling.md](resources/api-exception-handling.md)
- [api-error-responses.md](resources/api-error-responses.md)
- [native-operation-logging.md](resources/native-operation-logging.md)
- [prometheus-metrics.md](resources/prometheus-metrics.md)
- [loki-logging.md](resources/loki-logging.md)
- [blazor-error-boundary.md](resources/blazor-error-boundary.md)
- [opentelemetry-tracing.md](resources/opentelemetry-tracing.md)
- [observability-stack.md](resources/observability-stack.md)

## Related Documentation
- `docs/internal/CONFIGURATION.md` (Event source, historical only).
- `docs/internal/API.md` (Event source, historical only) — middleware pipeline, exception handling chain, rate limiting.
- [Configurator operations](../../../docs/internal/OPERATIONS.md) — target verification; Event business metrics, correlation ID, and security headers remain historical only.
