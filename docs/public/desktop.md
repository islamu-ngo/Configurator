# Desktop configuration preparation

Configurator Desktop is an offline Avalonia application. It reads an explicitly
selected local JSON or YAML setup definition and creates new protected files.
There is no built-in target, network connection, plugin execution, or apply step.

## Start and load

Run from the Configurator checkout:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --project src/Configurator.Desktop/Configurator.Desktop.csproj --configuration Release -- /absolute/path/setup.json /absolute/path/output-directory
```

Both arguments are optional. With no arguments, enter a local definition path
and choose **Load definition**. With one argument, the definition loads on opening;
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
unset option. Object fields expose their named child controls, and scalar lists
have **Add item** and **Remove item** actions. You do not need to edit JSON.
Declared defaults are loaded, and public recommendations can be selected
explicitly. Protected inputs are masked, including descendants of an object
whose nested fields raise its sensitivity.

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

## Save safely

Choose an existing output directory and a new file name. File names admit ASCII
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

## Keyboard

Tab and Shift+Tab traverse native controls in form order. Button access keys
follow the underlined labels. Ctrl+O focuses the definition path, Ctrl+Enter
prepares the artifact, and Escape cancels/discards. Loading focuses the first
visible field; validation focuses the first invalid one; adding a list item
focuses its editor, and removing it returns focus to **Add item**.
The right-to-left switch changes layout direction without changing answers.
