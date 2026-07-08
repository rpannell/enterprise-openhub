# syntax=docker/dockerfile:1
# Build context: the src/ directory (one level above this file).
# docker build -f src/OpenHub.Administration.Api/Dockerfile -t openhub/administration:latest src/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore step — copy only csproj files first so this layer is cached between code changes
COPY ["OpenHub.Administration.Api/OpenHub.Administration.Api.csproj", "OpenHub.Administration.Api/"]
COPY ["OpenHub.Platform.ServiceDefaults/OpenHub.Platform.ServiceDefaults.csproj", "OpenHub.Platform.ServiceDefaults/"]
COPY ["OpenHub.Accessor.KeyCloak/OpenHub.Accessor.KeyCloak.csproj", "OpenHub.Accessor.KeyCloak/"]
RUN dotnet restore "OpenHub.Administration.Api/OpenHub.Administration.Api.csproj"

# Build
COPY . .
WORKDIR "/src/OpenHub.Administration.Api"
RUN dotnet publish "OpenHub.Administration.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenHub.Administration.Api.dll"]
