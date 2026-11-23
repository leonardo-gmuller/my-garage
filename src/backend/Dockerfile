FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/backend/ src/backend/
RUN dotnet restore src/backend/MinhaGaragem.Api/MinhaGaragem.Api.csproj
RUN dotnet publish src/backend/MinhaGaragem.Api/MinhaGaragem.Api.csproj -c Release --no-restore -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && mkdir -p /data/uploads /data/keys && chown -R $APP_UID:$APP_UID /data
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MinhaGaragem.Api.dll"]
