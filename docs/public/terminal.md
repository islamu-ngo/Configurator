# Local terminal preparation

The terminal uses an explicitly selected local JSON or YAML definition. It has
no built-in Event target, network API, sign-in or remote apply operation.

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
3. Validate fields reports value-free codes and field identifiers. Select the
   desired artifact with Cycle artifact; its format, apply semantics and restart
   metadata are displayed.
4. Prepare artifact validates a captured input revision and composes the selected
   artifact without displaying its content. JSON, YAML, dotenv, Markdown legal
   drafts and identity JSON use the same flow. No legal-validity claim is made.
5. Set a simple filename and Save protected. Output is created in the working
   directory, never overwritten. All answer-derived output is private, even when
   the definition labels the fields public. Saving currently requires Linux's
   protected writer; unsupported platforms do not fall back to ordinary writes.

Editing a field invalidates prepared output immediately. Field navigation clears
the transient editor buffer; protected answers remain in the masked form so
multiple fields can be completed. Clear private state removes all protected
answers and prepared bytes while keeping public fields. Close, disposal and
handled termination signals clear private state and cancel pending saves before
commit. After a save, prepare again to save another artifact.

Private fields block clipboard, context-menu and undo/redo operations. No answers
are accepted on the command line, printed to stdout or repeated in diagnostics.
`--help` prints only public usage. Screen-reader, braille, RTL shaping, terminal
scrollback and erasure of immutable .NET strings after Core handoff are not
verified guarantees.
