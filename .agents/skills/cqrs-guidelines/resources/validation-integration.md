> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Validation rules for CQRS handlers in this project. -->
<!-- Keeps manual validator instantiation as the core constraint. -->

# Validation Integration (Lean)

## Rules
- Validators are instantiated manually inside handlers.
- Validators can require repositories (pass via constructor).
- Validation failures populate `BaseCommandResponse.Errors`.
- Queries skip validation unless explicitly required.

## Related
- [command-patterns.md](command-patterns.md)
