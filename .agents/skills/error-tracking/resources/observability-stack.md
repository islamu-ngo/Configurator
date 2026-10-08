> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Observability stack summary (OpenTelemetry + Prometheus + Loki). -->
<!-- Notes project-specific rules and integration constraints. -->

# Observability Stack (Lean)

## Stack
- OpenTelemetry traces + metrics
- Prometheus scraping `/metrics`
- Loki for structured logs (Grafana for dashboards)

## Project Rules
- This project does **not** use Sentry.
- Keep `UseSerilog(..., writeToProviders: true)` enabled.
- Configure OTLP once via `UseOtlpExporter()`; do not mix exporters.

## Related
- [loki-logging.md](loki-logging.md)
- [prometheus-metrics.md](prometheus-metrics.md)
