# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.400-alpine3.23@sha256:b36516b249f0cccf9e5017082f51d4bda2d61469f205a7167fbf3b8498ecdd59 AS build
WORKDIR /src

COPY Directory.Build.props Analysis.slnx ./
COPY src/Analysis.Hosting/Analysis.Hosting.csproj src/Analysis.Hosting/
COPY src/Analysis.Hosting/packages.lock.json src/Analysis.Hosting/
COPY src/Analysis.Api/Analysis.Api.csproj src/Analysis.Api/
COPY src/Analysis.Api/packages.lock.json src/Analysis.Api/
RUN dotnet restore src/Analysis.Api/Analysis.Api.csproj --locked-mode

COPY src/Analysis.Hosting/ src/Analysis.Hosting/
COPY src/Analysis.Api/ src/Analysis.Api/
RUN dotnet publish src/Analysis.Api/Analysis.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-alpine3.23@sha256:4d5339ac9814f1a033a09e664bcf159e9fb386c89b6e7917b3dca7254e656027 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 \
  CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8080/health/ready"]

ENTRYPOINT ["dotnet", "Analysis.Api.dll"]
