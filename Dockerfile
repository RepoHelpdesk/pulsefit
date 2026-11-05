# One image: the Vue back office is built into the API's wwwroot and served from the same origin.

FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api
WORKDIR /src
COPY src/PulseFit.Api/PulseFit.Api.csproj src/PulseFit.Api/
RUN dotnet restore src/PulseFit.Api/PulseFit.Api.csproj
COPY src/ src/
COPY --from=web /src/src/PulseFit.Api/wwwroot src/PulseFit.Api/wwwroot
RUN dotnet publish src/PulseFit.Api/PulseFit.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=api /app ./
# A demo: fresh sample data on every start and every night at 3am gym time.
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Gym="Data Source=/tmp/pulsefit.db" \
    Demo__ResetOnStartup=true \
    Demo__NightlyReset=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "PulseFit.Api.dll"]
