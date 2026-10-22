# syntax=docker/dockerfile:1

# 1) Frontend (React + Vite) → web/dist
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# 2) Backend (.NET 10) → /app/publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY src/Arco.Api/Arco.Api.csproj src/Arco.Api/
RUN dotnet restore src/Arco.Api/Arco.Api.csproj
COPY src/ src/
COPY --from=web /web/dist/ src/Arco.Api/wwwroot/
RUN dotnet publish src/Arco.Api/Arco.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# 3) Runtime: um único processo serve API + SPA
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
# tzdata: fusos IANA (ex.: America/Sao_Paulo) para calcular o "hoje" do usuário
RUN apk add --no-cache tzdata
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Arco="Data Source=/data/arco.db"
RUN mkdir -p /data && chown $APP_UID /data
VOLUME /data
COPY --from=api /app/publish .
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "Arco.Api.dll"]
