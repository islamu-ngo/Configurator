# Local terminal preparation

The terminal uses an explicitly selected local JSON or YAML definition. It has
no built-in Event target, network API, sign-in or remote apply operation.

Local files may contain a compact maintainer source with
`$schema: urn:islamu:configurator:authoring:v1` or an expanded runtime definition.
The file extension selects JSON or YAML syntax. The shared engine compiles and
admits compact sources before any answers are collected, producing the same
canonical identity as the standalone CLI. No definition store is required.
See the [definition guide](definitions.md) for source authoring.

```bash
dotnet run --project src/Configurator.Terminal/Configurator.Terminal.csproj --configuration Release -- --definition examples/minimal-self-hosted/setup-definition.json
dotnet run --project src/Configurator.Terminal/Configurator.Terminal.csproj --configuration Release -- --registry /path/to/definitions --family 69cdb122-41d2-4fab-9b82-7929a889c726 --app-version 1.0
```

Use your actual definition path. Registry directories contain local `.json`,
`.yaml` or `.yml` definitions. A family with several revisions needs an explicit
compatible application version; unversioned ambiguity fails closed. Publisher
and family metadata are identifiers, not proof of trust. The definition is frozen
for the session; close and relaunch to select another.

Use a terminal of at least 80 columns and 26 rows. Tab and Shift+Tab move focus;
Enter activates buttons. Underlined button shortcuts are available. English and
Arabic resources follow the process UI culture.

1. Navigate fields with Previous/Next; use Apply field to commit the current
   input. Strings, booleans, integers, decimal numbers and declared enum choices
   retain their types. Cycle choice selects a boolean or enum choice.
   Object properties are separate fields. Lists use Previous item, Next/add item
   and Remove item; the position after the last item is the new-item position.
2. Disclosure cycles Guided, Advanced and All declared. Activation and validation
   stay governed by Core; hiding a field never relaxes readiness.
3. To prefill supported answers, cycle to the matching declared artifact, enter
   the path to an existing local file in Import file, and press Import selected
   artifact. Directly copied JSON, YAML and dotenv object fields are supported;
   transformed or ambiguous mappings and conflicting existing answers are
   rejected without changing the form. The path field clears after the attempt.
   Review imported answers in the masked editor before continuing.
4. Validate fields reports value-free codes and field identifiers. Cycle artifact
   shows its declared filename, format, apply semantics and restart guidance.
   The first output starts selected. Toggle output selects or deselects the
   current artifact; the label shows how many declared outputs are selected.
   At least one output must be selected. Prepare artifact validates the current
   answer revision and selected bundle without displaying its contents. No
   legal-validity or target acceptance claim is made.
5. Save protected creates selected outputs under their declared filenames in the
   working directory. Existing files are never overwritten. All answer-derived
   output is private, even when its fields are labelled public. Protected saving
   currently requires Linux's verified writer; unsupported hosts fail closed.
   The result shows how many files were created. For multiple outputs, use Next
   output result to review each artifact ID and status. If a later file fails,
   earlier created files remain: review them in the working directory and decide
   how to clean up or retry. Configurator does not delete or overwrite them.

Editing a field or changing output selection invalidates readiness immediately.
Field navigation clears
the transient editor buffer; protected answers remain in the masked form so
multiple fields can be completed. Clear private state removes all protected
answers and readiness while keeping public fields. Close, disposal and
handled termination signals clear private state and cancel pending saves before
commit. After a save, prepare again to save another bundle.

Private fields block clipboard, context-menu and undo/redo operations. No answers
are accepted on the command line, printed to stdout or repeated in diagnostics.
`--help` prints only public usage. Screen-reader, braille, RTL shaping, terminal
scrollback and erasure of immutable .NET strings after Core handoff are not
verified guarantees.
