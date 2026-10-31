> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Fix patterns for common Clean Architecture violations. -->
<!-- Use this as a short checklist during refactors. -->

# Fix Patterns

Historical backend recipes only. Resolve the target owner in `PROJECTS.md`
before applying a dependency inversion; do not introduce absent layers or
single-use abstractions merely to match the following source-system examples.

## Core Fixes
- Move business rules from controllers → Application/Domain.
- Replace direct DbContext access in Application → repository interface.
- Move external service usage → Infrastructure via Application interface.
- Keep DTOs in Application; API/Blazor consume them.

## Quick Decision Guide
1. Business rule? → **Domain**
2. Use case/workflow? → **Application**
3. Database access? → **Persistence** (via interface)
4. External service? → **Infrastructure** (via interface)
5. HTTP/UI specific? → **API/Blazor**

**Related**: `dependency-rules.md`, `layer-responsibilities.md`.
