---
name: blazor-server
description: Historical/reference-only Event blazor-server guidance; never binds Configurator product paths.
paths:
  - "reference-only/blazor-server/**"
related_skills: []
related_docs: [AGENTS.md, PROJECTS.md, docs/internal/GOVERNANCE.md, docs/internal/QUICK_REFERENCE.md]
minimum_tests: []
related_intents: []
---

# Historical Event Rule: blazor-server

Twin copies at `.agents/rules/blazor-server.md` and `.omo/rules/blazor-server.md` are byte-identical.

## Reference-Only Scope

The reserved `reference-only/blazor-server/**` glob matches no Configurator path.
The original body below is retained verbatim from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` for historical engineering reference.
Its original paths, docs, intents, commands, libraries and mandatory language
are source-system facts, not Configurator instructions or active links. Do not
introduce Event HAL, EF, BFF, payment, identity or scheduling requirements.
Target authority remains [AGENTS.md](../../AGENTS.md), [PROJECTS.md](../../PROJECTS.md)
and [Governance](../../docs/internal/GOVERNANCE.md).

## Original Rule Body (Inactive)

```markdown

<!-- ABOUTME: Path-scoped rules for Explore.Blazor server-side BFF, auth, proxy, and SSR code. -->
<!-- ABOUTME: Twin copy at .omo/rules/blazor-server.md. When modifying this file, update both paths. -->

# Blazor Server / BFF Rules

## Applies To
- `src/Explore.Blazor/**/*.cs`, `src/Explore.Blazor/**/*.razor`

## Path-Specific Constraints
- **BFF Boundary**: Tokens must stay server-side. Use YARP to proxy `/api/*` requests. Never expose raw bearer tokens to the client.
- **Concern Separation**: Use dedicated forwarding handlers/transforms for tokens, tenants, and setup-secrets.
- **Pooled Clients**: Keep `UseCookies = false` on outbound server-side API clients.
- **SSR Safety**: Avoid component logic that assumes `HttpContext` presence (crucial for InteractiveAuto components).
- **Endpoint Modularization**: Organize auth, setup, preference, storage, support-access, and ATProto endpoints into dedicated extension files.
- **Dynamic Schemes & OAuth**: Manage OIDC/OAuth schemes via `IDynamicAuthSchemeManager`. Expose AT Protocol client metadata (`/oauth/client-metadata.json`) and JWKS (`/oauth/jwks.json`) via `AtprotoOAuthEndpointExtensions`.

## Must Read
- [docs/internal/QUICK_REFERENCE.md#multi-tenancy-reminder](../../docs/internal/QUICK_REFERENCE.md#multi-tenancy-reminder)
- [docs/internal/BLAZOR.md](../../docs/internal/BLAZOR.md)
- [docs/internal/SECURITY-MODEL.md](../../docs/internal/SECURITY-MODEL.md)

## Verification
- Build: `dotnet build --configuration Release --verbosity quiet`
- Tests: `Explore.Blazor.IntegrationTests`, `Event.Architecture.Tests`

## Related
- Intents: `bff-auth-bug`
- Agents: `backend-engineer-agent.md`, `presentation-engineer-agent.md`, `quality-verifier-agent.md`
- Rules: `blazor-client.md`, `api-controllers.md`
```
