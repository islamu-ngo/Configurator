---
name: configurator-integrate
description: Create or review a compact Configurator YAML or JSON integration for a tool, scaffold from a public JSON Schema, or prepare an optional definition-store contribution. Do not use for collecting private answers, applying configuration, or deploying the target application.
license: MIT
compatibility: Requires the standalone islamu-configurator CLI supporting definition authoring v1 and a native shell. No application startup, network access, or credentials are required.
---

## Rules

- Inspect only public configuration declarations, public schemas and synthetic examples. Never inspect `.env`, private answer files, connection strings, key stores or credentials to discover configuration.
- Keep exactly one handwritten `configurator.yaml`, `configurator.yml` or `configurator.json`. YAML is the default for a new handwritten integration; preserve an existing format and comments.
- Require reliable exact application versions and actual output behavior. Record missing facts as questions; do not invent versions, hot reload, target validation, host acceptance or administrator authority.
- Treat unknown sensitivity as sensitive. Review every nested leaf before retaining a public default. Secret fields never contain real values or defaults.
- Identity is allocated by `definition init`, never invented by the agent. A changed canonical definition requires `definition revise` against its prior release before another build.
- Remote distribution uses compiled public JSON. A store is optional; standalone use must work first.
- Do not add target runtime dependencies, launch the target, install a tool implicitly, or publish a PR. Return a concrete review draft; publication requires explicit user authority.

## Workflow

1. Run `islamu-configurator --help` and inspect `islamu-configurator definition --help`. Confirm command contract `islamu-configurator-command/v2` and authoring schema v1 before authoring. Use the installed tool's declared options; a missing command is a prerequisite failure.
2. Read [authoring](resources/authoring.md) for ordinary field/output decisions. Read [schema import](resources/schema-import.md) only when scaffolding from a public schema, or [store contribution](resources/store-contribution.md) only when preparing that optional contribution.
3. Use `definition init` to create the source, then supply the target's meaningful facts. Express advanced mappings through the existing closed projection vocabulary. Keep output format independent from source syntax.
4. Run `definition check` on the selected source. Resolve every admission error. Do not bypass unsupported schema constraints or classify private values public to obtain green output.
5. For an existing release, use `definition revise --against <prior-canonical-json>` after semantic changes. Then run `definition build`. Preserve immutable release files and verify actual machine results and digests.
6. Exercise a small complete and incomplete synthetic configuration through the CLI's declared validation/preparation commands. Compare output to independently specified target keys, types and readiness. Never treat preparation as target acceptance.
7. Return the source, generated release location and digest, executed commands/results, missing target facts and a review/PR draft. Explain any importer differences and unverified target/host behavior.

## Verification

- `definition check` is read-only; a build creates canonical public output and rejects a conflicting existing artifact.
- Equivalent JSON/YAML sources produce the same canonical definition; comment-only changes do not require a revision.
- The smallest ordinary integration needs one source. Generated release history is retained; the optional store reuses that source unchanged.
- No real answers appear in source, diagnostics, review text or public artifacts.
