# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/OpenHub.ProductManager.IdentityEventProcessor/Dockerfile -t openhub/product-manager-identity-event-processor:latest src/

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["OpenHub.ProductManager.IdentityEventProcessor/OpenHub.ProductManager.IdentityEventProcessor.csproj", "OpenHub.ProductManager.IdentityEventProcessor/"]
COPY ["OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "OpenHub.Platform.ServiceDefaults/"]
COPY ["OpenHub.Accessor.KeyCloak/OpenHub.Accessor.KeyCloak.csproj", "OpenHub.Accessor.KeyCloak/"]
COPY ["OpenHub.Platform.IdentityEvents.Contracts/OpenHub.Platform.IdentityEvents.Contracts.csproj", "OpenHub.Platform.IdentityEvents.Contracts/"]
RUN dotnet restore "OpenHub.ProductManager.IdentityEventProcessor/OpenHub.ProductManager.IdentityEventProcessor.csproj"

# Build
COPY . .
WORKDIR "/src/OpenHub.ProductManager.IdentityEventProcessor"
RUN dotnet publish "OpenHub.ProductManager.IdentityEventProcessor.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.ProductManager.IdentityEventProcessor.dll"]
