# CLI results preserve publication facts independently of delivery

Status: accepted for the authorized bounded machine-outcome implementation.
Date: 2026-10-15. Owners: CLI and native packaging.
Implementation anchor: [CLI contract](../CLI_CONTRACT.md).
Operator guidance: [command-line configurator](../../public/cli.md).

## Context

A restricted file can be published before a receipt overflows or stdout fails.
Reporting only command failure hides established effects and can invite an unsafe
retry. Repeated field coverage/readiness arrays also make receipt size grow with
both definition size and selected output count. Public definition/ID-map output
has the same written-prefix problem: ordered commits are not a transaction.

## Decision

Replace command v1 with v2; keep authoring/runtime v1. A command-owned
`SetupCliResultPlan` and `SetupCliOutcomeLedger` size the mandatory receipt before
effects and retain every selected output's full ID, established digest, sensitivity,
path intent and write outcome. Later publication or cleanup failure cannot erase
known writes or relabel an unknown commit as not-written. Reuse the native protected
publisher rather than introducing a transaction, retry or event framework.

Keep the 65,536-byte UTF-8 receipt bound including newline. Replace duplicated
coverage arrays with exact counts and sanitized-view SHA-256 digests; bounded
read-only detail continuation retrieves coverage/diagnostics without replaying a
write. At most 16 inline diagnostic examples and requested detail share an 8 KiB
budget with exact diagnostic totals/omissions. RFC 6901 pointers and optional
numeric source-position slots describe structure without adding Core repair.
View digests bind public definition, operation, selection and safe status facts,
never a private-answer hash or persistent answer cache.

Bundle selection uses repeated `--artifact` up to 64 times, not CSV `--artifacts`;
there is no compatibility alias. Invocation limits admit the real maximum-ID
selection: 256 tokens, 32,768 aggregate UTF-8 bytes including separators, and
4,096 characters per argument, while artifact IDs remain at most 128 characters.
No Core admission maximum is lowered to make receipts fit.

`SetupCliApplication` owns the only render/delivery attempt. Delegates return/store
typed results; Program never reopens stdout for fallback. Mark delivery started
before writing, and treat broken delivery as exit 74 with fixed best-effort stderr
diagnostics, not as rollback. `delivery: attempt` records intent; the receipt
cannot acknowledge its own receipt. Command completion, nullable/unassessed
preparation, target acceptance and cleanup are independent facts. Target acceptance
stays unassessed because this offline tool never contacts the target.

Prepare all selected destinations before ordered commits, including canonical
definition and optional ID map. Preserve a committed prefix if later work fails.
Regenerate v2 from the compiled schema owner and retire command v1 through owned
generator/package cleanup, preserving unrelated package content and definition
schemas. Independent machine verification must not use production serialization
as its expected-result oracle.

## Consequences and limits

Automation must migrate envelope consumers and CSV bundle arguments together.
Validation exits still describe the underlying read-only operation during detail
retrieval. Consumers advance by actual returned entries and guard noninitial pages
with the sanitized-view digest; unchanged structural views may survive value changes.

A healthy channel receives one bounded JSON line. A failed channel can receive
only a prefix or nothing even after successful publication. Consumers inspect
established destinations before retrying; no in-band protocol can guarantee
delivery acknowledgment, rollback or recovery after process loss.

This decision does not claim cooperative async/cancellation, parser/environment
rework, hardened public authoring custody, installed discovery/repair, portable
skill lifecycle, four-host qualification or complete installed adoption. Those
remain separate work; the receipt improvement is not enterprise certification.

## Functional provenance and independent design

The repository-native planning handoff recorded official API facts on 2026-10-15.
This document uses that source-free handoff, not external implementation or copied
documentation prose. The relevant sources are:

| Source | Interface or standards fact only |
| --- | --- |
| [Spectre command lifecycle](https://spectreconsole.net/cli/explanation/command-lifecycle-and-execution-flow) | Settings binding/validation precede command execution. |
| [Spectre error handling](https://spectreconsole.net/cli/how-to/handling-errors-and-exit-codes) | Exception propagation changes which handler owns failure mapping. |
| [Spectre testing](https://spectreconsole.net/cli/how-to/testing-command-line-applications) | An injected console supports observing actual command binding/output. |
| [RFC 6901](https://www.rfc-editor.org/rfc/rfc6901.html) | JSON Pointer defines structural member-token escaping. |

Framework API availability does not determine product authority. The single CLI
emitter, pre-sized result ledger, reused protected publisher and count/digest
continuation are independently chosen around Configurator's existing owners and
privacy constraints. No package upgrade, source transfer, new host/container or
license approval is inferred; existing component identities/notices remain intact.
