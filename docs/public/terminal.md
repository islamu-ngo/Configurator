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

Run interactively without redirecting stdin, stdout or stderr; redirected startup
is refused. `--help` can still be printed noninteractively. Use the [CLI](cli.md)
for pipes, scripts and machine-readable output.
Use a terminal of at least 80 columns and 26 rows. Tab and Shift+Tab move focus;
Enter activates buttons. Underlined button shortcuts are available. English and
Arabic resources follow the process UI culture.

1. Navigate fields with Previous/Next; use Apply field to commit the current
   input. Strings, booleans, integers, decimal numbers and declared enum choices
   retain their types. Cycle choice advances from the value currently visible;
   correct an invalid draft before cycling. Object and list containers have
   Supply container and Clear container actions. Supply deliberately includes
   the container, including an empty object or list; Clear omits it and checks
   requiredness again. Browsing blank object properties leaves an omitted object
   omitted. Object properties remain separate editable fields.
   Lists use Previous item, Next/add item, Add item and Remove item. When capacity
   remains, the position after the last item is the new-item position. Leave an
   empty new-item draft to skip adding it, including for boolean and numeric
   lists. At the item limit, a notice explains the bound and existing items stay
   editable; field navigation remains available even when no items are allowed.
   To delete an existing item, press Remove item twice. Navigating or editing
   cancels the confirmation. Selection then moves to the next item, the previous
   item when deleting the last row, or the empty container.
2. Disclosure cycles Guided, Advanced and All declared. Activation and validation
   stay governed by Core; hiding a field never relaxes readiness.
3. To prefill supported answers, cycle to the matching declared artifact, enter
   the path to an existing local file in Import file, and press Import selected
   artifact. Directly copied JSON, YAML and dotenv object fields are supported;
   transformed or ambiguous mappings and conflicting existing answers are
   rejected without changing the form. The path field clears when reading starts.
   Reading leaves the terminal responsive. Editing an answer, changing the import
   path or artifact target, or clearing private state cancels that import; late
   results cannot replace your newer work. A second import, preparation or save
   waits until the existing read finishes cleanup. Review imported answers in the
   masked editor before continuing.
4. Validate fields reports value-free codes and field identifiers. Cycle artifact
   shows its declared filename, format, apply semantics and restart guidance.
   Inspecting another artifact preserves preparation; changing the selected
   output set requires preparing again.
   The first output starts selected. Toggle output selects or deselects the
   current artifact; the label shows how many declared outputs are selected.
   At least one output must be selected. Prepare artifact validates the current
   answer revision and selected bundle without displaying its contents. No
   legal-validity or target acceptance claim is made.
5. Save protected creates selected outputs under their declared filenames in the
   working directory. Existing files are never overwritten. All answer-derived
   output is private, even when its fields are labelled public. Protected saving
   uses the Linux writer, with actual-host evidence on Linux x64 only; Arm/musl
   qualification remains pending and macOS/Windows protected saves fail closed.
   The result counts established installations, separately from verification.
   Tab to the read-only status field and use Left/Right or Home/End to inspect
   its full text. For multiple outputs, use Next output result to review each
   artifact ID and outcome. `installation-established` means the file was
   installed at the publication point; `verification-failed` means its immediate
   post-installation check did not succeed. Neither successful verification nor
   installation guarantees the destination's current integrity. If a later file
   fails,
   earlier created files remain: review them in the working directory and decide
   how to clean up or retry. Configurator does not delete or overwrite them.
   Save history cycles through the newest 16 attempts, including cancelled or
   failed saves. Each attempt shows its own artifact outcomes. Editing or Clear
   private state keeps this history for the session without restoring answers or
   readiness; closing the session discards it. A result still marked unknown
   must not be treated as proof that no file was created. Cleanup or observation
   failure is reported alongside known file outcomes, including installed but
   unverified files. Do not assume such a file is absent or safe to use: review
   it manually before retrying. A verification failure stops later outputs;
   cleanup failure never changes an established installation into "not written".
   While a save is still finishing, another import, preparation or save waits
   for its cleanup.

Editing a field or changing output selection invalidates readiness immediately.
Field navigation clears
the transient editor buffer; protected answers remain in the masked form so
multiple fields can be completed. Clear private state removes all protected
answers and readiness while keeping public fields. Close and handled termination
signals cancel pending work and clear private state. Closing waits up to two
seconds for cleanup while the terminal continues processing events, then exits.
If work remains unresolved, the closing status says its outcome is unknown;
created files may remain and must be reviewed before retrying. Cancellation does
not undo a file already created. Disposal detaches UI callbacks; read cleanup can
finish while the process remains alive. After a save, prepare again to save
another bundle.

Private fields block clipboard, context-menu and undo/redo operations. No answers
are accepted on the command line, printed to stdout or repeated in diagnostics.
`--help` prints only public usage. Screen-reader, braille, RTL shaping, terminal
scrollback and erasure of immutable .NET strings after Core handoff are not
verified guarantees.
