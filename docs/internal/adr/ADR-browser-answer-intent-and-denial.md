# Browser answers preserve edit intent and denial before cleanup

Status: proposed; design refined by CTO review, not implemented or I-VSD-revalidated.
Date: 2026-10-19. Owners: Presentation and Browser maintainers; direct native editors consume the shared contract.
Technical anchors: [presentation state](../PRESENTATION_STATE.md), [browser custody](../LAUNCH_CONTRACT.md).
Operator guidance: [browser preparation](../../public/browser.md).

## Context

Imported/default empty strings and an admitted empty enum choice can collapse to absence when state is reconstructed from display text. Rejected nested drafts can fail to advance the revision used to reject stale import. Browser consent currently awaits output cleanup before applying denial; sequential artifact retirement can leave later artifacts live while the first cleanup is held.

Current form mutation gates, post-lock notification batching, editor generations and export byte/handle ownership are the appropriate seams. No answer server, new state framework or export service is needed.

## Decision

Keep typed presence separate from rendered draft text within the existing shared form/input owner. Distinguish deliberate user edit from programmatic refresh or untouched native navigation. Parse, establish draft validity and advance one logical edit revision under the same gate, then notify after the transition. Clone typed imports/defaults. Keep absent, empty string, admitted empty enum choice, false, zero and empty containers distinct. Give omission a distinct selection identity; migrate direct Browser/Desktop/Terminal consumers without compatibility aliases.

Apply secret denial and recursive secret clearing before asynchronous cleanup. Fence pending import/preparation and detach all output owners. Synchronously invalidate every detached workspace/export owner before awaiting any old cleanup, then await only captured retirements. Late or failing old cleanup cannot restore secrets or erase replacement output. Existing expected JS rejection remains value-safe; unexpected failures must be observed, not detached silently.

## Consequences and alternatives

This changes shared editor semantics and requires all direct callers/tests to migrate coherently. The current gate and export owner remain the only state authorities. Export-time reinsertion cannot recover input intent; a Browser-only presence dictionary duplicates policy; an empty-option string sentinel cannot distinguish a valid empty enum choice. A compatibility parser would preserve the defect and is rejected in development mode.

Logical denial does not recall already completed clipboard/download transfers, erase all browser-managed immutable strings, or protect against compromised delivered code/extensions. JS boundary tests establish local ordering/ownership, not platform certification. Public release still needs current candidate, real browser/host/privacy/accessibility and operator evidence.

## Verification before acceptance

Require failing independent presence/import/empty-enum and invalid-edit vectors before implementation. Gate real JS cleanup, deny consent, apply a public edit and assert policy/secret absence and stale-capability rejection while cleanup remains held. Cover multiple artifacts, replacement and cleanup failure; verify current replacement bytes afterward. Use real form/Core/composer and only boundary fakes preserving id/generation/bytes. Native untouched navigation/append remains a no-op. No sleeps, mock-call mirroring, prose tests or warning suppression.

This record remains proposed until the changed plan/task mapping is I-VSD-revalidated and implementation/verification evidence is recorded. User approval, commits and public activation are separate authorities.
