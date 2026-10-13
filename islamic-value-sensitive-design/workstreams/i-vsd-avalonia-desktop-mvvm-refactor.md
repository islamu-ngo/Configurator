# Avalonia Desktop Modern MVVM Refactor — I-VSD Planning Review

Last Updated: 2026-10-11

## Review Metadata

- Mode: planning
- Subject: Modernizing the offline Avalonia Desktop client architecture to canonical .NET MVVM standards (DI, ViewModels, RelayCommands, DataTemplates, zero code-behind)
- Workstream: avalonia-desktop-mvvm-refactor
- Report kind: provider-responsibility design review
- Report revision: 1
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-11
- Reviewed input: Working-tree source in `src/Configurator.Desktop/`, `tests/Configurator.Desktop.Tests/`, reference architecture in `AAD.QuizTeam`, and intent `client-interface`
- Supersedes: none

## Executive Summary

The Avalonia Desktop client (`Configurator.Desktop`) provides an offline graphical interface for operators preparing sensitive configuration definitions and writing protected, owner-restricted native artifacts. While its security primitives (memory zeroing, create-only Linux permissions, generation fencing) are sound, its UI layer currently relies on heavy imperative code-behind (248 lines in `MainWindow.axaml.cs`), a monolithic 569-line ViewModel (`SetupDesktopShellViewModel`), procedural control generation (`FieldEditor.cs`), and event-handler-driven actions rather than declarative MVVM bindings.

This refactoring workstream modernizes the desktop application to the gold standard of .NET desktop MVVM architecture—demonstrated in reference applications like `AAD.QuizTeam` and adapted for Avalonia 12.x. It introduces `Microsoft.Extensions.DependencyInjection`, decomposes the monolithic ViewModel into single-responsibility components communicating via decoupled messaging (`IMessenger`), replaces procedural DOM construction with a typed `FieldViewModel` hierarchy and declarative XAML `DataTemplate`s, encapsulates native file dialogs behind a testable `IStorageDialogService`, and eliminates procedural code-behind.

Because Configurator is in active pre-release development, this is a clean break-and-replace refactor with zero backward-compatibility baggage.

All provider-responsibility obligations—responsible custody (*Amanah*), non-harm (*La Darar*), transparency, and security invariants—are preserved:
1. Memory zeroing of sensitive buffers is strictly maintained.
2. Protected create-only save semantics are never bypassed or degraded to ordinary file writes.
3. Generation fencing is preserved across asynchronous dialog and preparation boundaries.
4. Fail-closed validation prevents premature artifact generation.

This disposition is **current / plan-aligned**.

## Scope

Active scope:
- `src/Configurator.Desktop`: Architecture, Dependency Injection setup, ViewModel decomposition, RelayCommands, `FieldViewModel` hierarchy, XAML `DataTemplate`s, elimination of procedural `FieldEditor.cs`, and code-behind purging in `MainWindow.axaml.cs`.
- `tests/Configurator.Desktop.Tests`: Modernizing headless unit tests and workflow tests to exercise the decomposed ViewModels and declarative bindings.
- `tests/Configurator.Desktop.NativeHost`: Updating the native host runner to conform to refactored ViewModels and command bindings.

Excluded scope:
- Modifying core domain engine logic (`Configurator.Core`).
- Altering native protected writer implementations (`Configurator.Artifacts`).
- Browser WASM client (`Configurator.Browser`) or Terminal client (`Configurator.Terminal`).

## Findings & Mitigation Mappings

### IVSD-F001: Separation of concerns must preserve memory zeroing and secret isolation
In the existing monolithic ViewModel, `CryptographicOperations.ZeroMemory` is called explicitly in `finally` blocks during file reading and artifact writing. Decomposing the ViewModel into multiple child ViewModels and services must not weaken or drop this custody guarantee.

- Lifecycle: accepted; severity: high.
- Principles: *Amanah* (responsible custody), *La Darar* (prevention of harm).
- Mitigation IVSD-M001: All sensitive byte buffers and stream reads remain strictly bounded and wiped via `CryptographicOperations.ZeroMemory` in disposal and completion blocks across all refactored ViewModels and services.

### IVSD-F002: Asynchronous dialogs must preserve generation fencing against race conditions
File selection dialogs in desktop applications run asynchronously. If an operator starts choosing a file, changes workspaces, or cancels the operation, a late-resolving dialog result must not overwrite the active workspace or load an obsolete file.

- Lifecycle: accepted; severity: high.
- Principles: Truthfulness, non-harm, predictability.
- Mitigation IVSD-M002: The refactored `IStorageDialogService` and consuming ViewModels retain monotonic generation tokens (`_generation`), discarding any result if the active generation changed during the dialog await.

### IVSD-F003: Declarative forms must maintain masking and sensitivity boundaries
The existing `FieldEditor.cs` procedurally checked `sensitivity` properties and masked fields requiring protection (`PasswordChar != '\0'`). The replacement `FieldViewModel` hierarchy and XAML `DataTemplate`s must guarantee that sensitive and masked fields cannot be rendered in plaintext.

- Lifecycle: accepted; severity: high.
- Principles: Privacy, *Hifz al-Sirr* (protection of secrets).
- Mitigation IVSD-M003: `FieldViewModel` encapsulates masking flags, and XAML `DataTemplate`s bind `PasswordChar="●"` for masked string inputs, with comprehensive headless test verification.
