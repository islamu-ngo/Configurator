---
description: Compare an ordinary configuration export with a selected definition before deliberately filling fields.
---

# Import And Compare Configuration

Comparison helps you understand an existing configuration file under the
definition you selected. Filling fields is a separate action that reuses the
values the file actually represents.

{% hint style="info" %}
This workflow is being implemented. Client availability and measured package
qualification are not yet complete.
{% endhint %}

## Choose The Scope

Select the application definition and the declared output artifact that matches
your JSON, YAML or dotenv file. Use an ordinary configuration export; no special
Configurator import envelope or older definition is required.

A matching filename does not authenticate a file or establish its source release.
Some outputs intentionally omit information and cannot be reversed. The tool
must show unsupported or incomplete coverage rather than guess missing values.

## Compare Before Filling Fields

**Compare with this definition** is the default purpose. Comparison leaves your
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

Choose **Fill fields** only when you want recovered values applied. The action
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

## Understand What Was Checked

A completed assessment is not necessarily compatible. Known problems can require
changes while other coverage remains incomplete. A resource stop reports
incomplete work, not compatibility. Paging a completed retained report does not
make its assessment incomplete.

Compatibility applies only to the selected definition and assessed artifact
scope. It does not prove historical additions, removals or renames, a safe
application upgrade, a running deployment's state, or acceptance by the target
application. Target validation and authorization remain the application's job.

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
