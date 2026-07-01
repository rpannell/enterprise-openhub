# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/OpenHub.ProductManager.Function/Dockerfile -t openhub/product-manager-function:latest src/

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS base
WORKDIR /home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["OpenHub.ProductManager.Function/OpenHub.ProductManager.Function.csproj", "OpenHub.ProductManager.Function/"]
COPY ["OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "OpenHub.Platform.ServiceDefaults/"]
COPY ["OpenHub.Accessor.KeyCloak/OpenHub.Accessor.KeyCloak.csproj", "OpenHub.Accessor.KeyCloak/"]
COPY ["OpenHub.ProductManager.Contracts/OpenHub.ProductManager.Contracts.csproj", "OpenHub.ProductManager.Contracts/"]
RUN dotnet restore "OpenHub.ProductManager.Function/OpenHub.ProductManager.Function.csproj"

# Build
COPY . .
WORKDIR "/src/OpenHub.ProductManager.Function"
RUN dotnet publish "OpenHub.ProductManager.Function.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /home/site/wwwroot

FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=build /home/site/wwwroot .
