FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ENV NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT=10
ENV NUGET_ENHANCED_NETWORK_RETRY_DELAY_MILLISECONDS=5000

COPY . .

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/ECommerce.Host/ECommerce.Host.csproj \
    --disable-parallel

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/ECommerce.Host/ECommerce.Host.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/Uploads \
    && chown -R app:app /app/Uploads

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "ECommerce.Host.dll"]