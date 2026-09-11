FROM docker:29.1-cli AS docker-cli

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ThucLuc.DbManager/ThucLuc.DbManager.csproj src/ThucLuc.DbManager/
RUN dotnet restore src/ThucLuc.DbManager/ThucLuc.DbManager.csproj

COPY src/ThucLuc.DbManager/ src/ThucLuc.DbManager/
RUN dotnet publish src/ThucLuc.DbManager/ThucLuc.DbManager.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl openssh-client \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /config /state /releases /backups \
    && chown -R app:app /app /config /state /releases /backups

COPY --from=docker-cli /usr/local/bin/docker /usr/local/bin/docker
COPY --from=docker-cli /usr/local/libexec/docker/cli-plugins/docker-compose /usr/local/libexec/docker/cli-plugins/docker-compose

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    THUCLUC_OPS_CONFIG_PATH=/config/environments.json \
    THUCLUC_OPS_STATE_PATH=/state \
    THUCLUC_OPS_RELEASE_PATH=/releases \
    Ops__BackupRoot=/backups

EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "ThucLuc.DbManager.dll"]
