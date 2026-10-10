# Scaffold from a public schema

Use `definition init --from-schema` with a local public Draft 2020-12 schema. Supply only explicitly selected, root-confined local reference documents through the installed command's declared interface. No remote reference fetching is allowed.

Import is deliberately bounded: explicit scalar types, homogeneous string enums, representable bounds, closed objects, homogeneous arrays and acyclic local definitions/references. Unsupported asserted keywords, open maps, tuples, unions/combinators, conditional schemas and regular expressions reject rather than disappear.

Imported defaults are candidates and are discarded until a maintainer explicitly classifies the leaf public and reviews its value. `writeOnly: true` raises sensitivity to secret; its absence is never evidence for public classification. Annotation-only formats do not become validators.

Strict mode refuses known differences such as required-string handling, UTF-16 length and bounded exact numeric range. `--accept-bounded-semantics` acknowledges only the enumerated differences returned by the tool; it cannot bypass unsupported assertions, private defaults or invalid output behavior. Report the differences in the review and retain the target's own validation responsibility.

Review generated fields, exact target versions, output selection and privacy before check/build. Include one supported and one refused synthetic schema in the exercise; a successful import never implies full JSON Schema equivalence.
