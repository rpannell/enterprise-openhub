# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/CRM/OpenHub.CRM.Api/Dockerfile -t openhub/crm-api:latest src/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["CRM/OpenHub.CRM.Api/OpenHub.CRM.Api.csproj", "CRM/OpenHub.CRM.Api/"]
COPY ["CRM/OpenHub.CRM.Contracts/OpenHub.CRM.Contracts.csproj", "CRM/OpenHub.CRM.Contracts/"]
COPY ["Platform/OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "Platform/OpenHub.Platform.ServiceDefaults/"]
COPY ["Accessor/OpenHub.Accessor.KeyCloak/OpenHub.Accessor.KeyCloak.csproj", "Accessor/OpenHub.Accessor.KeyCloak/"]
COPY ["Accessor/OpenHub.Accessor.RabbitMq/OpenHub.Accessor.RabbitMq.csproj", "Accessor/OpenHub.Accessor.RabbitMq/"]
RUN dotnet restore "CRM/OpenHub.CRM.Api/OpenHub.CRM.Api.csproj"

# Build
COPY . .
WORKDIR "/src/CRM/OpenHub.CRM.Api"
RUN dotnet publish "OpenHub.CRM.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.CRM.Api.dll"]
