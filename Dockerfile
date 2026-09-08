FROM node:24-alpine AS web
WORKDIR /src/web-angular
COPY web-angular/package*.json ./
RUN npm ci
COPY web-angular/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore FirmaOperaCloud.slnx
RUN dotnet publish src/FirmaOperaCloud.Api/FirmaOperaCloud.Api.csproj -c Release -o /app/publish -p:SkipAngularBuild=true --no-restore
COPY --from=web /src/web-angular/dist/web-angular/browser/ /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN adduser --disabled-password --home /app --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FirmaOperaCloud.Api.dll"]
