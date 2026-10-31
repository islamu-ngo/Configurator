> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Hook system documentation for Claude Code integration. -->
<!-- Describes 4 C# hooks (SkillTrigger, ContextTracker, FormatCode, BuildCheck) and their configuration. -->

# Hooks (.NET 10 / C#)

Retained C# hook examples for Claude Code. Registration is opt-in, not evidence
that hooks are installed or active. SkillTrigger/ContextTracker retain
source-system suggestions and layer tracking; these are advisory historical
metadata, not target skill activation or project ownership. Resolve the current
intent and actual owner before applying a suggestion.

## What Are Hooks?

Hooks are C# scripts that run at specific points in Claude's workflow:
- **UserPromptSubmit**: Checks your prompt to suggest specialized agents/skills.
- **PostToolUse**: Tracks which Clean Architecture layers are being modified.
- **Stop**: Runs validation tasks like `dotnet build` and `dotnet format` when Claude finishes.

All hooks are written in C# and executed via `dotnet` CLI — no Node.js or Bash required.

## Hook Inventory

### 1. SkillTrigger.cs (UserPromptSubmit)

Reads the user prompt and suggests relevant agents/skills based on keyword matching.

**Triggers**: auth (401/403/keycloak/cerbos), frontend (blazor/mudblazor/css/razor), architecture (refactor/clean arch/cqs/cqrs/mapperly), build errors (error cs/build fail), database (ef core/migration/postgres), testing (tunit/bunit/mock), outbox (dead letter/message dispatch), design system (design token/wrapper/appearance), footer (social links/footer template), accessibility (wcag/aria/a11y), secrets (infisical/vault/encryption).

### 2. ContextTracker.cs (PostToolUse)

Monitors Edit/Write tool calls, detects the Clean Architecture layer from file paths, and logs to `.claude/build-cache/`. Updates `context-state.json` with the most recently touched layer.

**Layer detection**: Domain, Application, Infrastructure, API, Frontend, Shared.

### 3. FormatCode.cs (Stop)

Runs `dotnet format` on modified files to enforce `.editorconfig` rules.

### 4. BuildCheck.cs (Stop)

Verifies compilation of exactly one existing `.csproj`, supplied explicitly:

```bash
dotnet run .agents/hooks/BuildCheck.cs -- eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj
```

Choose the affected owner from `PROJECTS.md`; this engineering project is an example, not a default. The hook runs `dotnet build <project> --configuration Release --verbosity quiet --nologo`, drains stdout and stderr concurrently, and returns the actual build exit code. Missing, extra, non-`.csproj`, or nonexistent arguments return usage exit code `64`. The three-minute deadline kills the process tree, emits `build_check_timeout`, and returns `1`.

BuildCheck does not read ContextTracker state, discover a solution, cache errors, invoke an error resolver, or clean up tracking files.

## Configuration

Hooks may be registered in local `.claude/settings.json`; this is not a
repository requirement. The Stop build below is an explicit engineering-owner
example; select the affected project before enabling it. Omit both FormatCode
and BuildCheck Stop entries for documentation-only work rather than running
automatic formatting or an unrelated product build:

```json
{
  "hooks": {
    "UserPromptSubmit": [
      {
        "type": "command",
        "command": "dotnet .agents/hooks/SkillTrigger.cs"
      }
    ],
    "PreToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          {
            "type": "command",
            "command": "dotnet .agents/hooks/SecurityCheck.cs"
          }
        ]
      }
    ],
    "PostToolUse": [
      {
        "matcher": "Edit|MultiEdit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "dotnet .agents/hooks/ContextTracker.cs"
          }
        ]
      }
    ],
    "Stop": [
      {
        "type": "command",
        "command": "dotnet .agents/hooks/FormatCode.cs"
      },
      {
        "type": "command",
        "command": "dotnet run .agents/hooks/BuildCheck.cs -- eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj"
      }
    ]
  }
}
```

## Supported Hook Events

| Event | When It Fires | Use Case |
|-------|---------------|----------|
| `UserPromptSubmit` | Before processing user message | Skill/agent suggestions |
| `PreToolUse` | Before a tool executes | Security checks (Bash validation) |
| `PostToolUse` | After a tool executes | File change tracking |
| `Stop` | When Claude finishes a task | Build verification, formatting |
| `Notification` | On system notifications | (not currently used) |
| `SubagentStop` | When a subagent completes | (not currently used) |

## Troubleshooting

### "dotnet: command not found"
.NET SDK is not in PATH. Run `dotnet --version` to verify installation.

### Hooks failing on Windows
Path separator issues. The C# scripts handle path normalization automatically. Use relative paths like `dotnet .agents/hooks/...` in settings.json.

### Build is too slow
Pass only the affected owner's `.csproj` from `PROJECTS.md`. ContextTracker's historical layer cache does not select a BuildCheck project. Documentation-only work uses link, schema, and whitespace checks instead of a build.

## Cache

- **Location**: `.claude/build-cache/`
- **Ownership**: ContextTracker owns its tracking cache. BuildCheck neither reads nor deletes it; build output is emitted directly to stdout/stderr.
