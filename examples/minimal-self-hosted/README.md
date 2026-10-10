# Minimal self-hosted service example

> **Audience:** Adopters | Contributors
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-09
> **Source Anchors:** `examples/minimal-self-hosted/setup-definition.json`, `examples/minimal-self-hosted/answers/complete.json`, `examples/minimal-self-hosted/expected/`

The [offline quickstart](../../docs/public/getting-started.md) runs this
definition through the CLI and saves a `.env` file plus JSON and YAML manifests
on a protected Linux path. No target service is included or started.

[`configurator.yaml`](configurator.yaml) is the compact maintainer source for the
next sample revision. It preserves the same target keys and output formats while
the compiler derives runtime field metadata and projections. Build it through
`definition check` and `definition build`; revision 3 is distinct from the checked
expanded revision 2 because generated array ordering participates in canonical
identity. See the [integration guide](../../docs/public/integrate-your-tool.md).

| Input or expected output | Path |
| --- | --- |
| Definition | [`setup-definition.json`](setup-definition.json) |
| Synthetic answers | [`answers/complete.json`](answers/complete.json) |
| Expected dotenv | [`expected/environment.env`](expected/environment.env) |
| Expected JSON | [`expected/service.json`](expected/service.json) |
| Expected YAML | [`expected/service.yaml`](expected/service.yaml) |

The sample credential is a synthetic placeholder. Replace it in a locally
protected answer file for real use. The [browser](../../docs/public/browser.md)
can prepare these files with transient answers: explicitly consent to enter
the required credential, or defer it in an incomplete draft and finish it
locally. Browser downloads cannot establish protected native-save permissions.
Target validation and application remain with the target software.
