# Desktop Configuration App

Configurator Desktop is an offline graphical application built with Avalonia UI.
It guides operators through local configuration and prepares candidate files
(`.env`, JSON, YAML) according to an admitted definition. The target application
independently validates, authorizes and applies them.

Use the [support index](../internal/STATUS.md) to distinguish packaged targets
from actual-host and protected-save evidence.

Everything runs locally on your workstation. The desktop client makes no network calls, contains no background telemetry, and requires no target application credentials or remote access.

---

## Installation & Launch

On x86_64 Arch Linux, install the native [pacman development package](packaging.md#3-arch-linux-pacman-package). It provides the `islamu-configurator-desktop` binary and desktop application launcher for X11 and Wayland sessions without requiring an external .NET runtime.

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
| Direction controls | Follow the definition locale's direction, or explicitly override left-to-right/right-to-left layout |

---

## Configuration & Form Editing

### Form Controls

Fields dynamically render native controls matching their admitted v2 types:
- **Strings and Numbers:** Ordinary text inputs validating invariant decimal formatting.
- **Booleans:** Three-state checkboxes distinguish true, false and not supplied.
- **Enums:** Dropdown choices retain their declared ordinal and exact JSON type/value. Labels are display metadata, not identity: two choices with the same localized label remain distinct, and an empty-string choice is not **Not supplied**.
- **Protected Secrets:** Password-masked controls. Protected booleans and enums use masked inputs instead of open dropdowns to avoid leaking values.

### Presence, Initialization and Suggestions

These actions have different effects:

- **Set null:** Available for nullable fields; supplies a present JSON `null`, not an absent value.
- **Clear / Not supplied:** Explicitly omits the setting and suppresses its `initialValue`. Empty strings (`""`), false, zero and empty containers are supplied values, not omission. For a string list item, **Clear** retains an empty-string row; use **Remove item** to delete it.
- **Reset:** Removes the supplied override and explicit omission so eligible `x-configurator.initialValue` initialization can apply again. It does not promise a value where no initialization is declared.
- **Apply suggestion:** Deliberately adopts `x-configurator.suggestedValue`; a suggestion never fills answers automatically. Suggestion actions are withheld for protected fields or containers with protected descendants.

Standard JSON Schema `default` is descriptive only and is never inserted.
Initialization is resolved by the shared engine, including eligible children of
present containers; it does not create absent containers merely because their
children are required or initialized.

### Container Management

Structured configuration objects and lists provide explicit lifecycle controls:

- **Objects:** Click **Supply** to include an object container, even if empty (`{}`). Browsing empty child fields does not create the object. Click **Clear** to omit the container completely.
- **Lists:** Click **Add item** to append a new item row; click **Remove item** to delete the selected row.
- **Typed Maps:** Enter a literal key, then choose **Add entry**; **Remove entry** deletes that key and its value. Keys are exact and case-sensitive: dots, slashes and numeric-looking names are not navigation syntax. Duplicate keys, invalid bounded paths and additions beyond the declared `maxProperties` (or the engine's mapping ceiling) are blocked. Declared `minProperties` and value constraints still govern validation, so removing an entry can leave a correction to make. Surviving entry editors retain their identity.
- **Discriminated Branches:** Select a declared branch to edit its typed children. Reselecting the current branch preserves its data. Switching to another branch replaces the old payload with the new discriminator; only eligible shared initialization fills missing children. Old branch editors are disposed, not retained as a hidden answer cache.
- **Independent Child Validation:** Editing or correcting one child field does not invalidate healthy sibling fields. Unfinished drafts remain preserved while navigating between containers.

### Journeys and Disclosure

Setup definitions can organize configuration into guided experiences:

- **Experience Selector:** Switch between authored journeys (e.g., *Recommended*, *Production*) or neutral *Configure*.
- **Optional Editors:** Toggle **Show optional editors** to expand optional detail. Turning it off returns to authored disclosure without changing answers, initialization or current preparation.
- **Error Visibility:** Missing required answers and active validation errors remain visible and accessible regardless of the active journey.

### Definition Language and Direction

The definition locale selector uses admitted public translations with base-text
fallback for field titles/descriptions, choice labels and branch titles.
Instructions, units, choice explanations and static guidance links also feed
accessible help. Literal map keys are local answer data, not translated labels.
Changing locale does not change typed values, answer revision or prepared output.

**Follow locale direction** is enabled initially. Changing **Right-to-left
layout** selects an explicit direction override; enable follow-direction again
to resume the locale's direction. Neither direction control changes language.
Definition metadata localization is separate from shipped action text selected
by the process UI culture; this is not a complete Desktop translation claim.

### Validation and Preparation

Before generating files, verify answers and bundle readiness:

1. **Validate Answers:** Evaluates shared constraints and requests correction focus on the first visible error. Ordinary containers route focus toward a visible failing child; map and branch editors provide their key input or branch selector as correction destinations. Required/error paths remain reachable even when optional detail is collapsed.
2. **Select Artifacts:** Choose individual declared artifacts or check multiple outputs under **Bundle outputs**.
3. **Prepare Artifact / Bundle:** Staging validates full bundle composition, formats templates, and displays output digests without revealing secret plaintext.

### Importing Existing Files

Review existing `.json`, `.yaml`, or `.env` files against the selected definition:

1. Select the declared artifact that represents the file. A filename does not establish its scope or provenance.
2. Leave the purpose at **Compare with this definition**. Choose a file or enter its path, then start the review. Input is bounded to 4 MiB and stays local.
3. Review compatibility, coverage, finding completeness and autofill eligibility separately. Imported-file issues do not mark current workspace answers invalid. **Explain schema** opens declared guidance, never a current list row or map entry identified by an imported ordinal.
4. To adopt recovered values, deliberately choose **Fill fields**. Adoption rechecks current answers, omissions and drafts. A report never grants overwrite permission. Changing the purpose, artifact or source, cancelling, editing answers or replacing the definition retires the comparison.

Comparison and adoption return control to the native interface between bounded
work batches. **Escape** requests cancellation; the busy state remains until
the operation and owned input finish cleanup. A cancelled or superseded result
cannot restore a report or move focus later.

Comparison leaves current editors, accepted answers, origins, omissions, input
revision and prepared output untouched. Missing required imported members are
reported only where the selected mapping proves absence; settings outside its
coverage remain unassessed. Structural output changes may require regeneration.
The report cannot establish historical renames, release provenance, deployment
state or target acceptance.

**Fill fields** applies admissible recovered values atomically, replacing only
untouched initialization. Explicit conflicting answers, omissions, pending
invalid drafts, unmapped members or invalid supplied values refuse the whole
adoption. No best-effort subset is applied. Resolve conflicts deliberately, or
repair a separate copy of the file and compare again. Human-readable outputs and
ambiguous/lossy mappings may not support comparison or autofill.

### Custom Desktop storage integrations

Implementers of `IStorageDialogService` must now supply
`ReadImportAsync(CancellationToken)` returning `Task<BoundedFileInput?>`.
Open the selected provider's stream rather than translating its URI into a
native path. Bound acquisition to 4 MiB and finish provider stream/item cleanup
before returning the caller-owned input; clear acquired input on failure or
cancellation. The Desktop caller clears/disposes returned input after review.
This mandatory method intentionally breaks older implementations; there is no
default fallback. Definition and output selection still require local paths.

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

## Answer Custody and Qualification Limits

Answers and drafts remain transient local workspace state, not a persistent
answer store or public metadata. Replacing or closing the workspace disposes
its editors; answer edits invalidate preparation. Managed input strings cannot
promise secure memory erasure. Prepared artifacts remain private and saving
still crosses the native protected-save boundary above; typed controls and
localized metadata grant no target acceptance or apply authority.

Phase 10 typed v2 control documentation does not qualify a physical native host
or installed package. Headless window/control tests cannot establish those
properties, native assistive-technology behavior or translation quality.
Definition-standard-redesign Phase 12 qualification remains separate.
