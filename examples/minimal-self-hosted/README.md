# Minimal self-hosted service example

> **Audience:** Adopters | Contributors
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-09
> **Source Anchors:** `examples/minimal-self-hosted/setup-definition.json`, `examples/minimal-self-hosted/answers/complete.json`, `examples/minimal-self-hosted/expected/`

The [offline quickstart](../../docs/public/getting-started.md) runs this
definition through the CLI and saves a `.env` file plus JSON and YAML manifests
on a protected Linux path. No target service is included or started.

| Input or expected output | Path |
| --- | --- |
| Definition | [`setup-definition.json`](setup-definition.json) |
| Synthetic answers | [`answers/complete.json`](answers/complete.json) |
| Expected dotenv | [`expected/environment.env`](expected/environment.env) |
| Expected JSON | [`expected/service.json`](expected/service.json) |
| Expected YAML | [`expected/service.yaml`](expected/service.yaml) |

The sample credential is a synthetic placeholder. Replace it in a locally
protected answer file for real use. The browser can choose public options, but
the required credential and all complete native artifacts stay in native
clients. Target validation and application remain with the target software.
