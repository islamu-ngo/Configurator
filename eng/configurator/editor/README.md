# Local Browser editor assets

`editor-entry.js` owns the private editing adapter. It is product build input,
not a runtime CDN loader. `../BuildBrowserEditor.cs` owns dependency review,
generation and offline freshness verification. Never edit its generated vendor
outputs by hand.

## Reviewed closure

The exact approved closure and original-license evidence are recorded in
[`codemirror.md`](../../../docs/internal/legal/dependencies/codemirror.md).
The implementation-owner decision and independent terms review cover local
integration, not human hash approval or product qualification. Native generation
must preserve that exact graph and its complete original notices. Source
compilation and mocked editor tests do not qualify the actual engine or offline
package.

## Review without installation

Create a private proposal directory with exact direct dependency versions and
generate its npm v3 lock using `npm install --package-lock-only --ignore-scripts
--no-audit --no-fund`. That operation resolves metadata; it does not install a
dependency tree. Then run from the repository root:

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- \
  --review-closure .tmp/browser-ux-simplification/editor-closure-proposal
```

The native tool verifies archive integrity and extracts only original license
and notice entries. `closure-review.json` binds component name, exact version,
direct/transitive role, registry archive and integrity, and each original
license hash. `license-review.txt` retains those terms. Neither is an approval.
An optional final output directory keeps review artifacts outside source inputs:

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- \
  --review-closure eng/configurator/editor .tmp/browser-ux-simplification/editor-production-review
```

## Generate after exact closure approval

The native owner's offline self-test uses disposable synthetic assets, not a
real dependency approval or installation:

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --self-test
```

It checks an intact fixture and rejects changed approval/input bindings, closure,
component identity, output bytes, missing terms and altered toolchain metadata.

The production `package.json` must contain exact direct pins and a
`configuratorEditor` record with `approval: "approved"`, the approved
`closureSha256`, `recordSha256`, `lockSha256`, `noticesSha256`, and exact
`node`, `npm` and `bun` versions. The legal record must identify the actual
approval; do not populate it from an assumption.

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- \
  --write "$PWD"
TMPDIR="$HOME/.cache/agent-tmp" dotnet run --file eng/configurator/BuildBrowserEditor.cs -- \
  --check "$PWD"
```

`--write` verifies approval bindings and tool versions, runs native `npm ci`
with lifecycle scripts disabled, checks installed original terms against the
reviewed closure, and invokes Bun to bundle the local entry point. It generates
`configuration-editor.js`, `configuration-editor.LICENSE.txt` and
`configuration-editor.manifest.json` in
`src/Configurator.Browser/wwwroot/vendor/`.

`--check` does not install dependencies or access the network. It verifies
approval, source/lock/legal hashes, canonical component closure, notices and
output sizes/hashes. Changed build inputs require native regeneration. Repeat
generation with the same approved inputs and compare output hashes before
claiming reproducibility.

## Runtime ownership

One active panel owns one editor. Text and bounded undo stay inside that editor;
callbacks carry only version, dirty and history-limit metadata. Reads transfer
UTF-8 bytes to the shared document session and clear the transfer afterward.
Core and Presentation own scope, validation and atomic Apply. Completion uses
public definition metadata, never accepted answers.

`setMetadata` reconfigures public language, labels and phrases without a source
transaction. It preserves the model version and owned history, refreshes an open
search panel through public commands, and restores still-connected focus.
The bridge does not patch CodeMirror-owned content attributes independently.

Revocation destroys the private model synchronously in the consent change
capture listener. Retirement also clears pending reads, diagnostics, callbacks
and history. JavaScript string reclamation is not a physical memory-erasure
guarantee. Actual published cache, network, keyboard, IME and lifecycle evidence
is required separately from component tests.
