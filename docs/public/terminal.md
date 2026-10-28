# Terminal User Interface (TUI)

Configurator Terminal provides a fast, full-featured Terminal User Interface (TUI) powered by Terminal.Gui v2. It prepares production-ready configuration files locally from an admitted setup definition, without requiring network access, background daemons, or remote target connectivity.

All inputs and intermediate states are processed strictly in local memory. Sensitive values are masked during editing and never logged to stdout, command history, or external services.

---

## Prerequisites & Viewport Requirements

> [!IMPORTANT]
> **Terminal Viewport Requirements:**
> - **Minimum Content Viewport:** 80 columns × 26 rows.
> - **Minimum Outer Terminal Frame:** 82 columns × 28 rows (Linux terminal frame).
> - **Recommended Viewport:** 110 columns × 36 rows to keep all field guidance, container lists, and action buttons comfortably visible without clipping.

Run Configurator Terminal interactively in a standard terminal emulator. Standard input and output redirection (`<`, `>`, `|`) is refused for interactive sessions. To run noninteractive validations or scripts, use the [CLI](cli.md) instead.

---

## Launching the Terminal

Start the terminal client by pointing to a local definition file or an admitted definition registry:

```bash
# Launch with an explicit definition file (JSON or YAML)
dotnet run --project src/Configurator.Terminal/Configurator.Terminal.csproj \
  --configuration Release -- --definition examples/minimal-self-hosted/setup-definition.json

# Launch from an admitted definition registry by family and version
dotnet run --project src/Configurator.Terminal/Configurator.Terminal.csproj \
  --configuration Release -- --registry /path/to/definitions \
  --family 69cdb122-41d2-4fab-9b82-7929a889c726 --app-version 1.0
```

Local definition files can contain either a compact maintainer source (`$schema: urn:islamu:configurator:authoring:v2`) or an expanded runtime v2 definition. The engine compiles and admits compact sources before any answers are collected, generating canonical identities identical to the CLI and Desktop clients. See the [definition guide](definitions.md) for authoring details.

> [!NOTE]
> Definitions are frozen for the duration of the session. If you edit the definition source on disk, close and relaunch the terminal to load the updated revision.

---

## TUI Layout

The terminal interface organizes configuration into distinct navigation, editing, guidance, and action regions:

```text
┌─────────────────────────── ISLAMU Configurator TUI ───────────────────────────┐
│ Definition: Minimal Self-Hosted v1.0.0      Digest: sha256:7f83b1a2...        │
├──────────────────────────────────────┬────────────────────────────────────────┤
│ Configuration Fields                 │ Guidance & Consequence                 │
│                                      │                                        │
│ [*] Service Domain Name              │ Fully qualified domain name where      │
│     [ api.example.com              ] │ this service will receive traffic.     │
│                                      │ Type: string (FQDN)                    │
│ [ ] Enable TLS Termination           │ Requirement: Mandatory                 │
│     ( ) Yes   (*) No                 │                                        │
│                                      │ Consequence:                           │
│ [ ] Admin Password (Secret)          │ Stored in .env as SERVICE_PASSWORD.    │
│     [ •••••••••••••••••            ] │ Value is masked during editing.        │
│                                      │                                        │
│ Containers & Lists:                  │                                        │
│   [Supply container] [Clear]         │                                        │
│   [Previous item]    [Next/add]      │                                        │
│   [Add item]         [Remove item]   │                                        │
├──────────────────────────────────────┴────────────────────────────────────────┤
│ [Experience: Recommended ▼]  [Cycle choice]  [Apply field]  [Omit value]      │
│ [Validate fields]  [Cycle artifact]  [Prepare artifact]  [Save protected]     │
├───────────────────────────────────────────────────────────────────────────────┤
│ Status: Ready to prepare • 3 of 3 required fields completed                   │
└───────────────────────────────────────────────────────────────────────────────┘
```

---

## Keyboard Shortcuts

The TUI is designed for swift, keyboard-driven navigation:

| Key / Combination | Scope | Action |
| --- | --- | --- |
| `Tab` | Navigation | Move focus forward to the next field, control, or button |
| `Shift + Tab` | Navigation | Move focus backward to the previous field, control, or button |
| `Enter` | Actions | Activate the currently focused button or action |
| `Space` | Inputs | Toggle boolean switches, radio buttons, or cycle enum choices |
| `Alt + <Letter>` | Global | Jump directly to buttons with underlined access keys |
| `Left` / `Right` | Status & History | Pan through long status messages or history details |
| `Home` / `End` | Status & History | Jump to the beginning or end of status text |
| `Remove item` (twice) | List Management | Confirm removal of the selected list item |

---

## Workflow & Features

### Form Navigation and Field Editing

Move between fields using `Tab` and `Shift + Tab`, or use **Previous field** and **Next field** buttons. When modifying an input, press **Apply field** to commit your draft. 

Fields enforce strict typing based on the setup definition:
- **Strings, Numbers, and Decimals:** Invariant decimal formatting is validated on commit.
- **Booleans and Enums:** Use **Cycle choice** to advance choices sequentially. If an invalid draft is typed, correct it before cycling.
- **Explicit Omission:** Use **Omit value** to remove an answer completely. Empty strings (`""`) and admitted empty enum options remain explicit values; omission is an independent action.
- **Nullable values:** **Set null** supplies JSON null for a nullable location; it does not omit it. List-item null acts on the selected existing item.
- **Reset and suggestions:** **Reset value** removes overrides and explicit omission, allowing eligible initialization again. **Apply suggestion** deliberately adopts the declared suggestion; neither navigation nor language selection applies suggestions.
- **Initialization:** `x-configurator.initialValue` supplies eligible effective values without copying them into the supplied answer snapshot. Standard JSON Schema `default` is descriptive only. Requiredness does not create an absent parent container; explicit omission suppresses initialization until reset.

Protected secrets are masked on screen. Secret draft buffers exist only while focused; navigating away flushes the transient editing buffer.

### Working with Objects and Lists

Structured fields require explicit lifecycle management:

- **Objects:** Use **Supply container** to include an object container, even if empty (`{}`). Browsing blank properties does not create the object. Use **Clear container** to explicitly omit it and re-evaluate field requirements.
- **Lists:** Navigate rows with **Previous item** and **Next/add item**. When list capacity allows, the slot past the last row becomes a new-item slot.
- **Adding Items:** Leaving a new-item slot empty skips adding it. Activating **Add item** commits the typed item, including empty strings.
- **Double-Press Delete:** To prevent accidental data loss, removing an item requires pressing **Remove item** twice. Navigating away or editing any field cancels the pending deletion.

Typed maps expose a masked key-entry control on the map container. Enter an
exact literal key and activate **Add map entry**, then navigate to its typed
value. Empty keys, dots, slashes and numeric-looking keys are data, not path
syntax; duplicate keys are not replaced. Entry labels use local ordinals rather
than exposing dynamic keys in public guidance or diagnostics. Omit an entry to
remove it; declared bounds and value constraints still govern readiness.

For a discriminated provider object, **Cycle variant** selects a declared branch.
Only its selected children are available for editing. Switching branches drops
the previous payload; the shared engine supplies only eligible initialization.
Choice labels and translated branch titles never determine typed answer identity.

### Guidance, language and corrections

Use **Help** to page through admitted descriptions, instructions, units, choice
explanations and static guidance links. These are inert public text: no links
are fetched or executed. Paging keeps longer guidance reachable in the supported
minimum viewport.

**Locale** cycles declared definition languages with base-text fallback. Language
changes public metadata, not answer values, schema identity, input revision or
prepared output. Shipped English/Arabic action resources follow process UI
culture separately. Terminal rendering is not a claim of full bidirectional
layout or assistive-technology qualification.

**Next correction** resolves current shared diagnostics to a reachable editor,
including nested items and map entries. Coordinates from an earlier input
revision are rejected; private map keys and values do not become status text.

### Journeys and Disclosure Levels

Definitions can author tailored configuration journeys (e.g., *Recommended*, *Advanced*, *Minimal*):

- **Experience Selector:** Cycle through authored journeys or use the neutral **Configure** experience.
- **Optional Settings:** Toggle optional editor expansion to inspect advanced settings. Collapsing the view restores authored disclosure without discarding entered values. Journeys organize navigation; selecting a different journey does not itself expand optional detail.
- **Persistent Errors:** Active validation errors and required fields remain accessible regardless of the selected journey.
- **Preserved Drafts:** Rejected input text is preserved when switching views, allowing you to correct mistakes without starting over.

### Prefilling and Importing Existing Files

You can prefill answers by importing existing local `.json`, `.yaml`, or `.env` files:

1. Use **Cycle artifact** to select the target declared artifact.
2. Enter the path to your existing configuration file in **Import file**.
3. Activate **Import selected artifact**.

```text
Local File (.env / JSON / YAML) ──> Structural Ingestion (≤ 4 MiB) ──> Form Answers
```

Imports support direct top-level copy mappings. If an import file contains unknown keys, conflicting answers, or syntax errors, the entire import is rejected cleanly without altering your current form answers. The import path field clears immediately when reading begins, and any manual edit during an import cancels the stale operation.

### Saving and Verifying Configuration

Once answers are completed, verify and export your configuration bundle:

1. **Validate Fields:** Activate **Validate fields** to check all field constraints and dependencies. Diagnostics display value-free codes and field paths.
2. **Select Artifacts:** Use **Cycle artifact** to review declared filenames, formats, and restart instructions. Toggle individual outputs with **Toggle output**. At least one artifact must be selected.
3. **Prepare Artifact:** Activate **Prepare artifact** to stage the output bundle. The TUI validates complete composition and calculates cryptographic digests without displaying secret bytes.
4. **Save Protected:** Activate **Save protected** to create files in the current working directory.

> [!IMPORTANT]
> **Create-Only Protection (Linux x64):**
> On Linux, Configurator writes files using a strict create-only native writer with owner-only (`0600`) permissions. Existing files are never overwritten. If an output file already exists, resolve the conflict or choose a different directory before retrying.

Outputs distinguish between two independent milestones:
- `installation-established`: The file was securely written to the publication path.
- `verification-failed`: An immediate post-write integrity check failed.

Configurator never deletes or rolls back previously written files. If a later file in a multi-artifact bundle fails, earlier installed files remain safely on disk. Review them in the working directory before re-running.

Use **Save history** to cycle through the 16 most recent save attempts to inspect artifact outcomes and error codes.

---

## Session Termination & Privacy

To protect sensitive configuration data:
- **Clear Private State:** Removes all entered secrets and invalidates prepared bundles while preserving non-sensitive public settings.
- **Closing the Session:** Exiting or sending termination signals (`SIGINT`, `SIGTERM`) flushes private memory buffers and detaches UI callbacks.
- **Graceful Shutdown:** Configurator waits up to two seconds for active disk operations to finish before exiting. If an operation cannot be verified before exit, status indicates an unknown outcome so you can inspect destination files manually.

Deterministic adapter/control tests and actual interactive process qualification
are separate evidence. A prepared configuration does not prove target acceptance,
grant apply permission or qualify a new operating-system host.
