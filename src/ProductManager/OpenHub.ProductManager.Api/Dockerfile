# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/ProductManager/OpenHub.ProductManager.Api/Dockerfile -t openhub/product-manager:latest src/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["ProductManager/OpenHub.ProductManager.Api/OpenHub.ProductManager.Api.csproj", "ProductManager/OpenHub.ProductManager.Api/"]
COPY ["Platform/OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "Platform/OpenHub.Platform.ServiceDefaults/"]
COPY ["Accessor/OpenHub.Accessor.KeyCloak/OpenHub.Accessor.KeyCloak.csproj", "Accessor/OpenHub.Accessor.KeyCloak/"]
RUN dotnet restore "ProductManager/OpenHub.ProductManager.Api/OpenHub.ProductManager.Api.csproj"

# Build
COPY . .
WORKDIR "/src/ProductManager/OpenHub.ProductManager.Api"
RUN dotnet publish "OpenHub.ProductManager.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.ProductManager.Api.dll"]
