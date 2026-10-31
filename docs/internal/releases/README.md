# Configurator release inputs

> **Audience:** Maintainers | Contributors | AI agents
> **Kind:** Procedure
> **Status:** Current release-input route; publication requires separate authority
> **Owner:** Packaging/Release
> **Last Verified:** 2026-10-30
> **Source Anchors:** `eng/release/README.md`, `docs/internal/RELEASE_RUNBOOK.md`, `docs/internal/releases/changes/`
> **Review basis:** Documentation routing, not release qualification

Start with the [release runbook](../RELEASE_RUNBOOK.md) and
[native tooling](../../../eng/release/README.md). Committed change fragments are
governed immutable inputs; correcting current documentation does not authorize
rewriting their historical descriptions, identities or hashes.

Configurator owns its release inputs independently of Event. Native release
engineering creates governed YAML change fragments under `changes/` when a Configurator
release needs them. Event release history, signers and promotion authority are
not transfer inputs. Internal extraction increments carry their recorded
changelog disposition in Git; an empty YAML inventory is valid before the first
Configurator release.
