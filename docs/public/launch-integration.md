# Launch integration

An application may link to a Configurator static origin:

```text
https://<configurator-origin>/launch/69cdb122-41d2-4fab-9b82-7929a889c726?appVersion=1.0&entry=setup&locale=en
```

The example family belongs to the neutral bundled service. Applications own
their family GUIDs and definitions; the browser has no target API or Event
catalogue dependency. A link does not transfer credentials or grant apply or
administrative authority. Family GUIDs and publisher labels are identity claims,
not authentication, signature verification or curation evidence.

## Selection

The path must contain a nonzero, canonical **lowercase D-format GUID**. Unknown
families reject locally. Only these query keys are accepted, each at most once
including percent-encoded aliases:

| Key | Bound | Meaning |
| --- | --- | --- |
| `appVersion` | 64 characters | Exact member of the definition's application version set |
| `entry` | 128 characters | Exact declared entry point |
| `locale` | 35 characters | Exact supported locale |
| `digest` | 64 hex characters | Optional SHA-256 pin of the entire admitted definition |

Version, entry and locale selector values contain only ASCII letters, digits,
dot, hyphen or underscore. Empty values, malformed escapes, unexpected keys,
duplicates, fragments and queries longer than 1,024 encoded characters reject.
Do not put answers, URLs, tokens, login hints or return addresses in the link.
Unknown entries, unsupported locales, incompatible versions and mismatching
digests also reject.

With `appVersion`, Core chooses the highest compatible revision; compatibility
is an exact declared version, not a semantic-version range. With `digest`, only
that exact definition may be selected, and any supplied version must also be
compatible. Without both selectors the registry must contain exactly one
revision for the family, or the launch rejects as ambiguous. Omitted entry and
locale select the first declared value. Entry/locale select metadata, not a
separate filtered definition or a promise of translated questions.

Once selected, the workspace pins the complete admitted definition, revision
and digest. Registry refreshes cannot upgrade it silently. Explicit launch
navigation begins a new workspace. A public template imported into a new
revision must match its digest and revision exactly; no implicit migration is
performed.

## Bundling and privacy

Install other data-only definition JSON under
`src/Configurator.Browser/Definitions/`; the generic project resource glob
embeds it. The neutral sample is linked from
[`examples/minimal-self-hosted/setup-definition.json`](../../examples/minimal-self-hosted/setup-definition.json)
at build time. Core admission validates every bundled definition at boot.
Preserve the complete
definition's canonical identity. Do not silently filter private field metadata
out of an existing family/revision and publish a conflicting digest. The browser
only renders nonsecret public question metadata and collects declared public
enum/boolean selections; all other answers remain native-only.

Definition assets are public distribution data: do not include credentials,
private defaults, operational secrets or confidential explanatory text in them.
The public-template format is not full native configuration and never confers
target apply authority. See [public browser](browser.md) for the closed envelope,
import limit and static hosting fallback.
