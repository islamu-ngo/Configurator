# Build with the repository root as context and this file selected explicitly.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.404-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /source
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/*

# Restore the Browser graph before copying source so ordinary edits reuse this layer.
COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "NuGet.Config", "Configurator.slnx", "./"]
RUN dotnet workload install wasm-tools
COPY ["src/Configurator.Core/Configurator.Core.csproj", "src/Configurator.Core/packages.lock.json", "src/Configurator.Core/"]
COPY ["src/Configurator.Presentation/Configurator.Presentation.csproj", "src/Configurator.Presentation/packages.lock.json", "src/Configurator.Presentation/"]
COPY ["src/Configurator.Browser/Configurator.Browser.csproj", "src/Configurator.Browser/packages.lock.json", "src/Configurator.Browser/"]
COPY ["src/Configurator.Browser.Host/Configurator.Browser.Host.csproj", "src/Configurator.Browser.Host/packages.lock.json", "src/Configurator.Browser.Host/"]
# Razor presence enables the SDK's implicit assets package during locked restore.
COPY ["src/Configurator.Browser/App.razor", "src/Configurator.Browser/"]
RUN dotnet restore src/Configurator.Browser/Configurator.Browser.csproj --locked-mode
RUN dotnet restore src/Configurator.Browser.Host/Configurator.Browser.Host.csproj --locked-mode
COPY . .
RUN dotnet build src/Configurator.Browser.Host/Configurator.Browser.Host.csproj \
    --configuration "$BUILD_CONFIGURATION" --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish src/Configurator.Browser.Host/Configurator.Browser.Host.csproj \
    --configuration "$BUILD_CONFIGURATION" --output /publish/host \
    --no-build --no-restore /p:UseAppHost=false
RUN dotnet msbuild eng/configurator/Configurator.Release.proj \
    -target:PublishConfiguratorBrowser \
    "-property:Configuration=$BUILD_CONFIGURATION" \
    -property:ConfiguratorOutputRoot=/publish

FROM base AS final
COPY --from=publish /publish/host/ .
COPY --from=publish /publish/browser/wwwroot/ ./wwwroot/
ENTRYPOINT ["dotnet", "Configurator.Browser.Host.dll"]
