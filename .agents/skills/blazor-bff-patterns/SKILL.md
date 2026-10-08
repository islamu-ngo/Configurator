---
name: blazor-bff-patterns
description: "Reference-only source-system guidance; do not activate for Configurator offline engine, clients or repository tooling."
type: reference
enforcement: inform
priority: high
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


<!-- Blazor BFF rules for YARP, token forwarding, and auth state. -->
<!-- Read referenced resources before applying. -->

# Blazor BFF (Backend for Frontend) Patterns

> **Project-Agnostic BFF Patterns for Blazor Web Apps (InteractiveAuto)**
>
> Placeholders use `{Placeholder}` syntax - see [docs/internal/TEMPLATE_GLOSSARY.md](../../../docs/internal/extraction/FOUNDATION.md).

## Non‑Inferable Rules (Must Follow)
- **BFF boundary**: Browser never sees tokens; BFF uses HttpOnly cookies.
- **YARP**: API calls go through reverse proxy; attach Bearer token server-side.
- **Token forwarding**: Use YARP transforms or delegating handlers (no raw HttpClient in WASM).
- **YARP header transforms**: `X-Setup-Secret` is stripped from inbound requests and replaced by the BFF from secure config. `X-Tenant-Slug` is forwarded when the BFF has an authoritative tenant hint.
- **Service layer**: Wrap NSwag clients for error handling + safe defaults.
- **CSRF**: Enforce antiforgery on state‑changing routes.
- **InteractiveAuto**: Avoid direct HttpContext assumptions in components.
- **CORS**: API CORS policy names are environment-specific to this repo (`InternalAppPolicy`, `ExternalAppPolicy`, `InternalWebsitePolicy`, `ExternalWebsitePolicy`, `DevPolicy`). Do not copy placeholder policy names from generic BFF examples into this codebase.
- **Tenant resolution**: The API resolves tenant identity from trusted `X-Tenant-Slug` first, then normalized `Request.Host.Host` after forwarded-header processing. API-key flows are split: pre-auth middleware may record a requested tenant hint, and post-auth middleware finalizes binding or fails closed.

## Resources (Read Before Applying)
- [bff-configuration.md](resources/bff-configuration.md)
- [token-forwarding.md](resources/token-forwarding.md)
- [auth-state-management.md](resources/auth-state-management.md)
- [service-layer-patterns.md](resources/service-layer-patterns.md)
- [interactiveauto-yarp-security.md](resources/interactiveauto-yarp-security.md)

## Related Documentation
- [`docs/internal/ARCHITECTURE.md`](../../../docs/internal/extraction/FOUNDATION.md)
- [`docs/internal/BLAZOR.md`](../../../docs/internal/extraction/FOUNDATION.md)
- [`auth-patterns`](../auth-patterns/SKILL.md)
