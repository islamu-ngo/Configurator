# Browser Container Dependency Record

> **Audience:** Maintainers and release reviewers
> **Owner:** Platform/Ops
> **Reviewed:** 2026-10-20
> **Scope:** Optional standalone Browser image; no image publication or signer activation

## Exact inputs

Registry manifest inspection resolved these identities on the review date:

| Component | Role | Manifest digest |
| --- | --- | --- |
| `mcr.microsoft.com/dotnet/sdk:10.0.302-noble` | Build-only SDK matching `global.json`; discarded from the final image | `sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0` |
| `mcr.microsoft.com/dotnet/aspnet:10.0.10-noble` | Shipped runtime for the first-party public-file host and inherited operating-system closure | `sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b` |

The Dockerfile pins both version tags and digests. A digest identifies the
selected image bytes; it is not proof of publisher authentication, vulnerability
absence or support on every listed platform. Changes require another resolved
closure/license review and actual image verification, not only a tag edit.

## Terms and distribution

.NET and ASP.NET Core retain their original MIT and third-party notices.
Ubuntu and installed system packages retain their own terms; no single license
describes the assembled filesystem. Component-specific source and notice
obligations are not waived by placing them in a container or by Configurator's
MIT license.

The implementation does not patch or rebuild upstream components. It preserves
the base filesystem, package records and original notices; the existing release
owner independently includes the Browser's actual restored NuGet/runtime notices.
Only the first-party `Configurator.Browser.Host` executable and published static
application assets are added. No external web-server configuration is restored,
generated or copied. The host uses the ASP.NET Core shared framework, with no
additional NuGet package or product project references. The SDK filesystem is not
copied into the runtime image.

This decision admits these pinned inputs for local build and runtime
verification. It does not authorize registry publication. Before conveying an
assembled image, retain its exact platform/image identity, package/SBOM
inventory, original license/notice material and corresponding component-source
availability required by the resolved licenses. Do not advertise the entire
image as MIT-only, remove base metadata, substitute a new image digest without
review, or treat a package scanner as legal approval.

## Functional sources and independent design

Official documentation and registry metadata were consulted on 2026-10-20:

- [Microsoft standalone Blazor WebAssembly hosting](https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0):
  the published application is a static tree, not a server-side answer process.
- [ASP.NET Core static files](https://learn.microsoft.com/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
  and [response compression](https://learn.microsoft.com/aspnet/core/performance/response-compression?view=aspnetcore-10.0):
  public-file MIME handling, SPA fallback and transport compression APIs.
- [Microsoft .NET container documentation](https://learn.microsoft.com/dotnet/core/docker/build-container):
  non-root application user, SDK/runtime separation and .NET startup.
- [Microsoft container identities and terms](https://github.com/dotnet/dotnet-docker):
  pinned runtime identity and preserved original notices.
- [Coolify application settings](https://coolify.io/docs/applications/configuration/general)
  and [networking](https://coolify.io/docs/core/networking-in-coolify):
  repository-root build context, Dockerfile selection, internal port routing
  and orchestrator-owned proxy/networks. These are deployment instructions,
  not an additional image dependency or a Coolify-specific runtime.

The first-party host was independently authored from the existing public-file,
route, MIME, logging and shipped-CSP requirements and official API facts.
Container stages and context admission use Configurator's actual release owner.
Interoperability names, commands, versions, digests and documented platform
constraints are factual inputs; third-party prose and implementation structure
were not copied.

The corrected .NET Dockerfile uses the first-party
`Event/src/Explore.Blazor/Dockerfile` as a stage/restore-cache reference:
`base`, `build`, `publish` and `final`. It does not copy Event's server application,
secret-management wiring, project graph or deployment assumptions into this
standalone WASM utility. The user's later explicit instruction removes NGINX
entirely; it supersedes the earlier static-runtime selection. The new host
serves public files only and does not introduce Blazor Server, SSR or a private
answer service.

The public guide owns build/run commands. The technical packaging anchor owns
control flow and verification boundaries. Neither a successful image build nor
this record establishes a production hosting, native protected-save,
assistive-technology or target-application acceptance claim.
