---
name: configurator-integrate
description: Create or review a compact Configurator YAML or JSON integration and public secret policies, scaffold from a public JSON Schema, or prepare an optional definition-store contribution. Do not use for collecting private answers, generating credentials, applying configuration, or deploying the target application.
license: MIT
compatibility: Requires the standalone islamu-configurator CLI supporting the current 0.0.1 machine and definition contracts, and a native shell. No application startup, network access, or credentials are required.
---

# Adopt the bounded definition standard

## Rules

- Inspect only public configuration declarations, public schemas and synthetic examples. Never inspect `.env`, private answer files, connection strings, key stores or credentials to discover configuration.
- Keep exactly one handwritten `configurator.yaml`, `configurator.yml` or `configurator.json`. YAML is the default for a new handwritten integration; preserve an existing format and comments.
- Require reliable exact application versions and actual output behavior. Record missing facts as questions; do not invent versions, hot reload, target validation, host acceptance or administrator authority.
- Treat unknown sensitivity as sensitive. Review every nested leaf before retaining a public default. Secret fields never contain real values or defaults.
- Identity is allocated by `definition init`, never invented by the agent. Use `init --fork-from` for a new family from an admitted source; use `definition revise` within an existing family before building changed canonical content.
- Remote distribution uses compiled public JSON. A store is optional; standalone use must work first.
- Do not add target runtime dependencies, launch the target, install a tool implicitly, or publish a PR. Return a concrete review draft; publication requires explicit user authority.

## Workflow

1. Run `islamu-configurator --version`, `islamu-configurator info --machine`, and `islamu-configurator --help` from the installed standalone binary (never assume a Configurator source checkout or .NET SDK). Confirm command contract `islamu-configurator-command/v0.0.1` and string authoring/runtime/answer/fixture contracts `0.0.1` (`bounded-v0.0.1`). Product and public contracts remain `0.0.1` during development even across breaking changes; only current canonical routes are supported (`artifact`, `catalogue`, `definition`, `doctor`, `info`, `profile`, `schema`, `secret` — legacy `env`, `catalogue show`, `artifact open|format|export|preview`, and `definition schema` do not exist). Discover built-in schemas with `islamu-configurator schema list --machine` and `islamu-configurator schema export --kind authoring --text`; available kinds are `authoring`, `runtime`, `answers`, `command`, `fixtures`, and `store-curation`. A missing command is a prerequisite failure.
2. Read [authoring](resources/authoring.md) for ordinary field/output decisions and [secret policies](resources/secrets.md) when a target accepts passwords, tokens, hashes or private keys. Read [schema import](resources/schema-import.md) only when scaffolding from a public schema, or [store contribution](resources/store-contribution.md) only when preparing that optional contribution.
3. Use `islamu-configurator definition init` to create the source, then supply the target's meaningful facts. Express advanced mappings through the existing closed projection vocabulary. Keep output format independent from source syntax.
4. Run `islamu-configurator definition check`, `islamu-configurator definition lint --quality --strict`, and `islamu-configurator definition describe` on the selected source. Lint adds quality advisories (including conditionally required leaves and unannotated enum choices) after admission, not a replacement validator. Resolve every admission error; do not bypass unsupported assertions or classify private values public to obtain green output.
5. For an existing release, run `islamu-configurator definition revise` against its current canonical anchor (`--against <prior-canonical-json>` selects an explicit anchor), then inspect semantic impact via `islamu-configurator definition diff --against <prior> --source <candidate> --machine` to separate behavioral changes from display-only edits before `definition build`. Unchanged canonical content does not advance revision. Preserve immutable releases and verify actual machine results and digests.
6. Author bounded data-only synthetic fixtures (`schemas/configurator-fixtures.schema.json`, exported via `schema export --kind fixtures --text`) and run `islamu-configurator definition test --source <source> --fixtures <fixtures.json> --machine` plus `artifact create` / `artifact bundle-create` (and explicit `--draft` when exercising secret-deferred incomplete bundles). Compare output to independent target keys, types, and readiness; a secret-deferred draft reports `preparation: "incomplete"` and never implies complete readiness. Check host capability truthfully with `doctor --machine` (or `doctor --require protected-write --machine` when native restricted writes are mandatory). Keep adapter/package/host observations separate from target acceptance.
7. Return the source, generated release location and digest, executed commands/results, missing target facts and a review/PR draft. Explain the import transformation ledger and unverified target/host behavior. Only the target owns semantic acceptance, operator authorization and apply/restart.

## Verification

- `definition check` is read-only; a build creates canonical public output and rejects a conflicting existing artifact.
- Equivalent JSON/YAML sources produce the same canonical definition; comment-only changes do not require a revision.
- The smallest ordinary integration needs one source. Generated release history is retained; the optional store reuses that source unchanged.
- No real answers appear in source, diagnostics, review text or public artifacts.
