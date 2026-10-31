> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- ABOUTME: Intent-manifest guidance for skill creation and agent-context infrastructure changes. -->
<!-- Explains how agent-context answers the repository Contribution Contract. -->

# Intent Contract

## Intent ID

Use `agent-context` for skills, resources, agent contracts and harness routing.
Use `documentation` for ordinary guides and navigation; `provenance-docs` is
specific to licensing and transfer evidence, not a general documentation route.

## Files In Scope

Primary files are:

- `.agents/skills/<skill-name>/SKILL.md`
- `.agents/skills/<skill-name>/resources/*.md`
- `.agents/contract/intents.yaml`
- `.agents/benchmarks/` for manually exercised routing scenarios
- `.omo/rules/` twins and harness pointers declared by the intent

Do not use this intent as permission to edit application code. If a skill references application behavior, link to source docs or code and keep the application change under its own intent.

## Required Reads

Always read the schema and the active source material before editing. For domain-heavy skills, read the upstream research, plan, or implementation notes that justify each invariant and resource.

## Acceptance Criteria

- The new skill is schema-compliant without skip-list changes.
- The skill has a precise activation boundary and clear non-goals.
- The resource library is reachable through `resources/index.md`.
- Claims are evidence-bounded and do not overstate validation.
- Review metadata, links and intent references directly. The native YAML guard
  checks bounded syntax only; it does not prove routing semantics or benchmark success.

## Forbidden Moves

- Do not add the new skill to `SkipSchemaMigration`.
- Do not create a skill that relies on prior chat context for essential instructions.
- Do not claim certification, production behavior, or external validation unless the source evidence proves it.
- Do not bury verification instructions in prose; keep commands copy-pasteable.
