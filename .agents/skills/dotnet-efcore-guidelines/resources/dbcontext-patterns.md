> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- DbContext setup rules for EF Core in this project. -->
<!-- Focuses on configuration discovery and scoped lifetime. -->

# DbContext Patterns (Lean)

## Rules
- Use `ApplyConfigurationsFromAssembly` in `OnModelCreating`.
- Keep DbContext lifetime scoped (per request).
- Avoid DbContext usage in Application layer; use repositories.

## Related
- [entity-configuration.md](entity-configuration.md)
