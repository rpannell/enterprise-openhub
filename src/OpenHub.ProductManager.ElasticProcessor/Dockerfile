# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/OpenHub.ProductManager.ElasticProcessor/Dockerfile -t openhub/product-manager-elastic-processor:latest src/

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["OpenHub.ProductManager.ElasticProcessor/OpenHub.ProductManager.ElasticProcessor.csproj", "OpenHub.ProductManager.ElasticProcessor/"]
COPY ["OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "OpenHub.Platform.ServiceDefaults/"]
COPY ["OpenHub.Accessor.KeyCloak/OpenHub.Accessor.KeyCloak.csproj", "OpenHub.Accessor.KeyCloak/"]
COPY ["OpenHub.ProductManager.Contracts/OpenHub.ProductManager.Contracts.csproj", "OpenHub.ProductManager.Contracts/"]
RUN dotnet restore "OpenHub.ProductManager.ElasticProcessor/OpenHub.ProductManager.ElasticProcessor.csproj"

# Build
COPY . .
WORKDIR "/src/OpenHub.ProductManager.ElasticProcessor"
RUN dotnet publish "OpenHub.ProductManager.ElasticProcessor.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.ProductManager.ElasticProcessor.dll"]
