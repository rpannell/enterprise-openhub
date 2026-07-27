# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/RealtimeMessaging/OpenHub.RealtimeMessaging.Api/Dockerfile -t openhub/realtime-messaging:latest src/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["RealtimeMessaging/OpenHub.RealtimeMessaging.Api/OpenHub.RealtimeMessaging.Api.csproj", "RealtimeMessaging/OpenHub.RealtimeMessaging.Api/"]
COPY ["RealtimeMessaging/OpenHub.RealtimeMessaging.Contracts/OpenHub.RealtimeMessaging.Contracts.csproj", "RealtimeMessaging/OpenHub.RealtimeMessaging.Contracts/"]
COPY ["Platform/OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "Platform/OpenHub.Platform.ServiceDefaults/"]
RUN dotnet restore "RealtimeMessaging/OpenHub.RealtimeMessaging.Api/OpenHub.RealtimeMessaging.Api.csproj"

# Build
COPY . .
WORKDIR "/src/RealtimeMessaging/OpenHub.RealtimeMessaging.Api"
RUN dotnet publish "OpenHub.RealtimeMessaging.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.RealtimeMessaging.Api.dll"]
