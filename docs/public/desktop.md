# Desktop Configuration App

Configurator Desktop is an offline graphical application built with Avalonia UI.
It guides operators through local configuration and prepares candidate files
(`.env`, JSON, YAML) according to an admitted definition. The target application
independently validates, authorizes and applies them.

Use the [support index](../internal/STATUS.md) to distinguish packaged targets
from actual-host and protected-save evidence.

Everything runs locally on your workstation. The desktop client has zero
background telemetry, requires no target application credentials, and performs
outbound HTTPS requests only when you explicitly enter a public HTTPS
definition URL and choose **Fetch** on the **Open** screen.

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
- **No arguments:** **Open** organizes definition intake into three tiers:
  1. **Catalog (Offline Bundled Store):** Filter the built-in public catalog by name, description, or tags and select **Configure** to load a vetted definition immediately offline.
  2. **Local File:** Click **Choose definition…** to open Avalonia's managed cross-platform file picker, or enter a local `.json` / `.yaml` path and click **Load definition** (4 MiB limit).
  3. **Public HTTPS Link:** Paste a credential-free public `https://` URL (no query, fragment, userinfo, redirects, localhost, or private IP targets) and click **Fetch** to admit a definition without disturbing active workspace answers.
- **One argument:** Automatically loads the specified local definition file on launch.
- **Two arguments:** Loads the local definition and sets the target output directory.

Open displays the loaded definition's title, application ID, publisher, family
UUID, revision and SHA-256 digest pin. Changing the file on disk does not alter
the running workspace; load it again to explicitly admit an updated revision.

Dismissing a picker preserves your current answers and preparation. Unavailable
pickers and remote locations produce separate guidance. A selection that finishes
after cancellation or workspace replacement cannot change the new workspace.

---

## Desktop GUI Layout

The workspace has four top-level stages accessible from the fixed header bar:
**Open** admits a definition from the bundled catalog, local file, or public HTTPS link.
**Configure** shows its authored sections and the current section's editors.
**Review outputs** contains preparation and deliberate protected saving.
**Settings** provides a dedicated left-sidebar workspace organized into sections
(**Appearance**, **Language & layout**, **Keyboard & dialogs**, and
**Privacy & host output**) for session display preferences, keyboard shortcuts,
and local host output guarantees.
Compact **Appearance**, **Language**, and **Direction** selectors also remain
directly accessible in the top header without an extra dropdown panel. The top
header stays fixed while only the active stage body scrolls.
Changing stages does not collect another copy of your answers or discard
unfinished numeric text. Validation and missing counts cover the whole active
configuration, including fields on other pages.

Sections with more than 50 root editors have explicit Previous/Next pages.
Paging changes the displayed editors while the workspace retains their drafts
and identities. Definitions whose root is a map or a variant have a neutral
Configure section so their recursive editor remains reachable.

Small windows use a section picker. Wider workspaces show a fixed left section
rail that scrolls independently when a definition has more sections than fit in
the viewport, while the editing body scrolls separately within an 800-unit
reading width. Resizing rearranges the existing editing surface rather than
replacing an in-progress text input.

Appearance offers **System**, **Light** (Latte), and **Dark** (Macchiato).
The preference lasts for this application session; restarting uses System.
Native high contrast takes priority, and turning it off restores your chosen
ordinary appearance. Appearance changes do not alter answers or preparation.

---

## Keyboard Shortcuts

Navigate quickly using native keyboard controls:

| Shortcut | Action |
| --- | --- |
| `Tab` | Move focus forward to the next field, control, or button |
| `Shift + Tab` | Move focus backward to the previous field, control, or button |
| `Ctrl + O` | Focus the definition file path input |
| `Ctrl + Enter` | Open Review and prepare the selected outputs; saving remains separate |
| `Escape` | Dismiss the active popup first; otherwise cancel work or discard preparation |
| `Alt + <Letter>` | Activate button matching the underlined access key |
| Direction controls | Follow the definition locale's direction, or explicitly override left-to-right/right-to-left layout |

On macOS the application uses the platform's Command modifier for Open and
Prepare. Focused editors and native popups receive keyboard input first.
Preparation shortcuts never save files.

---

## Configuration & Form Editing

### Deployment recipe and component scope

Definitions that declare deployment choices show a **Deployment recipe and
component scope** picker before the fields. Each choice names a compatible
recipe and scope; selecting it changes both answers together. Event starts
with Standalone and all applicable components. Separate images and Compose
can offer API-only or Blazor-only configuration.

Readiness and available output destinations follow the selected scope. Choosing
a subset of files within an incomplete all-component scope does not bypass its
remaining requirements. Changing scope keeps inactive answers in the local
workspace for switching back, removes ineligible destinations and retires
prepared output and pending imports. Prepare again after the change. A completed
native save retains its publication receipt; it cannot be recalled by changing
scope. Component configuration does not establish a complete deployment or
target acceptance.

### Form Controls

Fields dynamically render native controls matching their admitted types:
- **Strings and Numbers:** Ordinary text inputs validating invariant decimal formatting.
- **Booleans:** A choice control distinguishes **Not supplied**, **Yes**, and **No**. Explicit null remains a separate action when the definition permits it.
- **Enums:** Dropdown choices retain their declared ordinal and exact JSON type/value. Labels are display metadata, not identity: two choices with the same localized label remain distinct, and an empty-string choice is not **Not supplied**.
- **Protected Secrets:** Password-masked controls. Protected booleans and enums use masked inputs instead of open dropdowns to avoid leaking values.

### Find settings and correct errors

**Find settings** searches admitted titles, descriptions, schema metadata and
public facets. It never indexes answers, secrets, private map entry names or
prepared file content. Queries accept at most 256 characters and 16 terms;
limit feedback leaves the accepted search unchanged. Results show a total,
active/inactive status and pages of 25. Include inactive settings explicitly
when needed. Selecting an inactive result explains its applicability without
changing its controlling answers.

Search navigation does not supply an absent container or choose a private
collection entry. A correction from validation uses its current revision and
editor identity to reveal the section, root page and collection ancestors
before requesting focus. A retired correction cannot target a replacement row.

Untouched required fields show their required status and contribute to global
missing counts. Inline correction guidance appears after an edit is blurred
or after validation/preparation is requested. These display choices never
weaken readiness or remove invalid drafts.

Lists and maps display at most 25 immediate rows per page. Nested collections
stay closed until opened; changing pages preserves surviving row identities
and unfinished numeric text. Secondary omission, suggestion, reset, null and
removal actions live in each field's **Actions** menu. **Supply** remains a
distinct deliberate operation for absent containers.

### Secret Generation and Deliberate Copy

PEM private-key fields accept multiple lines, preserving the original key envelope
when pasted or copied. Visibility remains a deliberate Show/Hide action.

For a field with an admitted local generation recipe, choose **Generate** to
create a value using its declared policy. **Regenerate** deliberately replaces
the current value. Existing or issuer-supplied credentials remain manual input;
Configurator does not contact the issuer or prove that a target accepts a key.

Secret fields start masked. **Show/Hide** changes visibility, and editing or
closing the workspace hides the value again. **Copy value** deliberately sends
the value to the native clipboard. A clipboard failure leaves the answer
intact; clipboard history is outside the workspace's custody.

Hash and digest recipes that require input provide a separate masked input.
When a recipe generates an original password, its transient result has separate
Show/Hide and Copy actions, distinct from copying the final hash. Save or copy
that original credential deliberately before dismissing the result; it is not
silently included in ordinary output artifacts. Inline rule feedback explains
corrections without echoing a secret, and invalid drafts remain editable.

### Presence, Initialization and Suggestions

Available actions follow the current field state. Readonly or inactive fields
do not offer null or suggestions. Ordinary correction guidance includes the
definition's limits and public choices in the selected language, without
repeating the entered answer. Protected fields and their containers withhold
suggestions and choice disclosure.

These actions have different effects:

- **Set null:** Available for nullable fields; supplies a present JSON `null`, not an absent value.
- **Clear / Not supplied:** Explicitly omits the setting and suppresses its `initialValue`. Empty strings (`""`), false, zero and empty containers are supplied values, not omission. For a string list item, **Clear** retains an empty-string row; use **Remove item** to delete it.
- **Reset:** Removes the supplied override and explicit omission so eligible `x-configurator.initialValue` initialization can apply again. It does not promise a value where no initialization is declared.
- **Apply suggestion:** Deliberately adopts `x-configurator.suggestedValue`; a suggestion never fills answers automatically. Suggestion actions are withheld for protected fields or containers with protected descendants.

The configuration root is always present, so its **Clear** action is disabled.
Root maps and root branches use the same typed controls described below.
Array rows use **Remove item** rather than omission or field reset.

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
It also preserves the selected experience. File paths and filenames keep their
left-to-right order within right-to-left layouts.
The same workspace selection refreshes provider action labels, window controls,
field guidance and import review text in English or Arabic. Other provider
languages fall back to English without changing the computer's language settings.

The window starts with System appearance; Light and Dark are session choices.
Native high contrast takes precedence over either ordinary choice.
Validation errors retain visible text as well as a distinct color and weight.
Appearance, language and direction are under **Display settings**, available
across stages. Experience and optional-detail controls belong to Configure.

**Follow locale direction** is enabled initially. Changing **Right-to-left
layout** selects an explicit direction override; enable follow-direction again
to resume the locale's direction. Neither direction control changes language.
Definition metadata uses its admitted translations. Shipped controls use the
workspace's English/Arabic resources, falling back to English for other
definition languages. Before a workspace is loaded, the process UI culture
selects the initial shipped text. This is not complete translation coverage.

### Validation and Preparation

In **Review outputs**:

1. Review whole-configuration readiness. **Validate** reveals current correction
   guidance and opens the affected section/page before requesting focus.
2. Select the eligible declared outputs in **Outputs to prepare**. The first
   eligible output starts selected. With none selected, preparation is disabled.
3. Choose an existing destination directory. One selected output can use a
   filename override; multiple outputs use their declared filenames.
4. **Prepare selected outputs** validates and prepares the current selection.
   Preparation does not write files, overwrite anything or apply configuration.
5. Inspect readiness and consequences, then deliberately **Save prepared outputs**.
   Saving consumes that preparation. Selection, destination, filename or answer
   changes require a fresh preparation.

Cancellation or an edit can retire preparation without making the answers
invalid. Wait for the active operation to settle, review the current answers and
prepare again. A preparation failure is reported separately from validation
errors; it does not identify a field that needs correction.

### Importing Existing Files

Review existing `.json`, `.yaml`, or `.env` files against the selected definition:

1. Expand **Compare or fill from an existing file** and select its **Source output format** independently of the export checkboxes. A filename does not establish its scope or provenance.
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
`ReadImportAsync(CancellationToken)` returning `Task<StorageSelection<BoundedFileInput>>`.
Open the selected provider's stream rather than translating its URI into a
native path. Bound acquisition to 4 MiB and finish provider stream/item cleanup
before returning the caller-owned input; clear acquired input on failure or
cancellation. The Desktop caller clears/disposes returned input after review.
This mandatory method intentionally breaks older implementations; there is no
default fallback. Definition and output selection still require local paths and
return `StorageSelection<Uri>`. Report dismissed, unavailable and nonlocal
selections through `StorageSelectionStatus`; only `Selected` transfers a value.
Token cancellation throws `OperationCanceledException`. Do not encode outcomes
in exception messages.

---

## Protected Saving

### Linux Protected Save Contract

> [!IMPORTANT]
> **Linux Protected Save Contract (`0600`):**
> On Linux x64, Configurator writes configuration files using a native create-only writer with owner-only (`0600`) permissions. Existing files are never overwritten. Protected saving fails closed on unsupported platforms without plain-file fallback.

To publish files safely:
1. Choose an existing directory and prepare the selected outputs. For one file,
   an optional filename override accepts up to 128 letters, numbers, dots,
   dashes or underscores; multiple outputs retain their declared filenames.
2. Deliberately click **Save prepared outputs**.
3. The writer verifies that no target files exist before committing any file to disk.

### Understanding Publication Outcomes

The desktop client separates installation from verification:
- `installation-established`: The file was successfully written with restricted permissions.
- `save-installed-unverified`: The file was created, but immediate post-installation verification failed. Inspect the file manually before using it.
- `cleanup-failed`: Temporary writer resources could not be cleanly released. Review installation and verification facts separately.

If a later file in a bundle fails, Configurator does not roll back earlier
installations. Immediate verification records what was checked during that
attempt; it does not guarantee the file's current integrity. Review
**Publication facts** and inspect installed or unverified destinations manually.
Select only failed or unattempted outputs, resolve the conflict, then prepare
again using fresh destinations.

---

## Managed Local Profile Store & Protocol Handshake

> [!NOTE]
> **Planned feature:** Managed encrypted local profile storage, `configurator://` deep-link matching, and direct PaaS/secret-manager delta sync are on the future roadmap ([proposed design decision](../internal/adr/ADR-encrypted-local-profiles-and-target-apply.md)). Current Desktop builds support stateless configuration editing, comparing ordinary files, and create-only Linux protected output.

Configurator Desktop is designed to serve as an operator's local command center for managing applications across multiple deployment environments:

1. **Protocol Handshake (`configurator://`):**
   * Registered as a native desktop URI scheme on Linux, macOS, and Windows.
   * When operators click "Configure Visually" or release notes upgrade links, the desktop application can open directly, bypassing browser download steps.
2. **Encrypted Local Profile Store (`~/.config/islamu-configurator/profiles/<app-id>/<env>/`):**
   * Stores **immutable per-revision encrypted snapshots** per application and deployment environment (`development`, `staging`, `production`) so previous working revisions remain intact for Day-2 rollback.
   * Planned persistence tiers match your security preference: **Compare-only** (stores no secret values at all—only non-secret settings and policy metadata for schema-upgrade diffing), **Import for upgrade only** (encrypts secrets on disk and carries them forward silently into new versions without revealing plaintext), **Inspectable encrypted** (encrypted on disk, viewable inside the unlocked Desktop app), alongside existing **Unencrypted export**.
   * Convenience-first unlocking uses your OS keyring automatically on your machine, paired with an optional passphrase backup slot for portability.
3. **Native Secret Manager & PaaS Sync (Coolify, Infisical & Azure Key Vault):**
   * During the **Review outputs** stage, Desktop will provide optional authenticated export and upgrade-delta sinks to push configured settings directly into self-hosted **Coolify** instances or target vaults (**Infisical**, **Azure Key Vault**).
   * By default, zero network requests occur; vault or PaaS synchronization happens solely on explicit operator confirmation.

For the underlying architecture and sovereign infrastructure vision, see [Product Vision](../internal/PRODUCT_VISION.md) and [Marketing & Adoption Strategy](../internal/MARKETING_AND_ADOPTION_STRATEGY.md).

---

## Window Closure & Pending Tasks

Closing the window while background reads or writes are active triggers cooperative cancellation:
- **Automatic Settlement:** The window waits up to two seconds for active preparation, reads or writes to settle. It closes normally if that work finishes while the close request is active.
- **Stay Open:** If work remains after the deadline, choose **Stay open** to keep the window accessible while it settles. This does not undo cancellation or release the active operation.
- **Force Close:** After the deadline, choose **Force close** to close without waiting for the unknown outcome. Forced closure cannot guarantee that no file was written, roll back installation or erase managed memory. Inspect destination files manually.

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
