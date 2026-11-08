---
description: Compare an ordinary configuration export with a selected definition before deliberately filling fields.
---

# Import And Compare Configuration

Comparison helps you understand an existing configuration file under the
definition you selected. Filling fields is a separate action that reuses the
values the file actually represents.

{% hint style="info" %}
Comparison and deliberate filling are available in the machine CLI, Browser,
Desktop and Terminal for supported projections. The
[execution record](../internal/reports/ARTIFACT_IMPORT_COMPARISON_2026-10-30.md)
separates tested candidates and measured limits from other-host or target
acceptance claims.
{% endhint %}

## Choose The Scope

Select the application definition and the declared output artifact that matches
your JSON, YAML or dotenv file. Use an ordinary configuration export; no special
Configurator import envelope or older definition is required.

For definitions with deployment choices, select the recipe and component scope
first, then choose one of its eligible destinations. Import uses that captured
context; filenames and imported keys do not select or broaden the scope.
Environment files use the destination's declared Docker run or Compose syntax,
including explicit empty assignments. Switching context retires the comparison
and any pending adoption.

Some destinations repeat one setting for distinct consumers. Those keys must
appear together and agree after decoding. Conflicting or partly missing copies
refuse filling fields; correct the file rather than relying on key order.

A matching filename does not authenticate a file or establish its source release.
Some outputs intentionally omit information and cannot be reversed. The tool
must show unsupported or incomplete coverage rather than guess missing values.

## Compare Before Filling Fields

In Browser, **Start from an existing configuration** defaults to **Fill fields**
and selects the current output when it identifies exactly one file. Choose a
format explicitly when the output is ambiguous. Choose **Compare with this
definition** to inspect a file without adopting it. Comparison leaves your
current answers, pending editor text, omissions and prepared output unchanged.
Imported-file problems are separate from the validity of your current form.

The assessment distinguishes:

| Result | What to do |
| --- | --- |
| Missing required | Supply an applicable setting whose absence the file proves |
| Invalid for this definition | Review a represented value against the current constraint |
| Unrecognized input | Review obsolete or unmapped content; private names and values are not printed |
| Structural output change | Review or regenerate output metadata |
| Unassessed | Check settings this artifact does not establish |
| Unsupported inverse | Choose another supported artifact or enter values deliberately |

Initialization does not make an absent file member appear supplied. An
unrepresented setting is not automatically a missing file member. A public
operation selector established by the artifact is described as context-derived,
not as something read from your file.

Findings can open declared schema guidance or an enclosing editor. Imported row
numbers and private map keys do not select or overwrite unrelated current rows.
Color is accompanied by severity text; comparison is not a color-only check.

## Deliberately Fill Fields

Use **Fill fields** when you want recovered values applied. The action
checks the current workspace again. Untouched initialization can be replaced;
your explicit answers, omissions and pending drafts are protected.

Adoption is all-or-none. Invalid values, unknown content, structural conflicts or
conflicting edits cannot be silently discarded to import a convenient subset.
Required settings not supplied by the file may remain unfinished work afterward.
If the current form cannot store a represented root entry, filling fields is
unavailable even when the file is compatible. Use a supported declared artifact
or the [machine CLI](cli.md); the form must not silently discard part of the file.

Comparison may complete while filling fields remains unavailable. Read the
eligibility reason first. Repair a separate copy of the source under your review,
then reimport it. Keep the original unchanged. Viewing a report grants no
permission to overwrite current work.

## Editable Document Contract

Document editing is separate from comparing an existing generated file. The
shared document API presents **Editable answers (public scope)**, not a complete
backup: protected fields stay in dedicated Form controls, and no masked token
stands in for a secret. Effective defaults are not copied into editable answers.
Browser Developer integration and its qualification are tracked separately.

Canonical documents use the answer envelope's `values` and `omitted` members.
Within captured scope, absence from both resets an answer; an explicit omission
suppresses initialization. Environment editing instead defaults to merge:
missing keys retain answers. Only deliberate replacement of represented scope
records omissions for missing keys. Read the value-free change summary before
Apply.

Parsing does not edit the form. A valid candidate applies in one revision;
invalid, out-of-scope or obsolete work changes nothing. Untouched drafts,
omissions, origins and protected values remain. Missing required settings can
remain unfinished Review work, but invalid supplied values cannot become
success. A draft intersecting represented scope must be resolved before editable
source is generated.

Browser source acquisition requires workspace consent even when the editable
scope is public: arbitrary pasted text may contain private content. Session and
candidate authority retire on edits, revocation, replacement or disposal. The
[shared-state contract](../internal/PRESENTATION_STATE.md#bounded-document-sessions-and-atomic-apply)
owns the API and lifecycle details.

## Understand What Was Checked

A completed assessment is not necessarily compatible. Known problems can require
changes while other coverage remains incomplete. A resource stop reports
incomplete work, not compatibility. Paging a completed retained report does not
make its assessment incomplete.

Compatibility applies only to the selected definition and assessed artifact
scope. It does not prove historical additions, removals or renames, a safe
application upgrade, a running deployment's state, or acceptance by the target
application. Target validation and authorization remain the application's job.

Secret policies apply to imported supplied values as well as typed or generated
values. A malformed supplied secret is invalid; it is not a missing secret that
a draft may defer. Local hash-format checks do not run the hash's KDF or verify
the original password, and key-format validation does not prove target
acceptance. See the [secret-policy guide](definition-cookbook.md#secret-policy-and-target-format)
and [shared import contract](../internal/IMPORT_COMPARISON.md).

## The Release Notes 1-Click Upgrade Diff Workflow

Application maintainers can embed direct upgrade links in their release notes:

```markdown
### Upgrading to v2.0.0
[⚡ Check Configuration Breaking Changes](https://configurator.islamu.org/diff?id=immich&from=v1.9.0&to=v2.0.0)
```

1. **Protocol Handshake (`configurator://`):** Visitors to the link are greeted with a prompt: *"Open in Configurator Desktop or continue in Web?"* (matching Microsoft Teams, VS Code, and Slack UX patterns).
2. **Desktop Automation:** The Desktop application checks its managed local store (`~/.config/islamu-configurator/profiles/<app-id>/`), identifies the existing profile for the target environment (`development`, `staging`, `production`), and loads the comparison report instantly.
3. **Web Drop-Zone:** In the Web client, operators simply drop their existing `.env` or `answers.json` file into the page to view the diff.
4. **Actionable Delta:** The report immediately flags:
   - 🔴 **Newly Required Variables** that must be answered to avoid container boot crashes.
   - 🟡 **Renamed Variables** with automatic mapping suggested.
   - 🟢 **Deprecated / Obsolete Variables** safely removed.
5. **Direct Vault Sync:** When configured with secret managers (Infisical or Azure Key Vault via `ISLAMU SecretKit`), operators can click **"Apply Upgrade Diff to Staging"** or **"Production"** directly from Desktop without manual copy-pasting.

For the detailed engineering architecture and distribution strategy, see [Product Vision](../internal/PRODUCT_VISION.md) and [Marketing & Adoption Strategy](../internal/MARKETING_AND_ADOPTION_STRATEGY.md).

## Keep Private Input Local

Treat every configuration file as potentially secret-bearing, even when its
current definition contains only public fields. Unknown or obsolete content may
contain old credentials.

Browser inspection requires the workspace's custody acknowledgment and consent
before consuming the file. Configuration bytes and reports stay local and must
not enter public caches, telemetry, URLs, storage or worker messages. Revocation
denies access, retires retained work and clears owned byte buffers; it does not
promise physical erasure of every managed runtime copy.

Browser input remains limited to 1 MiB and native input to 4 MiB, with additional
structural limits. Cancellation and replacement must prevent old work from
publishing later. The source file is never overwritten by comparison.

For surface-specific custody and output boundaries, see
[Browser](browser.md), [Desktop](desktop.md), [Terminal](terminal.md) and
[CLI](cli.md). Native protected saves and deliberate Browser downloads provide
different guarantees.
