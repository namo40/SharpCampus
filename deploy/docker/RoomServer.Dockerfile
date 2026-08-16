# The build context is the repository root, so every path below is repository-relative.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY . .

# The image ships the compiled master data database, so the JSON sources a local run imports stay out
# of the publish output.
RUN dotnet publish src/SharpCampus.RoomServer -c Release -o /app/publish -p:ExcludeMasterDataSources=true
RUN dotnet run --project tools/SharpCampus.MasterDataTool -c Release -- build --input /source/masterdata --output /app/masterdata/masterdata.bin

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build /app/masterdata masterdata

# Kestrel binds what the Kestrel section of appsettings names, which the compose file overrides per
# container; the port the base image defaults to would only be overridden with a warning.
ENV ASPNETCORE_HTTP_PORTS=

# The unprivileged user the aspnet image ships. Nothing here needs root.
USER app

# gRPC (h2c) and the operations endpoints.
EXPOSE 5002 5012

ENTRYPOINT ["dotnet", "SharpCampus.RoomServer.dll"]
