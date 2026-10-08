> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Minimal BFF + YARP configuration rules. -->
<!-- Keep tokens server-side; proxy via YARP. -->

# BFF Configuration (YARP)

## Required Rules
- Proxy `/api/*` through YARP from Blazor BFF → API.
- Attach Bearer token server-side from secure cookie/session.
- Forward tenant/setup headers when present.

## Auth Endpoints
- `/auth/challenge` → login
- `/auth/signout` → logout
- Optional `/bff/me` for user claims exposure (minimal).

## NSwag Client Wiring
- **Server**: direct API base URL + token forwarding handler.
- **WASM**: call BFF base address (cookies + 401 handler).

**Related**: `token-forwarding.md`, `auth-state-management.md`, `interactiveauto-yarp-security.md`.
