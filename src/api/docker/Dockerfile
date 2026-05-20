# ── Build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first for layer-cache efficiency
COPY Api.csproj .
RUN dotnet restore

# Copy remaining source and publish release build
COPY . .
RUN dotnet publish Api.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ──────────────────────────────────────────────────────────
# aspnet:8.0 is Debian-based; no SDK or build artefacts are included (OWASP A02).
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is required by the docker-compose healthcheck
# (test: curl -sf http://localhost:8080/health)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
