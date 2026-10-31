# IP Protection, Clean-Room Governance, And Audit Readiness

> **Audience:** Maintainers | Contributors | AI agents | Legal reviewers
> **Status:** Operational policy; legal conclusions and exceptions require qualified legal review
> **Owner:** Project Steward
> **Contributing reviewers:** Contributor Experience
> **Last Verified:** 2026-10-08
> **Source Anchors:** `LICENSE`, `THIRD-PARTY-NOTICES.md`, `Directory.Packages.props`, `Directory.Build.props`, `.ci/scripts/validate-dependency-license-policy.cs`, `docs/internal/extraction/PROVENANCE.md`

This is the repository's source of truth for externally informed implementation,
third-party material, dependency-license compatibility, and provenance evidence.
It is an engineering control, not legal advice or a guarantee that a court,
regulator, or licensor will agree with a project classification.

## Legal Posture And Authority Boundary

Configurator is distributed under MIT. The Project Steward explicitly authorized
MIT transfer of ISLAMU-owned material from Event, including the reusable
procedures restored from revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38`. This first-party transfer is an
explicit exception to the source-free handoff requirement: identified authorized
originals may be copied faithfully and minimally adapted, with source path,
revision/blob and disposition recorded in the transfer manifest. Do not replace
them with new summaries on the premise that authorized originals are forbidden
external source.

The exception does not confer rights over third-party expression. Event's AGPL
license and service covenant are not changed or imported into Configurator.
There is no CLA automation or service covenant governing this MIT utility.
Every assembled distribution must comply with each third-party component's own
terms. Contributor assertions cannot cure copied code or missing authority.

| Material | Who controls outbound terms? | Rule |
|---|---|---|
| Authorized ISLAMU-owned transfer | Project Steward within the applicable rights | MIT transfer with recorded original provenance |
| Third-party library, service, image, asset, dataset, generated output or documentation | Its rightsholder or applicable license | Retains its own terms; evaluate every intended distribution mode |
| Unverified or copied material | Unknown or third party | Must not enter implementation context, repository, build artifacts or releases |

## Non-Negotiable Rules

1. Do not copy third-party code, snippets, SQL, migrations, tests, comments,
   documentation prose, schemas with expressive selection, assets, or generated
   artifacts without separately documented rights and distribution approval.
2. Do not provide incompatible implementation source, decompiled output,
   disassembly, ASTs or source-derived structural notes to implementation agents.
3. Externally informed implementation starts from a sanitized functional
   specification describing observable behavior, constraints, inputs, outputs,
   errors and edge cases only. The authorized first-party transfer above is
   separate, not a general waiver.
4. Independently design structure, sequence, organization, naming, data model,
   control flow, UI composition, tests and docs using repository-native patterns.
5. Do not add a dependency whose terms prevent lawful MIT distribution of the
   assembled Configurator offering or another intended Steward-approved path.
   Exceptions require documented legal/distribution approval before merge.
6. First-party transfer authority never relicenses third-party material.
7. Record provenance and independent-design review before externally informed
   work is merged. Absence of external influence may be recorded as
   `Not externally informed`.

## Three Defensive Pillars

### 1. Literal-Copying Prevention

The clean-room boundary is stricter than a line-by-line similarity check.
Prohibited material includes source and object-code-derived representations,
exact internal naming, distinctive comments, tests, database migrations, query
text, copied screen assets, documentation passages, and translations of code
from one language or framework to another.

Public API or interoperability requirements may require exact identifiers or
wire values. Treat those as externally constrained elements, record source and
necessity, copy no surrounding implementation expression, and escalate when
contract or access terms are uncertain.

### 2. Non-Literal Copying And SSO Review

Different syntax or a different framework is not sufficient independence.
Review Structure, Sequence, and Organization (SSO), including module boundaries,
domain decomposition, state transitions, data relationships, operation ordering,
UI hierarchy, error taxonomy and test arrangement.

Use Abstraction-Filtration-Comparison (AFC) as a conservative engineering review
heuristic:

1. **Abstraction:** describe external behavior and proposed ISLAMU design at
   progressively higher levels, from user goal to workflows, subsystems, data
   and operations.
2. **Filtration:** identify elements dictated by functionality, standards,
   interoperability, security, efficiency, platform constraints, public-domain
   material or commonplace domain practice. Filtering is a review judgement,
   not a legal conclusion.
3. **Comparison:** compare remaining discretionary expression. If distinctive
   ordering, grouping, naming, relationships, UI composition or prose remains
   materially similar, redesign it or obtain qualified legal review.

Repository-native architecture is evidence of independent design when it follows
real requirements: bounded data-only definitions, Core/Artifacts/Presentation
ownership, immutable operation generations, transient consent-gated browser custody, protected
native output and separate host apply authority. Patterns are not a safe harbor
by themselves.

### 3. Clean-Room Context Sanitization

The researcher or product analyst observes authorized public behavior and
documentation, then produces a source-free handoff. The implementer receives
only that handoff plus ISLAMU repository context.

If restricted source enters an implementation context:

1. Stop implementation immediately.
2. Discard unmerged output created from the contaminated context.
3. Record the incident without reproducing restricted material.
4. Have an unexposed reviewer create a new functional specification from
   permitted observations.
5. Start implementation in a fresh context and obtain legal review when
   contamination scope is uncertain.

## Functional Ideas, Commonplace Elements, And Scenes A Faire

Functional goals, industry-standard flows, public standards, and elements
dictated by external constraints may be unprotectable or receive narrow
protection in some jurisdictions. EU software law distinguishes program
expression from underlying ideas and principles, and EU case law distinguishes
functionality from program expression. United States software cases use doctrines
including merger, scenes a faire, and AFC.

Use these concepts only to classify risk. A scenes a faire label is never
self-approval. Record why an element is commonplace or constrained, preferably
with multiple independent observations when not defined by one public standard.
Do not reproduce a source's expressive selection, arrangement, text, graphics or
detailed workflow merely because its high-level function is common.

## Clean-Room Workflow

### Phase 1 - Authorized Observation And Functional Specification

- Prefer public behavior, documentation, standards, protocols, and independently
  observable inputs/outputs.
- Record source title/URL, access date, access basis and observed facts. Do not
  archive source excerpts, trees or third-party assets.
- Describe user capabilities, business constraints, failure modes, accessibility,
  security and acceptance criteria.
- Separate observations from assumptions and design decisions.

### Phase 2 - Source-Free Handoff

- Use the [audit-record template](../../../.agents/skills/ip-clean-room/resources/audit-record-template.md).
- Include no third-party identifiers unless needed for provenance or an
  interoperability contract.
- Attest that no source, snippet, AST, decompiled artifact, SQL, migration, test,
  comment or asset is present.
- End the research context; start implementation from the sanitized handoff in
  a fresh context.

### Phase 3 - Independent Implementation And SSO Review

- Derive design from ISLAMU requirements, repository architecture and standards.
- Record at least one meaningful independent design choice in definition model,
  workflow, artifacts, UI or failure handling.
- Run the [AFC/SSO checklist](../../../.agents/skills/ip-clean-room/resources/sso-and-provenance-review.md)
  before review. Similarity not dictated by a functional constraint is a blocker.

### Phase 4 - Provenance And Audit Evidence

- Link sanitized handoff, source register, issue/intent, design decision,
  dependency review, tests and PR.
- Append non-obvious reusable conclusions to `dev/_journal/journal.md`.
- Preserve facts/hashes through normal Git history; never place restricted
  content in evidence artifacts.

## Research Artifact Storage Decision

Sanitized feature-specific research belongs in `dev/active/<workstream>/` by
default. Promote only durable, implementation-independent requirements into
authoritative `docs/` pages. Do not create "non-attribution" reports: audit
readiness requires source identity/access provenance, while isolation requires
excluding source expression. Raw excerpts, screenshots, downloads or
source-derived artifacts remain outside the repository and implementation
contexts.

The 154 tracked entries under `.agents/skills/gitbook/resources/example-site`
are excluded: the Evolve Demo site-derived corpus has no verified transfer
license and includes tokenized GitBook assets. Project-native example guidance
replaces it; do not reconstruct it from its URLs. It is not covered by the
first-party MIT exception.

## Dependency And License Gate

Before adding/updating a package, library, container image, generated component,
font, asset or dataset, record:

- exact component/version;
- direct, transitive, build-only, test-only, optional-service or shipped-runtime role;
- license expression and authoritative terms;
- obligations for MIT and every intended alternative distribution;
- whether terms attach only to the component or restrict combined/derivative work;
- notices, source-offer, attribution, patent, trademark, hosting, seat,
  field-of-use, redistribution or sublicensing requirements;
- approval, rejection, replacement or separately licensed path.

Signed definition or extension inputs remain third-party material even when
their origin is authenticated. A signature proves origin/integrity only; it
cannot waive licensing, authorize executable plugins, SQL, migrations or scripts,
or grant target administration rights. Retain template ID/version, source kind,
license expression and review reference for imported legal-template provenance.
Portability is not permission to copy unlicensed text or a claim of compliance.

Evaluate the actual resolved/published graph, including native assets and
test-only packages, rather than inferring distribution rights from central
declarations. The retained local validator is:

```bash
dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .
```

It checks visible license expressions/approved metadata against dependency
locks and available published inventories. Missing restored metadata fails.
It is not legal advice and cannot prove commercial-contract, unusual exception,
asset-license, service-term or assembled-distribution compatibility.

A component is blocked when it forces incompatible terms onto ISLAMU-owned
material or prohibits an intended distribution. Choose a compatible replacement
or document separate rights covering every intended build/distribution.
Optional loading or moving a component to another process does not waive review.

### Single-Edition Dependency Policy

Configurator has one supported build graph. `Directory.Packages.props` pins
packages; `Directory.Build.props` supplies generated locks and locked CI restore.
Regenerate lockfiles through normal restore, never by hand. Generator privacy,
generated-output obligations and notices remain separate review duties.

Terminal.Gui uses its official NuGet package with an exact central pin and
generated locks. Embedded grammar/theme assets retain their own terms and
reviewed supplemental notices, tied to the restored package version and content
hash. A package's MIT metadata does not relicense those assets. The temporary
downstream archive, patch and rebuild machinery were retired by explicit
Steward direction on 2026-10-13; see the
[decision and history](../TERMINAL_GUI_DEPENDENCY.md).

git-cliff is an operator-supplied build-time renderer with a frozen toolchain
identity, not installed by this transfer. Retain its notices. Configurator-owned
promotion authority must precede signed publication; source tooling and
synthetic fixtures do not constitute trust activation.

## PR Evidence And Stop Conditions

A PR must state one of:

- `Not externally informed; no dependency changed`; or
- sanitized handoff/provenance record, independent SSO review and dependency
  license evidence; or
- authorized first-party transfer with original revision/blob, selected sections,
  adaptations, exclusions and separate third-party review.

Stop and request qualified legal review when rights, access terms, protectable
expression, reverse engineering, interoperability exceptions, contributor
authority, commercial redistribution or dependency compatibility are unclear.
Do not merge on an undocumented assumption.

## Primary Legal References

- [Directive 2009/24/EC](https://eur-lex.europa.eu/legal-content/EN/ALL/?uri=CELEX:32009L0024) - protected expression versus underlying ideas/principles.
- [CJEU Case C-406/10 summary](https://eur-lex.europa.eu/legal-content/EN/SUM/?uri=celex:62010CJ0406) - functionality, programming language and data formats versus expression.
- [17 U.S.C. Section 102](https://www.law.cornell.edu/uscode/text/17/102) - idea, procedure, process, system and method-of-operation boundary.
- [Copyright Office Circular 61](https://www.copyright.gov/circs/circ61.pdf) - program registration guidance and scope limits.

## Related Repository Sources

- [MIT license](../../../LICENSE)
- [Third-party notices](../../../THIRD-PARTY-NOTICES.md)
- [Transfer provenance](../extraction/PROVENANCE.md)
- [Clean-room skill](../../../.agents/skills/ip-clean-room/SKILL.md)
