FROM mcr.microsoft.com/dotnet/sdk:10.0.400-noble@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
COPY global.json ./
COPY backend/ backend/
RUN dotnet restore backend/Analysis.slnx --locked-mode
RUN dotnet build backend/Analysis.slnx --configuration Release --no-restore
RUN dotnet run --project backend/tests/Analysis.OperationalChecks --configuration Release --no-build --no-restore
RUN dotnet run --project backend/tests/Analysis.CatalogChecks --configuration Release --no-build --no-restore
RUN cd backend && dotnet tool restore \
    && dotnet ef migrations has-pending-model-changes --project src/Analysis.Infrastructure --configuration Release --no-build
RUN dotnet publish backend/src/Analysis.Api --configuration Release --no-build --no-restore --output /out/api \
    && dotnet publish backend/src/Analysis.Worker --configuration Release --no-build --no-restore --output /out/worker \
    && dotnet publish backend/tests/Analysis.CatalogChecks --configuration Release --no-build --no-restore --output /out/checks

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-noble@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_EnableDiagnostics=0 TZ=UTC
USER $APP_UID
EXPOSE 8080

FROM runtime AS api
COPY --from=build /out/api/ ./
HEALTHCHECK --interval=5s --timeout=6s --start-period=15s --retries=3 CMD ["dotnet", "Analysis.Api.dll", "--healthcheck", "/api/health/ready"]
ENTRYPOINT ["dotnet", "Analysis.Api.dll"]

FROM runtime AS worker
COPY --from=build /out/worker/ ./
HEALTHCHECK --interval=5s --timeout=6s --start-period=15s --retries=3 CMD ["dotnet", "Analysis.Worker.dll", "--healthcheck", "/health/ready"]
ENTRYPOINT ["dotnet", "Analysis.Worker.dll"]

# Explicit verification target only; fixtures never enter API or worker images.
FROM runtime AS m2checks
COPY --from=build /out/checks/ ./
ENTRYPOINT ["dotnet", "Analysis.CatalogChecks.dll"]
