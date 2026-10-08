> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Hook configuration and customization guide for the ISLAMU Event project. -->
<!-- Covers hook registration format, agent integration mapping, and customization patterns. -->

# Hooks Configuration (.NET 10)

## Hook Registration Format

Hooks are registered in `.claude/settings.json` using the nested object format:

```json
{
  "hooks": {
    "EventName": [
      {
        "type": "command",
        "command": "dotnet .agents/hooks/ScriptName.cs"
      }
    ]
  }
}
```

For events with matchers (PreToolUse, PostToolUse):

```json
{
  "hooks": {
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
    ]
  }
}
```

## Agent Integration Mapping (Historical Event Source)

This retained mapping describes source-system suggestions, not Configurator agent activation or build-error routing. BuildCheck emits the targeted build output directly and returns its actual exit code; it does not invoke any agent.

| Hook Trigger | Targeted Agent | Purpose |
|---|---|---|
| Build failure (Stop) | `auto-error-resolver` | Parses CS errors and applies C# fixes |
| Auth keywords (UserPromptSubmit) | `auth-route-debugger` | Debugs Keycloak JWT and Cerbos policies |
| Frontend keywords (UserPromptSubmit) | `frontend-error-fixer` | Fixes Blazor lifecycle and MudBlazor syntax |
| Architecture keywords (UserPromptSubmit) | `code-refactor-master` | Validates CQS pattern and native operation usage |
| Test keywords (UserPromptSubmit) | `codebase-verifier` | Runs build/test verification sequence |
| Outbox keywords (UserPromptSubmit) | (skill suggestion) | Points to `outbox-pattern` skill |
| Design system keywords (UserPromptSubmit) | (skill suggestion) | Points to `design-system` skill |
| Footer keywords (UserPromptSubmit) | (skill suggestion) | Points to `footer-management` skill |

## Customization

### Adding New Triggers to SkillTrigger.cs

Add a new keyword block in the trigger rules section:

```csharp
// New Feature Area
if (prompt.Contains("keyword1") || prompt.Contains("keyword2"))
    suggestions.Add("  Consult 'skill-name' skill for feature guidance.");
```

### Adjusting Architecture Detection

The following is the historical Event layer-detection example, not Configurator project ownership. If adapting tracking in a separately authorized change, use the actual owners from `PROJECTS.md`. BuildCheck never consumes this mapping:

```csharp
// Map path patterns to architecture layers
static string DetectCleanArchLayer(string filePath) =>
    filePath switch
    {
        _ when filePath.Contains("Event.Domain") => "Domain",
        _ when filePath.Contains("Event.Application") => "Application",
        _ when filePath.Contains("Event.Persistence") => "Infrastructure",
        _ when filePath.Contains("Event.API") => "API",
        _ when filePath.Contains("Explore.Blazor") => "Frontend",
        _ => "Shared"
    };
```

### Selecting the Targeted Build

BuildCheck requires exactly one existing `.csproj`; there is no no-argument or solution fallback. Choose the affected owner from `PROJECTS.md`:

```bash
dotnet run .agents/hooks/BuildCheck.cs -- eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj
```

That engineering-owner invocation is an example, not a default. For a Stop registration, use the same complete command after selecting the affected project; omit the build entry for documentation-only work. The retained implementation adds `build`, the project path, `--configuration Release`, `--verbosity quiet`, and `--nologo` through `ProcessStartInfo.ArgumentList`. It drains stdout/stderr concurrently and returns the actual build exit code. Invalid arguments return `64`; a three-minute timeout kills the process tree, emits `build_check_timeout`, and returns `1`. Do not add warning suppressions to make a build pass.

## Cache Management

- **Location**: `.claude/build-cache/`
- **Contents**: ContextTracker's historical layer tracking and session logs. BuildCheck does not cache build errors.
- **Ownership**: BuildCheck neither reads nor clears the cache; it emits build output directly.
- **Manual cleanup**: Delete the `build-cache` directory to reset tracking state.
