# Terminal User Interface (TUI)

Configurator Terminal provides a keyboard-driven Terminal User Interface (TUI)
powered by Terminal.Gui v2. It prepares candidate configuration files locally
from an admitted definition, without network access, background daemons or remote
target connectivity. The target independently validates and authorizes their use.

The workspace's selected language controls its buttons, help and save guidance.
English and Arabic are supported, with English fallback for other locales.
Changing the workspace language does not change the host's language. Save
receipts distinguish file creation, immediate verification and cleanup; a
created file's continued integrity is never guaranteed by the receipt.

Use **Output tasks** to choose a task with Tab and Space. Task status and related
tasks appear after an acknowledged, verified native save. An installed but
unverified file remains in the receipt without enabling follow-up guidance.
The chooser shows up to eight tasks per page. For longer lists, use
**Next task page** and **Previous task page** to reach every task.
Preparing a file alone does not complete a task, and saving never switches tasks.
Answers stay in the local workspace for subsequent tasks. Use Clear private
when finished; closing the workspace also retires private state.

Choosing a task opens a value-transfer review. Use **Next suggested value** to inspect candidates
and **Select / deselect value** to choose each value. Supplied destinations and
omissions start unselected; selecting them explicitly authorizes replacement.
Protected values remain masked. Apply the selected values and switch, switch
without copying, or cancel. **Review suggested value** offers the same review
for the current field. Changes to the workspace retire the review; open it again
before applying. Transfers remain local and independently editable, with one undo.

The [support index](../internal/STATUS.md) distinguishes actual Terminal process
exercises from Desktop X11 scenarios and package admission.

All inputs and intermediate states are processed strictly in local memory. Sensitive values are masked during editing and never logged to stdout, command history, or external services.

---

## Prerequisites & Viewport Requirements

> [!IMPORTANT]
> **Terminal Viewport Requirements:**
> - **Minimum Outer Terminal Frame:** 80 columns × 24 rows. Keyboard focus scrolls the content to keep the active control visible.
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
  --family 4d8950ad-972c-4357-8605-7a7c89c05a17 --app-version 1.0
```

Local definition files can contain either a compact maintainer source (`$schema: urn:islamu:configurator:authoring:v0.0.1`) or an expanded runtime 0.0.1 definition. The engine compiles and admits compact sources before any answers are collected, generating canonical identities identical to the CLI and Desktop clients. See the [definition guide](definitions.md) for authoring details.

> [!NOTE]
> Definitions are frozen for the duration of the session. If you edit the definition source on disk, close and relaunch the terminal to load the updated revision.

---

## TUI Layout

The workbench organizes setup into three responsive keyboard stages tailored for an `80×24` minimum terminal frame and wider consoles:

1. **`1:Configure`** — Unified Master-Detail split workbench pairing a left section outline (`[!]` error, `[*]` missing required, `[v]` reviewed, `[o]` opened, `[ ]` unvisited) with a right detail editor containing rich metadata (`Units`, `If omitted` guidance, deprecation notices, inline validation, full vertically scrollable help without paging buttons, and `Ctrl+Z` undo).
2. **`2:Review & Import`** — Inspect readiness counts, jump to remaining issues or search via **Find…**, review selected output files, and compare or adopt local `.json`, `.yaml`, or `.env` exports using vertically stacked controls that adapt without fixed-row overlaps.
3. **`3:Files & Save`** — Validate, prepare output bundles, save create-only protected files, and inspect per-artifact save receipts and history.

The top row also exposes **Profiles** (`Ctrl+P`) alongside utility actions (**Human**/**Wire** naming, **Find…**, **Doc…**, **Undo**, **Output tasks**) and a bottom 5-key `StatusBar` (`F2` Sections, `F3` Find, `Ctrl+E` Doc, `F1`/`?` Keys, `F10` Close).

When the viewport width is `88` columns or wider, the `32`-column **Sections** navigator stays docked on the left (focusable with `F2`); at `76–87` columns, **Sections…** (`F2`) opens the navigator as a centered modal dialog. Viewports narrower than `76×20` hide interactive controls and display a resize prompt.

Top utility controls toggle **Human** (humanized) and **Wire** (technical) naming, search settings and actions with **Find…** (`F3` or `/`), open the bounded public-scope **Doc…** editor (`Ctrl+E`, capped at `256 KiB` with pre-apply diff preview and stale-revision protection), and trigger **Undo** (`Ctrl+Z`). Nested objects, lists, maps, and variants share these same stages.

### Direction and remaining scope

The project retains official Terminal.Gui for the long-lived operator and
developer workbench while browser-first development supports opening the static
browser client from a project's README Configure link. Future opt-in native
Infisical/Azure Key Vault operations require separate design and are not
available in this client. When they arrive, they will run through ISLAMU
SecretKit, never through vendor CLIs. Over SSH, sign-in will use a device code
instead of a remote browser. Azure may optionally reuse an existing `az login`
as a credential source
([decision](../internal/adr/ADR-secret-store-integration-and-credential-sources.md)). Current comparison checks an imported local configuration
against the selected definition and workspace; schema-to-schema migration inference
is out of scope. Target validation and apply permission remain with the application.
See the [decision and tradeoffs](../internal/adr/ADR-terminal-framework-and-workbench.md)
for the direction and its qualification boundaries.

---

## Keyboard Shortcuts

### Encrypted local profiles

Press **Ctrl+P** to open the local profile commands. Tab and Shift+Tab move
through the scrollable dialog; Enter activates a focused command. Select an
absolute local store directory, run **List revisions**, and enter its zero-based
revision index. Discovery names are unverified until authenticated opening.
The recipient index is also zero-based.

Enter the profile passphrase in its separate masked field, independently of
target credentials. New saves require confirmation and at least 15 characters;
length alone does not guarantee strength. **Save import/upgrade only** retains
secrets for deliberate upgrade/output without revealing them. **Save inspectable
encrypted** permits normal editable secret custody after unlock.
**Save comparison only** strips recoverable secrets and cannot restore them;
its remaining operational data can still be private.

The dialog displays one reviewed JSON JWE policy: `A256GCM`,
`PBES2-HS512+A256KW`, PBKDF2-HMAC-SHA512 with 600000 iterations. Unsupported
algorithms are refused without trying alternatives. This is no FIPS claim.

**Unlock profile** opens the selected revision into the shared workspace.
**Compare schema** reports value-free schema and carry eligibility counts.
**Upgrade snapshot** revalidates and seals a fresh immutable snapshot, preserving
the opened protection mode and eligible held secrets. To replace a retained
value, enter its field pointer and activate **Retire retained value** twice,
then close the dialog and enter the replacement in Configure. Retirement
survives undo. Held values have no reveal, copy or document-edit channel.

**Unencrypted target: Files and Save** returns an ordinary workspace to the
existing Prepare/Save controls. With a held profile, enter an artifact ID and
an absolute target output file first: the command composes and directly saves
the restricted artifact without previewing held secrets. Every native save is
create-only; unsupported protected-save hosts refuse without ordinary-file
fallback. Inspect the result for installation, verification and cleanup facts.

Portable passphrase recovery is recommended. Toggle the device keyring option
to add a native recipient. An empty passphrase makes saving device-only and
requires explicit acceptance of permanent loss risk. Missing, locked or denied
keyrings, including SSH sessions without the desktop service, produce safe
status codes; choose a configured portable recipient explicitly to unlock.
Failed lookup never creates a replacement key. Save receipts include the
snapshot identity, trusted ciphertext digest, device recipient ID and initial
recovery state. Retain those recovery facts independently of the file.

To check recovery, copy the revision into another restricted store, select that
store and revision, enter the trusted digest and optional device recipient ID
from the original receipt, and supply only the portable passphrase. Use recipient
0 for portable-only files or 1 when the native recipient precedes it.
**Check portable recovery** uses a fresh codec without keyring access and reports
verified-for-this-snapshot only after checking the exact ciphertext and identities.
Configured-but-unverified and device-bound-only are distinct states.

Losing every recipient makes recovery impossible; a vault copy alone is
insufficient. Reinstallation and support cannot reconstruct keys. Uninstall or
reset may leave files or keyring entries, but preservation is not guaranteed.
New passphrases protect new snapshots; old copies retain old access and target
credentials are not rotated. For headless recovery use the bounded
[CLI profile commands](cli.md), with separate credential inputs.

The profile workflow tests exercise save, unlock, upgrade and recovery with
synthetic data. English and Arabic terminal sessions also verified Tab traversal
through the scrolling commands and Cancel at 120 by 40 and 80 by 24 cells.
Terminal-specific Arabic shaping and assistive-technology qualification remain
separate host checks.

The TUI is designed for swift, keyboard-driven navigation:

| Key / Combination | Scope | Action |
| --- | --- | --- |
| `Tab` / `Shift + Tab` | Navigation | Move focus forward or backward between panes, fields, and buttons |
| `Enter` / `Space` | Actions & Inputs | Activate the focused button or select an outline section (records `[o]` opened) |
| `j` / `k` | Navigation (outside text input) | Move to the next / previous setting without stealing cursor typing in inputs |
| `g` / `G` | Navigation (outside text input) | Jump to the first / last setting in the active workspace |
| `/` | Discovery (outside text input) | Open **Find…** quick settings & actions dialog |
| `m` | Review (outside text input) | Toggle section reviewed coverage (`[v]`) on the current outline section |
| `?` / `F1` | Global | Open the modal keyboard shortcut reference (`?` requires non-text focus) |
| `F2` | Sections | Focus the docked section outline (`>= 88` cols) or open **Sections…** modal (`< 88` cols) |
| `F3` | Discovery | Open **Find…** to filter and jump to settings, errors, or actions |
| `F7` / `F8` / `F9` | Actionable jumps | Jump to next validation error (`F7`), next required setting (`F8`), or next unvisited section (`F9`) |
| `Ctrl + E` | Document | Open the public-scope document editor (`256 KiB` cap, diff preview) |
| `Ctrl + Z` | Editing | Undo the last committed public/workspace mutation |
| `Ctrl + P` | Profiles | Open encrypted local profile commands |
| `F10` | Global | Close workbench safely |
| `Left` / `Right`, `Home` / `End` | Status & Help | Pan status text; `Up`/`Down`/`PageUp`/`PageDown` scroll the full help pane |
| `Remove item` (twice) | List Management | Confirm removal of the selected list item |

---

## Workflow & Features

### Deployment recipe and component scope

Definitions that declare deployment choices show each compatible recipe and
component scope before the field controls. The selected pair is marked
**selected**. Use Tab or Shift+Tab to focus a different labelled pair and Enter
to choose it. Both answers change together; an invalid field draft must be
corrected before switching. Detail and language controls do not change scope.

Required fields and available output destinations follow that scope. A
dashboard-only or API-only workspace can be complete without another
component's credentials. Selecting fewer output files inside an all-component
workspace still requires completing that workspace's scope. Inactive answers
remain in transient memory for switching back; they do not become output.
Review shows ready, total, missing, invalid and deferred counts in the selected
interface language; those counts come from the current shared workspace state.

Changing the pair retires preparation and import results. Review the current
destination, prepare again, and use the existing protected-save action.
Completion describes local preparation for the selected components; the target
application still owns acceptance and apply permission.

### Form Navigation and Field Editing

Move between fields using `Tab` and `Shift + Tab`, or use **Previous field** and **Next field** buttons. When modifying an input, press **Apply field** to commit your draft. 

Fields enforce strict typing based on the setup definition:
- **Strings, Numbers, and Decimals:** Invariant decimal formatting is validated on commit.
- **Booleans and Enums:** Use **Toggle choice** to cycle sequentially or **Choices…** to pick from a centered dialog displaying translated display labels, copyable wire tokens, explanations, and deprecation notices (`booleanLabels` for booleans). If an invalid draft is typed, correct it before switching.
- **Explicit Omission:** Use **Omit value** to remove an answer completely. Empty strings (`""`) and admitted empty enum options remain explicit values; omission is an independent action.
- **Nullable values:** **Set null** supplies JSON null for a nullable location; it does not omit it. List-item null acts on the selected existing item.
- **Reset and suggestions:** **Reset value** removes overrides and explicit omission, allowing eligible initialization again. **Apply suggestion** deliberately adopts the declared suggestion; neither navigation nor language selection applies suggestions.
- **Initialization:** `x-configurator.initialValue` supplies eligible effective values without copying them into the supplied answer snapshot. Standard JSON Schema `default` is descriptive only. Requiredness does not create an absent parent container; explicit omission suppresses initialization until reset.

Protected secrets are masked on screen. Secret draft buffers exist only while focused; navigating away flushes the transient editing buffer.

For a field with an admitted local generation recipe, use **Generate** or
**Regenerate** for immediate defaults, or activate **Options…** when customizable
bounds are supported to open the centered secret-generator modal. Inside the modal,
adjust length or byte count, character-class toggles (`a-z`, `A-Z`, `0-9`, `!@#`),
and excluded characters while observing live entropy calculation and enforcement
of the `128`-bit minimum floor. Generating a preview keeps the candidate strictly
in a masked terminal buffer until you choose **Apply**; canceling, pressing `Esc`,
or closing zeroes all working buffers without modifying draft answers.
The definition controls the algorithm and policy; generation does not prove
that a remote service accepts the value, and field help displays shared policy
requirements and safe correction messages.

**Show/Hide** controls visibility separately from generation. **Copy secret**
deliberately transfers the value to the system clipboard; ordinary copy/cut
shortcuts remain blocked. If the clipboard is unavailable, the value remains
unchanged and the terminal reports the failure. It never exports secrets using
terminal escape sequences as a fallback. Clipboard history is outside the
workspace's control.
On Linux, copying requires an X11/XWayland desktop session (`DISPLAY`) and the
native clipboard capability. A headless terminal reports clipboard unavailable.
Reveal displays line breaks and other control characters as spaces. This is a
display safeguard; copying a generated/imported PEM key preserves its exact
multiline value. Ordinary clipboard paste into secret fields remains blocked.

Hash/digest recipes that require entered input open a separate masked prompt.
Recipes that generate an original password show it in a masked result dialog;
copy it deliberately before closing if it must be retained. Closing dismisses
that transient result. Changing the workspace while generation runs makes the
old result stale rather than applying it to a different field or row.

### Working with Objects and Lists

Structured fields require explicit lifecycle management:

Definitions may place a typed map or a discriminated branch at the configuration
root. Its controls appear as the root slot; entries and selected branch fields
follow it. The root cannot be omitted. **Reset value** clears its supplied
override and retires the previous child editors. Array rows use the collection's
remove action rather than field omission or reset.

- **Objects:** Use **Supply container** to include an object container, even if empty (`{}`). Browsing blank properties does not create the object. Use **Clear container** to explicitly omit it and re-evaluate field requirements.
- **Lists:** Navigate rows with **Previous item** and **Next/add item**. When list capacity allows, the slot past the last row becomes a new-item slot.
- **Adding Items:** Leaving a new-item slot empty skips adding it. Activating **Add item** commits the typed item, including empty strings.
- **Double-Press Delete:** To prevent accidental data loss, removing an item requires pressing **Remove item** twice. Navigating away or editing any field cancels the pending deletion.

Typed maps expose a masked key-entry control on the map container. Enter an
exact literal key and activate **Add map entry**, then navigate to its typed
value. Empty keys, dots, slashes and numeric-looking keys are data, not path
syntax; duplicate keys are not replaced. Entry labels use local ordinals rather
than exposing dynamic keys in public guidance or diagnostics. Omit an entry to
remove it. Additions beyond `maxProperties` or the engine ceiling are rejected
without changing answers, including missing containers created by nested edits.
Declared minimum counts and value constraints still govern readiness.

For a discriminated provider object, **Cycle variant** selects a declared branch.
Only its selected children are available for editing. Switching branches drops
the previous payload; the shared engine supplies only eligible initialization.
Choice labels and translated branch titles never determine typed answer identity.

### Guidance, language and corrections

Correction guidance uses the current shared field state and includes authored
limits and public choices in English or Arabic without repeating the entered
answer. Readonly or inactive fields do not offer null or suggestions; protected
fields and their containers withhold suggestions and choice disclosure.

Scroll inside the field help pane (`Up`, `Down`, `Page Up`, `Page Down`) to inspect
admitted descriptions, instructions, units, choice explanations, and static guidance links.
These are inert public text: no links are fetched or executed, and all guidance lines remain reachable in the supported minimum viewport without button pagination.

**Locale** cycles declared definition languages with base-text fallback. Language
changes public metadata, not answer values, schema identity, input revision or
prepared output. Shipped English/Arabic action resources follow process UI
culture separately. RTL metadata is right-aligned without reversing Latin
fallback labels or technical punctuation. Terminal rendering is not a claim of full bidirectional
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

### Comparing And Filling From Existing Files

Use an ordinary local `.json`, `.yaml`, or `.env` export under the selected
definition. Comparison is the default action; filling fields is deliberate.

1. Use **Cycle artifact** to select the target declared artifact.
2. Enter the path to your existing configuration file in **Import file**.
3. Activate **Compare file**. Comparison and deliberate adoption yield between
   bounded work batches so cancellation remains available. The busy state clears
   only after owned input and work finish cleanup. Comparison does not commit the active editor or change
   answers, valid/invalid pending text, origins, omissions or prepared output.
4. Use **Next finding** to inspect each retained finding. Severity text remains
   meaningful in monochrome. Declared schema titles explain the relevant schema;
   imported row numbers never select unrelated current rows or map entries.
5. Read coverage, inventory and autofill eligibility separately. Choose
   **Fill fields** only when you want atomic adoption.

```text
Local file -> bounded local read (4 MiB maximum) -> private assessment
                                               -> deliberate atomic Fill fields
```

Supported inverses recover represented values and proved presence information,
including supported nested copies and complete repetitions. Unrepresented work
remains unassessed; initialization does not conceal absent file members.
Comparison can explain unknown or obsolete input without printing private names
or values. It does not infer renamed settings or a historical source release.

Fill fields protects explicit answers, omissions and pending drafts; only untouched
initialization may be replaced. Unknown content, structural changes, invalid
values and conflicts refuse the entire adoption. If comparison completes but
autofill is unavailable, review and repair a separate copy and compare it again.
Missing required work can remain unfinished after otherwise admissible adoption.

The path field clears when reading begins, including its undo history, so Undo
cannot restore the consumed path. **Clear private state** also removes discarded
ordinary draft text from undo history. Comparison preserves the active editor's
current draft and editing history.
Edits, cancellation, artifact changes,
private clearing and disposal invalidate old work while its existing read owner
finishes physical cleanup. Reports are transient and private. Comparison never
overwrites the source or grants application apply/acceptance authority.
See [Import and compare](import-and-compare.md) for result and custody boundaries.

[Operations](../internal/OPERATIONS.md) records actual published-process
observations separately from native Core resource measurements and headless
checks. Those records do not qualify every host or assistive tool.

### Saving and Verifying Configuration

Once answers are completed, verify and export your configuration bundle:

1. **Validate Fields:** Activate **Validate fields** to check all field constraints and dependencies. Diagnostics display value-free codes and field paths.
2. **Select Artifacts:** Use **Cycle artifact** to review declared filenames, formats, and restart instructions. Toggle individual outputs with **Toggle output**. At least one artifact must be selected.
3. **Prepare Artifact:** Activate **Prepare artifact** to stage the output bundle. The TUI validates complete composition and calculates cryptographic digests without displaying secret bytes.
4. **Save Protected:** Activate **Save protected** to create files in the current working directory.

Cancellation or edits can retire preparation without making the answers
invalid. Wait for active work to settle, review the current answers and prepare
again. A preparation failure is separate from a validation error and does not
identify a field that needs correction.
Preparation runs outside the interface owner; the terminal loop remains
available for cancellation and feedback while that work runs.

> [!IMPORTANT]
> **Create-Only Protection (Linux x64):**
> On Linux, Configurator writes files using a strict create-only native writer with owner-only (`0600`) permissions. Existing files are never overwritten. If an output file already exists, resolve the conflict or choose a different directory before retrying.

Outputs distinguish between two independent milestones:
- `installation-established`: The file was securely written to the publication path.
- `verification-failed`: An immediate post-write integrity check failed.

Configurator never deletes or rolls back previously installed files. If a later
file in a bundle fails, review each recorded outcome and inspect destinations
manually. Immediate verification does not guarantee a file's current integrity.
Select only failed or unattempted outputs, resolve the conflict and prepare again
using fresh destinations.

Use **Save history** to cycle through the 16 most recent save attempts to inspect artifact outcomes and error codes.
The history and outcome actions stay in place and are disabled until the
corresponding receipt facts are available.
Changing the interface language preserves the caret in an unchanged public
value.

---

## Session Termination & Privacy

To protect sensitive configuration data:
- **Clear Private State:** Removes all entered secrets and invalidates prepared bundles while preserving non-sensitive public settings.
- **Closing the Session:** Exiting or sending termination signals (`SIGINT`,
  `SIGTERM`) cancels application-owned operations, clears the owned secret-edit
  buffer and retires private references. Physical operations retain their
  buffers until settlement; managed strings cannot be universally erased.
- **Graceful Shutdown:** Configurator waits up to two seconds for active disk operations to finish before exiting. If an operation cannot be verified before exit, status indicates an unknown outcome so you can inspect destination files manually.

Deterministic adapter/control tests and actual interactive process qualification
are separate evidence. A prepared configuration does not prove target acceptance,
grant apply permission or qualify a new operating-system host.
