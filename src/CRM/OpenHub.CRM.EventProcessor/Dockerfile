# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/CRM/OpenHub.CRM.EventProcessor/Dockerfile -t openhub/crm-event-processor:latest src/

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["CRM/OpenHub.CRM.EventProcessor/OpenHub.CRM.EventProcessor.csproj", "CRM/OpenHub.CRM.EventProcessor/"]
COPY ["CRM/OpenHub.CRM.Contracts/OpenHub.CRM.Contracts.csproj", "CRM/OpenHub.CRM.Contracts/"]
COPY ["Platform/OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "Platform/OpenHub.Platform.ServiceDefaults/"]
RUN dotnet restore "CRM/OpenHub.CRM.EventProcessor/OpenHub.CRM.EventProcessor.csproj"

COPY . .
WORKDIR "/src/CRM/OpenHub.CRM.EventProcessor"
RUN dotnet publish "OpenHub.CRM.EventProcessor.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.CRM.EventProcessor.dll"]
