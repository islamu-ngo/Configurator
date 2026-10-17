# Launch integration

An application can link directly to a Configurator static origin and its public
definition. Loading happens before answer collection. Configurator admits the
closed data-only vocabulary, checks canonical identity and then operates
locally; it does not authenticate you to the target or apply configuration.
Family GUIDs, source origins and publisher labels do not establish publisher
authentication, signature verification or curation.

## Bundled definition links

```text
https://<configurator-origin>/launch/69cdb122-41d2-4fab-9b82-7929a889c726?appVersion=1.0&entry=setup&locale=en
```

The path contains a nonzero canonical **lowercase D-format GUID**. Unknown
families reject. Query keys are closed and may appear at most once, including
percent-encoded aliases:

| Key | Bound | Meaning |
| --- | --- | --- |
| `appVersion` | 64 characters | Exact declared application version |
| `entry` | 128 characters | Exact declared entry point |
| `locale` | 35 characters | Exact supported locale |
| `digest` | 64 hexadecimal characters | SHA-256 pin of the entire admitted definition |

Version, entry and locale use ASCII letters, digits, dot, hyphen or underscore.
Empty values, malformed escapes, unexpected/duplicate keys and encoded queries
over 1,024 characters reject. Bundled launches reject fragments. With a version,
Core selects the highest compatible registered revision; compatibility is an
exact declared version set. A digest selects only that exact document and any
supplied version must match. Omitting both requires exactly one registered
revision or the launch is ambiguous. Entry/locale default to the first declared
value and select metadata; they do not filter the admitted definition or promise
translated questions.

## Publisher source links

Open configuration also accepts a public definition file URL directly. Optional
advanced selectors supply the canonical digest, application version, entry and
locale. After admission, review the exact revision and digest before opening
the editor. The definition-only share action copies the public source and
pinned metadata; it never includes workspace answers. Local upload remains
available for private repositories, blocked cross-origin reading and offline
work. Neither route requires [Store](definition-store.md) membership.

Publish a public static HTTPS JSON definition that permits credential-free
cross-origin reading by the Configurator origin, then link using `/open`:

```text
https://<configurator-origin>/open#src=https%3A%2F%2Fpublisher.example%2Fsetup-definition.json&digest=<canonical-sha256>&appVersion=1.0&entry=setup&locale=en
```

Replace the digest placeholder with the **canonical** SHA-256 produced from
Core-admitted content; it is not necessarily the hash of formatted source bytes.
Keep the selected family/revision/digest consistent with the publisher's
release metadata. This URL conveys definition metadata only. Never place
answers, tokens, login hints, private source paths or return addresses in it.

`/open` accepts metadata in the fragment only, with no query. `src` is required
and at most 2,048 characters; the encoded fragment is at most 4,096 characters.
The remaining keys are `digest`, `appVersion`, `entry` and `locale` with the
bounds above. Duplicate decoded keys and malformed/unknown values reject.
The source must be credential-free HTTPS with no user information, query or
fragment. Public hostnames and IP literals admitted by the source-address filter
are allowed; local/private literal addresses, single-label hosts and
`.local`/`.localhost` names reject. No redirect is
followed. A public hostname is not proof of safe DNS resolution; browsers
control DNS and transport, so this is not a server-side network isolation claim.

The request omits credentials and referrer and bypasses the HTTP cache. It is
cancelled on timeout, replacement or disposal. Streaming admission enforces
Core's **4 MiB** byte limit even without a reliable Content-Length. A source
must pass Core admission and all supplied selectors/digest checks before a
workspace can begin. Conflicting previously admitted family/revision content
remains rejected after registry removal. Active work retains its admitted
definition; late loads cannot replace it.

The entry page displays the admitted pinned metadata before an explicit
**Open workspace** action. Public-safe confirmation and optional offline
retention are separate pre-answer actions; they do not enable secret consent.
Without explicit retention, imported public metadata remains session-local.

CORS, offline state or a failed source yields no partially admitted workspace.
Offer the same public JSON file for deliberate local upload. Selected local
`.json`, `.yaml` and `.yml` files also accept self-contained compact maintainer
sources; public HTTPS links require compiled runtime JSON. Local compilation
adds no include, filesystem-reference or network authority. The local path
uses the same admission/byte limits, resets its file control after processing
and begins a fresh no-secret workspace. It does not support arbitrary launch
URLs, local native I/O, executable plugins or credentials from the definition.

## Bundling and public distribution

Install other public definition JSON under
`src/Configurator.Browser/Definitions/`; the project resource glob embeds it.
The neutral sample is linked from
[`examples/minimal-self-hosted/setup-definition.json`](../../examples/minimal-self-hosted/setup-definition.json).
Core admits bundled definitions at boot. Preserve the complete canonical
identity; filtering field metadata from an existing family/revision would
produce conflicting content and must not be relabelled with the same identity.

Definition assets/caches are public distribution data and contain no answer
values, private defaults, credentials or confidential instructions. Explicit
retention caches a selected admitted definition only. Drafts and private output
have a separate transient custody boundary described in the
[browser guide](browser.md).
