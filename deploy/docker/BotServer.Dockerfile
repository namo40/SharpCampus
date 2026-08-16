# The build context is the repository root, so every path below is repository-relative.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY . .

# A bot plays through the same services a console client calls and reads no master data of its own,
# so this image carries neither the sources nor a built database.
RUN dotnet publish src/SharpCampus.BotServer -c Release -o /app/publish -p:ExcludeMasterDataSources=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

# Kestrel binds what the Kestrel section of appsettings names, which the compose file overrides per
# container; the port the base image defaults to would only be overridden with a warning.
ENV ASPNETCORE_HTTP_PORTS=

# The unprivileged user the aspnet image ships. Nothing here needs root.
USER app

# gRPC (h2c) and the operations endpoints.
EXPOSE 5003 5013

ENTRYPOINT ["dotnet", "SharpCampus.BotServer.dll"]
