# Grafana OSS 13.2.2 configuration definition

This independently authored MIT example maps four synthetic answers to a Docker
environment file and a Graphite data-source provisioning YAML file, and is
promoted into the bundled Store at `store/grafana-oss/` with `target-runtime-verified`
conformance evidence. The `.invalid` addresses deliberately cannot be used as
live service destinations. Replace them with operator-controlled values and have
your target Grafana instance validate the result before production use.

`configurator.yaml` owns the current runtime `setup-definition.json` (and compiles
byte-identically to `store/grafana-oss/releases/2.json`). Native identity
allocation maintains family `c9091324-8fc2-42ed-a659-95b708d4c0a4`, revision `2`;
canonical runtime digest:
`bb583ec230dbfd0f2a9f0488d6ea3beef9e5ec64d66d4f872c82c132bf34448d`.
`answers.json` is a synthetic `0.0.1` private answer envelope with nested `server`
and `datasource` objects; `fixtures.json` exercises ready composition and
expected-refusal cases via `islamu-configurator definition test`
(`fixtureDigest: b5e8000cb92e1fd22548f044fd19f76789fdc7c0f43a27f89654160a2851e4a4`).
`release.json` is the unsigned `0.0.1` delivery receipt generated and checked by
`eng/configurator/DefinitionDelivery.cs` (`trustBoundary: "unsigned-local-verification-only"`,
`signatureStatus: "unsigned"`). The `hostname` and `http-url` formats are checked
automatically for lexical shape only; Grafana remains the target validator.

## Transient Docker runtime verification (2026-11-24)

The standalone CLI prepared `.env` (`22f36118ea95ec3e98322c8d3700b725b37c54e792dc35ff1b7061a575093a0e`)
and `datasource.yaml` (`0f097bbcc96b617a693d6237fcbc0a09a1463ffc16d6ad0b2452071535721a8b`)
and launched a transient loopback container from
`grafana/grafana@sha256:ac461fb352abc50da10a51c7d02462e9c05488f11f53f14b3ad79a8145f638a0`
(Grafana OSS `13.2.2`, external AGPLv3 runtime only; zero Grafana source,
images, trademarks, or dependencies enter this repository):

- `GET /api/health` returned HTTP `200`.
- `GET /api/admin/settings` confirmed `server.domain = dashboard.example.invalid`
  and `users.allow_sign_up = false`.
- `GET /api/datasources` confirmed provisioned `Example metrics` (`type: graphite`,
  `access: proxy`, `url: http://metrics.example.invalid:8080`) with zero
  provisioning errors, followed by clean container removal (`docker rm -f`).

**Claim boundary:** This proof verifies startup, environment variable ingestion,
and Graphite provisioning YAML acceptance in that exact container image only.
It does **not** prove upstream Graphite network connectivity, authentication
policies, cryptographic signatures, or target apply authority.

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
