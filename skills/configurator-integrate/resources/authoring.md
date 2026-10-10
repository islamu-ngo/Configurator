# Author a compact integration

Use the package's `schemas/configurator-authoring-v1.schema.json` as the machine vocabulary. The discriminator is `urn:islamu:configurator:authoring:v1`. The installed command help is authoritative for options.

Declare project ID/name, exact application versions, fields and outputs. Repository and explicit definition license are needed for redistribution in the optional store; an independent local integration need not invent them. Tool-generated identity is persisted once. Keep application version, source contract version and definition revision distinct.

Fields are maps keyed by meaningful target names. Type is required. Sensitivity defaults conservatively to sensitive; guided disclosure and humanized titles are generated. Use `public` only after reviewing the actual leaf. Nested object properties and list-object properties follow the same policy. Never import a private default or recommended value.

JSON/YAML shorthand copies selected declared root keys. Dotenv shorthand requires representable scalar fields. Use bindings for renamed output keys; bindings and an explicit projection cannot coexist. For constants, conditions, nested/repeated structures or Markdown, use the admitted projection vocabulary and logical references. Avoid target-key inference from punctuation or traversal order.

YAML accepts one bounded document with exact lowercase booleans/null and JSON-shaped exact numbers. Quote ambiguous strings and typed-looking keys. Empty plain scalars, aliases, anchors, tags, directives, merge keys and multiple documents reject. Comments affect neither canonical bytes nor digest.

Before releasing, compare generated output with independent synthetic target expectations. A compiler check proves the preparation policy, not that the target will accept or apply the result. Retain the prior canonical release and revise deliberately when compiled content changes; never overwrite history or silently migrate user answers.
