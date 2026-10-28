# Grafana OSS 13.2.2 format example

This independently authored example maps four synthetic answers to a Docker
environment file and a data-source provisioning YAML file. It demonstrates
Configurator's declared `.env` and YAML projection only. Grafana startup,
provisioning acceptance, and data-source connectivity have **not** been tested
against the pinned release. The `.invalid` addresses deliberately cannot be
used as live service destinations. Replace them with operator-controlled values
and have Grafana validate the result before using it.

`configurator.yaml` owns the v2 runtime `setup-definition.json`. Native identity
allocation created family `ffc20a25-7ea5-4138-be3b-88f678cac36b`, revision 1;
canonical runtime digest:
`0920bb01a8304dba1b514ce625ecb9a20adc7e491f086f46d6cc324692c493d6`.
`answers.json` is a synthetic version 1 private answer envelope. Hostname and URI
formats are descriptive annotations in the base profile, not target validation.

The environment output contains `GF_SERVER_DOMAIN` and
`GF_USERS_ALLOW_SIGN_UP`. A Grafana container can receive these as environment
variables. The YAML output is intended for the container's provisioning
`datasources` directory; its Graphite entry uses server-side access. Configurator
does not start Grafana, connect to the endpoint, or apply either file. Both
outputs are restricted native artifacts even though the synthetic answers are
public.

## Functional sources and provenance

Observed from official sources on 2026-10-09:

- [Grafana OSS v13.2.2 release](https://github.com/grafana/grafana/releases/tag/v13.2.2): exact target release identity.
- [Configure Grafana Docker](https://grafana.com/docs/grafana/latest/setup-grafana/configure-docker/): Docker environment configuration and provisioning root.
- [Configure Grafana](https://grafana.com/docs/grafana/latest/setup-grafana/configure-grafana/): `GF_` setting-name mapping and existing-option override behavior.
- [Provision Grafana](https://grafana.com/docs/grafana/latest/administration/provisioning/): data-source YAML placement and the required `apiVersion`, `datasources`, `name`, `type`, and `access` interface keys.

These are interface facts, not copied upstream examples. The definition's
field grouping, projections, synthetic values, expected bytes, and prose were
designed for Configurator's existing data-only model. No Grafana source,
assets, instructions, or dependency entered the repository. The test compares
generated outputs to independently written literal files and SHA-256 digests;
it does not establish target acceptance.
