# Author a public secret policy

Inspect the target's public configuration declarations, format documentation and
synthetic tests. Establish whether it consumes a password, opaque token, encoded
key, password hash, digest, private key or connection string. Record the exact
accepted format, algorithm and bounds with the target version. A field name such
as `password`, `token` or `key` is insufficient evidence for a generation recipe.
Do not inspect private answers, `.env`, key stores or deployed credentials.

Use the built-in authoring-0.0.1 schema and current `definition check` to discover
the closed `x-configurator.secret` vocabulary. The declaration establishes a
secret sensitivity floor; explicit public classification cannot override it,
including through local references. Standard string bounds and typed secret
constraints jointly govern acceptance. Generation settings choose values within
that policy; they do not relax acceptance or target requirements. Never insert
private defaults, initial values, suggestions, examples or constants in a public
definition. Retired `credential` metadata is not a compatibility route.

Keep these choices distinct:

- A local random password or token is not an API credential issued by a provider.
- A password hash must match the target's exact serialization and cost policy.
  A SHA-2 digest is not password storage. Format validation does not verify the
  original password, and Configurator's PBKDF2 envelope is not a universal format.
- An HMAC key and an AES key are different policies even when both encode random
  bytes. The encoded string length is not the decoded key size.
- A private RSA/EC key requires the target's exact PEM or private JWK format and
  key parameters. Local well-formedness does not establish target acceptance.
- A connection string is classified as secret but is not a locally generated
  credential, and does not authorize a connection or credential lookup.

Missing target format or constraints are material authoring questions. Do not
guess them from examples or choose arbitrary cryptographic flexibility. Use the
supported recipe that matches documented target requirements; reject an
unsupported requirement rather than weakening the policy or adding executable
definition code. Policy admission cannot promise acceptable device latency or
qualification on an untested browser or host.

Check and build the public definition, then validate synthetic complete,
incomplete and invalid answers locally. Confirm stable safe diagnostic codes and
readiness without copying values into reports. Public integration work does not
authorize generating an operator's credentials. An explicitly requested private
generation workflow belongs to the machine CLI skill and protected local output;
generated values never belong in chat, command arguments or public examples.
