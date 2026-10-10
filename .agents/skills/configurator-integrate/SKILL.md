---
name: configurator-integrate
description: Create or review a compact Configurator YAML/JSON source, scaffold a bounded public JSON Schema, or prepare an optional store contribution. Do not use for private answer collection, target apply, or deployment.
type: workflow
enforcement: suggest
priority: high
---

## Workflow

Read and execute the canonical [portable integration workflow](../../../skills/configurator-integrate/SKILL.md). It owns the native command sequence, privacy decisions and routed resources; do not reproduce or replace that workflow here.

## Verification

```bash
dotnet run --file eng/configurator/ValidateConfiguratorSkill.cs -- .agents/skills/configurator-integrate/SKILL.md
dotnet run --file eng/configurator/ValidateConfiguratorSkill.cs -- --portable skills/configurator-integrate/SKILL.md
```
