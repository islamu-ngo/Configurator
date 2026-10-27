# Desktop Configuration App

Configurator Desktop is an offline graphical application built with Avalonia UI. It guides operators through configuring self-hosted applications and generates production-ready configuration files (`.env`, JSON, YAML).

Everything runs locally on your workstation. The desktop client makes no network calls, contains no background telemetry, and requires no target application credentials or remote access.

---

## Installation & Launch

On x86_64 Arch Linux, install the native [pacman development package](packaging.md#arch-linux-development-package). It provides the `islamu-configurator-desktop` binary and desktop application launcher for X11 and Wayland sessions without requiring an external .NET runtime.

To launch from a local source repository:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --project src/Configurator.Desktop/Configurator.Desktop.csproj \
  --configuration Release -- /path/to/setup.json /path/to/output-directory
```

Command-line arguments are optional:
- **No arguments:** Click **Choose definition…** to select a definition file, or enter its path and click **Load definition**.
- **One argument:** Automatically loads the specified definition file on launch.
- **Two arguments:** Loads the definition and sets the target output directory.

The application bar displays the loaded definition's title, application ID, publisher, family UUID, revision number, and computed SHA-256 digest pin. Changing the definition file on disk does not alter the running workspace; select **Reload** to explicitly admit an updated revision.

---

## Desktop GUI Layout

The desktop interface organizes controls into intuitive navigation, form editing, and output staging panels:

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│  ISLAMU Configurator Desktop                                 [ _ ][ □ ][ X ] │
├──────────────────────────────────────────────────────────────────────────────┤
│  Definition: Minimal Self-Hosted v1.0.0    Digest: sha256:7f83b1a2...        │
│  Definition Path: [/path/to/setup.json                ] [Choose definition…] │
│  Output Folder:   [/path/to/output-dir                ] [Choose folder…    ] │
├──────────────────────────────────────┬───────────────────────────────────────┤
│  Configuration Workspace             │  Guidance & Details                   │
│                                      │                                       │
│  Experience: [ Recommended        ▼] │  Field: service.name                  │
│  [x] Show optional settings          │  Type: string                         │
│                                      │  Requirement: Required                │
│  ▼ General Settings                  │                                       │
│    Service Domain:                   │  Fully qualified domain name for this │
│    [ api.example.com               ] │  service deployment.                  │
│                                      │                                       │
│    Database Password:                │  Consequence:                         │
│    [ •••••••••••••••••             ] │  Stored securely in .env as           │
│                                      │  DB_PASSWORD.                         │
│  ▼ Advanced Clustering               │                                       │
│    Container: [Supply] [Clear]       │                                       │
│    Nodes: [Add item] [Remove item]   │                                       │
├──────────────────────────────────────┴───────────────────────────────────────┤
│  Declared Outputs: [x] .env   [x] service.json                               │
│  [Validate Answers]   [Prepare Bundle]   [Save New Protected File]           │
├──────────────────────────────────────────────────────────────────────────────┤
│  Status: All required fields valid • Ready to prepare bundle                 │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## Keyboard Shortcuts

Navigate quickly using native keyboard controls:

| Shortcut | Action |
| --- | --- |
| `Tab` | Move focus forward to the next field, control, or button |
| `Shift + Tab` | Move focus backward to the previous field, control, or button |
| `Ctrl + O` | Focus the definition file path input |
| `Ctrl + Enter` | Prepare the currently selected artifact or bundle |
| `Escape` | Cancel active background operation or discard prepared bundle |
| `Alt + <Letter>` | Activate button matching the underlined access key |
| `RTL Toggle` | Switch layout direction between left-to-right and right-to-left |

---

## Configuration & Form Editing

### Form Controls

Fields dynamically render native controls matching their declared types:
- **Strings and Numbers:** Ordinary text inputs validating invariant decimal formatting.
- **Booleans and Enums:** Dropdown selectors or radio groups, including an explicit unset choice.
- **Protected Secrets:** Password-masked controls. Protected booleans and enums use masked inputs instead of open dropdowns to avoid leaking values.
- **Explicit Omission:** Empty strings (`""`) and admitted empty enum choices are valid explicit values. To leave a setting unconfigured, choose **Clear** or **Not supplied** so declared defaults apply.

### Container Management

Structured configuration objects and lists provide explicit lifecycle controls:

- **Objects:** Click **Supply** to include an object container, even if empty (`{}`). Browsing empty child fields does not create the object. Click **Clear** to omit the container completely.
- **Lists:** Click **Add item** to append a new item row; click **Remove item** to delete the selected row.
- **Independent Child Validation:** Editing or correcting one child field does not invalidate healthy sibling fields. Unfinished drafts remain preserved while navigating between containers.

### Journeys and Disclosure

Setup definitions can organize configuration into guided experiences:

- **Experience Selector:** Switch between authored journeys (e.g., *Recommended*, *Production*) or neutral *Configure*.
- **Optional Editors:** Toggle **Optional editors** to reveal advanced settings. Turning it off restores authored defaults without discarding entered values.
- **Error Visibility:** Missing required answers and active validation errors remain visible and accessible regardless of the active journey.

### Validation and Preparation

Before generating files, verify answers and bundle readiness:

1. **Validate Answers:** Evaluates all field constraints and automatically moves focus to the first invalid visible field.
2. **Select Artifacts:** Choose individual declared artifacts or check multiple outputs under **Bundle outputs**.
3. **Prepare Artifact / Bundle:** Staging validates full bundle composition, formats templates, and displays output digests without revealing secret plaintext.

### Importing Existing Files

Reuse existing settings by importing local `.json`, `.yaml`, or `.env` files:

1. Select a declared artifact format.
2. Click **Choose import…** or enter a file path and click **Import existing file**.
3. The engine parses the file (up to 4 MiB) and maps top-level fields to matching definition questions.

> [!NOTE]
> Imports must match simple declared field mappings. Conflicting answers, unknown keys, or invalid types reject the entire import cleanly, leaving your current workspace unchanged.

---

## Protected Saving

### Linux Protected Save Contract

> [!IMPORTANT]
> **Linux Protected Save Contract (`0600`):**
> On Linux x64, Configurator writes configuration files using a native create-only writer with owner-only (`0600`) permissions. Existing files are never overwritten. Protected saving fails closed on unsupported platforms without plain-file fallback.

To publish files safely:
1. Click **Choose folder…** to select an existing directory, then enter a target filename (up to 128 characters: letters, numbers, dots, dashes, underscores).
2. Click **Save new protected file** or **Save protected bundle**.
3. The writer verifies that no target files exist before committing any file to disk.

### Understanding Publication Outcomes

The desktop client separates installation from verification:
- `installation-established`: The file was successfully written with restricted permissions.
- `save-installed-unverified`: The file was created, but immediate post-installation verification failed. Inspect the file manually before using it.
- `cleanup-failed`: Temporary writer resources could not be cleanly released, but written files remain valid.

If a failure occurs during a multi-file bundle save, previously written files are kept intact. Review the **Publication facts** panel for individual artifact outcomes, then correct remaining items and prepare again.

---

## Window Closure & Pending Tasks

Closing the window while background reads or writes are active triggers cooperative cancellation:
- **Automatic Settlement:** The window waits up to two seconds for active disk operations to finish before exiting cleanly.
- **Stay Open:** If operations are still running after the deadline, choose **Stay open** to keep the window accessible while work settles.
- **Force Close:** Choose **Force close** to terminate immediately. If forced, check target directories manually to inspect any partially written files.
