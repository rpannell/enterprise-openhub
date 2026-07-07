# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/OpenHub.ProductManager.ReindexJob/Dockerfile -t openhub/product-manager-reindex-job:latest src/
#
# Unlike every other Dockerfile in this repo, this container is expected to EXIT (0 on
# success, non-zero on failure) rather than run forever — it's a one-shot job invoked by a
# Kubernetes CronJob (or a plain `docker run`), not a long-running service.

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["OpenHub.ProductManager.ReindexJob/OpenHub.ProductManager.ReindexJob.csproj", "OpenHub.ProductManager.ReindexJob/"]
COPY ["OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "OpenHub.Platform.ServiceDefaults/"]
COPY ["OpenHub.Accessor.KeyCloak/OpenHub.Accessor.KeyCloak.csproj", "OpenHub.Accessor.KeyCloak/"]
COPY ["OpenHub.Accessor.ElasticIndex/OpenHub.Accessor.ElasticIndex.csproj", "OpenHub.Accessor.ElasticIndex/"]
COPY ["OpenHub.ProductManager.Contracts/OpenHub.ProductManager.Contracts.csproj", "OpenHub.ProductManager.Contracts/"]
RUN dotnet restore "OpenHub.ProductManager.ReindexJob/OpenHub.ProductManager.ReindexJob.csproj"

# Build
COPY . .
WORKDIR "/src/OpenHub.ProductManager.ReindexJob"
RUN dotnet publish "OpenHub.ProductManager.ReindexJob.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.ProductManager.ReindexJob.dll"]
