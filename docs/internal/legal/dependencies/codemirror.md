# Local Browser editor dependency review

> **Audience:** Maintainers and release reviewers
> **Owner:** Browser / Platform
> **Reviewed:** 2026-11-01
> **Status:** Exact closure approved for local integration; product qualification remains open
> **Decision:** Integrate only the reviewed closure below, retaining its complete original notices and verifying native generated assets.

## Approved component graph

This technical dependency decision covers the one local CodeMirror editor
selected by the approved Browser UX simplification implementation. It does not
certify completed integration or product qualification.

| Component | Exact version | Resolved role | Recorded terms |
| --- | --- | --- | --- |
| `@codemirror/autocomplete` | 6.20.4 | Direct runtime | MIT |
| `@codemirror/commands` | 6.11.1 | Direct runtime | MIT |
| `@codemirror/lang-json` | 6.0.2 | Direct runtime | MIT |
| `@codemirror/language` | 6.13.1 | Direct runtime | MIT |
| `@codemirror/lint` | 6.9.7 | Direct runtime | MIT |
| `@codemirror/search` | 6.7.2 | Direct runtime | MIT |
| `@codemirror/state` | 6.7.6 | Direct runtime | MIT |
| `@codemirror/view` | 6.43.15 | Direct runtime | MIT |
| `@codemirror/streamparser` | 6.0.0 | Transitive runtime | MIT |
| `@lezer/common` | 1.5.5 | Transitive runtime | MIT |
| `@lezer/highlight` | 1.2.5 | Transitive runtime | MIT |
| `@lezer/json` | 1.0.3 | Transitive runtime | MIT |
| `@lezer/lr` | 1.4.12 | Transitive runtime | MIT |
| `@marijn/find-cluster-break` | 1.0.4 | Transitive runtime | MIT |
| `crelt` | 1.0.7 | Transitive runtime | MIT |
| `style-mod` | 4.1.4 | Transitive runtime | MIT |
| `w3c-keyname` | 2.2.8 | Transitive runtime | MIT |

The table is the resolved package graph, not proof that every module survives
bundle tree shaking. Each exact registry archive was verified against its
native lock SHA-512 integrity. Only original license/notice files were read;
no third-party implementation source or expressive examples entered the
implementation context.

## Review binding and current limits

The native review binds sorted component identity, version, role, terms, archive
integrity/source and each original license-file SHA-256:

- Closure SHA-256:
  `0494667e39ad04b311fd84b37ed53397e156020156198ee3ce1626b090664bd9`
- Proposal lock SHA-256:
  `d226aef66b41efed0f638186f7f3b93fe90bef6cb31d246c8f9e8f13749180bd`
- Production lock SHA-256:
  `fe664dfbbf3eeac5f530e172ef7125fefced26ae3b016cfef142fe838217a6bd`
- Original-notice review bundle SHA-256:
  `281fa74258cf5b84111aa2f77a749ac06de39ac4d3c34c3eb4e1e3d03f248d5d`

The proposal was resolved with npm **12.0.2** on Node **26.9.0** using
`--package-lock-only --ignore-scripts --no-audit --no-fund`. No `node_modules`
tree was installed. The local bundler available for the planned build is Bun
**1.4.2**; no bundler executable is conveyed by this proposal.

On November 1, a metadata-only registry advisory check of this exact lock exited
0 with an empty vulnerability map and zero findings at every reported severity:

```bash
cd .tmp/browser-ux-simplification/editor-closure-proposal
npm audit --package-lock-only --ignore-scripts --json --registry=https://registry.npmjs.org
```

The audit reported 17 total dependencies. Native SHA-256 checks afterward still
matched the proposal manifest
`fd5cda90cf70c4ece703dd540848be24ef6ff2b34d56b3ce30aeb18df56a9b33`
and lock hash above, and no `node_modules` directory existed. This is dated
known-advisory evidence, not closure approval or a guarantee against undisclosed
defects.

Native review command:

```bash
dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --review-closure .tmp/browser-ux-simplification/editor-closure-proposal
```

It completed with exit 0 for **17 components / 8 direct dependencies**.
The ignored proposal contains the native lock, generated `closure-review.json`
and the complete original `license-review.txt`. These are local decision
evidence, not portable shipped assets. The native asset owner
must retain the exact graph and original terms in the planned generated
manifest/notice outputs. Changes to the closure require a new decision.

The production manifest has a distinct private package name. Native lock
generation and a separate archive/license review reproduced the same
17-component closure and original-notice hash, exit 0:

```bash
cd eng/configurator/editor
npm install --package-lock-only --ignore-scripts --no-audit --no-fund --registry=https://registry.npmjs.org
cd ../../..
dotnet run --file eng/configurator/BuildBrowserEditor.cs -- --review-closure eng/configurator/editor .tmp/browser-ux-simplification/editor-production-review
```

## Decision authority and independent review

On November 1, the implementation owner approved the exact closure above within
the existing full-workstream implementation authorization. An independent
read-only review checked the complete manifest, lock, component review and all
17 original license texts, and recommended local integration subject to notice
preservation. It found no incompatible term or unclear compatibility issue
requiring legal escalation.

This is a documented technical dependency decision, not a claim that the user
approved this particular hash. The earlier Steward-only pause in this record
was an added workflow interpretation. The authoritative
[dependency gate](../IP_GOVERNANCE.md#dependency-and-license-gate) requires an
exact graph, actual terms, obligations and disposition; its
[stop conditions](../IP_GOVERNANCE.md#pr-evidence-and-stop-conditions) require
qualified legal review when rights or compatibility are unclear. That review
did not identify such a condition in these original MIT terms.

## Terms and intended distribution

The actual extracted license files grant MIT permissions and require preserving
the applicable copyright and permission notices. Their warranty/liability
disclaimers must remain with the material. The Lezer JSON notice additionally
names Arun Srinivasan; component-specific copyright lines must not be flattened
into an ISLAMU ownership claim.

The approved use is local Browser editing in the MIT Configurator distribution,
without a runtime CDN, language server, remote AI or executable definition
extension. Original copyright, permission and warranty/liability text must
travel with the distributed bundle, with component/version-to-notice mapping.
The terms do not impose source disclosure, source-offer, hosting, seat,
field-of-use or royalty conditions on this use. They contain no express patent
grant or trademark authorization; this decision supplies neither. This is not
an alternative-offering legal certification or a grant to relicense components.

## Source register and independent design

Accessed 2026-10-31:

- [CodeMirror documentation](https://codemirror.net/docs/): public interface facts only.
- [CodeMirror reference](https://codemirror.net/docs/ref/): state/view, lexer and lifecycle contracts only.
- [Official npm registry](https://registry.npmjs.org/): exact version metadata,
  archive integrities and original component license files.
- [npm install documentation](https://docs.npmjs.com/cli/v12/commands/npm-install/):
  metadata-only lock behavior.

The integration follows Configurator's existing form, document-session, consent
and operation-generation boundaries. The editor entry point was independently
implemented in a fresh source-free worker from functional requirements and
official public API facts. The parent reviewed the first-party implementation;
no third-party application workflow, UI composition, test arrangement or
implementation source was supplied as a model. Licensed dependencies remain
separately identified components, not newly claimed ISLAMU-owned expression.
Deterministic generated-asset verification and actual Browser custody,
offline, accessibility and journey evidence remain required.
