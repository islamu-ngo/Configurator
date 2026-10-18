# Desktop configuration preparation

Configurator Desktop is an offline Avalonia application. It reads an explicitly
selected local JSON or YAML setup definition and creates new protected files.
There is no built-in target, network connection, plugin execution, or apply step.

Local `.json`, `.yaml` and `.yml` files can contain either a compact maintainer
source (`$schema: urn:islamu:configurator:authoring:v1`) or an expanded runtime
definition. The extension selects syntax; the shared engine compiles and admits
the source before creating the form. The canonical identity matches the CLI and
Terminal without requiring a definition store.
See the [definition guide](definitions.md) for source authoring.

## Start and load

For x86_64 Arch Linux, use the native
[pacman development package](packaging.md#arch-linux-development-package).
It provides the `islamu-configurator-desktop` command and desktop menu entry in
X11 and Wayland/XWayland sessions without an external .NET runtime.

Run from the Configurator checkout:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --project src/Configurator.Desktop/Configurator.Desktop.csproj --configuration Release -- /absolute/path/setup.json /absolute/path/output-directory
```

Both arguments are optional. With no arguments, use **Choose definition…** to
select and load a local file, or enter its path and choose **Load definition**.
With one argument, the definition loads on opening;
the initial output directory is the current directory. The second argument
selects an existing output directory. Do not supply credentials as arguments.

The loaded title, application, publisher, family, revision, and computed SHA-256
pin appear above the form. Publisher metadata does not prove trust. Changing the
source file does not silently change the admitted definition: explicitly reload
to adopt a new revision. A failed reload clears the previous form and preparation.
See the [definition contract](../internal/CORE_DEFINITION_CONTRACT.md) for the
bounded data format.

## Edit and check

String and numeric fields have ordinary input boxes; numbers use invariant
decimal notation. Boolean and enum fields have selection controls, including an
unset option. Object fields expose their named child controls. Scalar and object
lists have **Add item** and **Remove item** actions; object rows expose their
declared child fields. You do not need to edit JSON.
Declared defaults are loaded, and public recommendations can be selected
explicitly. Protected inputs are masked, including descendants of an object
whose nested fields raise its sensitivity. Protected boolean and enum values
use masked entry instead of a popup that could disclose their choices.
Protected booleans accept `true` or `false`; protected enums accept their
declared value. A blank entry is unset. The workspace scrolls vertically so
fields, artifact actions and status remain reachable in a shorter window.

**Essential** shows active required, invalid, protected, consequential, and
essentially disclosed fields. **Guided** adds ordinarily disclosed fields.
**Advanced** shows every active field. Switching modes never changes validation,
answers, or readiness, and inactive fields do not participate in output.
Help, grouping, type constraints, and declared consequences accompany the inputs.

Choose **Validate** to check the current answers and focus the first invalid
visible field. Choose **Prepare artifact** after selecting any declared artifact
ID. JSON, YAML, dotenv, and Markdown are all supported through the same workflow.
The artifact format, apply semantics, and restart consequence are shown; these are
metadata about the eventual consumer, not permission to modify a running system.
Preparation shows only metadata and a digest, not a preview of protected bytes.

To reuse supported values, select a declared JSON, YAML, or dotenv artifact,
use **Choose import…** to select and import a local file, or enter its path and
choose **Import existing file**.
The file must match a simple declared field mapping and current answers; an
unknown key, conflicting answer, invalid field, or edited form rejects the
whole import. Markdown and complex projections cannot be imported. A successful
import refreshes the form and focuses the first visible field. Private values
remain local and do not appear in operation status.

For multiple outputs, check the declared files under **Bundle outputs**, choose
**Prepare bundle**, then **Save protected bundle**. The selection and current
answers are checked for complete composition before saving becomes available.
An empty or inactive selection must be corrected and prepared again; conflicting
declared file names reject definition admission. Editing an answer or destination discards
it. The capability notice shows whether this host can save protected files.

## Save safely

Use **Choose folder…** to select an existing output directory, or enter its path,
then choose a new file name. Cancelling or an unavailable native picker preserves
answers and prepared output. Remote locations are refused; typed local paths
remain available. File names admit ASCII
letters, digits, dots, underscores, and hyphens, up to 128 characters; path
separators, parent-directory paths, and standard-output targets are rejected.
Choose **Save new protected file**. Linux uses the shared create-only writer,
which publishes owner-read/write-only files and refuses existing targets,
unsafe targets, or changed filesystem authority. There is no overwrite action.
Unsupported platforms cannot save protected output and have no ordinary-file,
clipboard, console, or public-download fallback.

Every answer-derived artifact is restricted, even when all inputs are public.
Saving consumes the preparation on success, rejection, or cancellation. Edit an
answer, output destination, file name, definition path, or artifact selection,
and the preparation is discarded. Prepare again before another save.
**Cancel / discard preparation** cancels pending work and drops prepared output;
a late completion cannot restore it.

An installed file and a successfully verified file are different outcomes.
`save-installed-unverified` means installation happened but the immediate
post-installation check failed; do not treat it as "nothing written" or assume
the file is safe to use. `cleanup-failed` is independent and cannot undo an
installation. Review these files manually before retrying. Successful
verification is historical evidence, not a guarantee of current integrity.

A bundle checks every destination before writing any file. If a destination
already exists, nothing is overwritten: resolve the conflict and prepare again.
If a later write or verification fails, status lists the declared files already
installed, their verification and cleanup outcomes, and the unattempted files.
Review installed-but-unverified files before using them or choosing a recovery.
Keep verified files; select only the remaining
outputs and prepare again. Configurator does not roll back committed files or
apply them to the target application.

Cancellation or edits still fence stale status updates and discard preparation.
They do not retract an established installation. Desktop's separate in-flight
buffer ownership and close/drain recovery work is not qualified by these result
semantics.

## Keyboard

Tab and Shift+Tab traverse native controls in form order. Button access keys
follow the underlined labels. Ctrl+O focuses the definition path, Ctrl+Enter
prepares the artifact, and Escape cancels/discards. Loading focuses the first
visible field; validation focuses the first invalid one; adding a list item
focuses its editor, and removing it returns focus to **Add item**.
The right-to-left switch changes layout direction without changing answers.
Native selection returns focus to its button on cancellation or failure;
successful loading/import focuses the first visible field. Status includes a
value-free next step. Closing discards preparation and fences pending work.

## Current qualification limits

Linux x64 has verified native control and protected-write checks. Windows and
macOS protected saving and installers remain planned. Native harness checks do
not establish screen-reader compliance, every desktop environment, installed
packages or the complete published-app workflow. Those independent qualification
gates must pass before packaging/support claims expand.
