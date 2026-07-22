# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY .editorconfig global.json Directory.Build.props CentraSA.sln ./
COPY src/CentraSA.Domain/CentraSA.Domain.csproj src/CentraSA.Domain/
COPY src/CentraSA.Application/CentraSA.Application.csproj src/CentraSA.Application/
COPY src/CentraSA.Infrastructure/CentraSA.Infrastructure.csproj src/CentraSA.Infrastructure/
COPY src/CentraSA.Web/CentraSA.Web.csproj src/CentraSA.Web/
RUN dotnet restore src/CentraSA.Web/CentraSA.Web.csproj

FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/CentraSA.Web/CentraSA.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data \
    && chown app:app /data

WORKDIR /app
COPY --from=publish --chown=app:app /app/publish/ ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    Storage__DataDirectory=/data

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CentraSA.Web.dll"]
