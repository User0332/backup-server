FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

COPY src/User0332.Backup.Server/User0332.Backup.Server.csproj src/User0332.Backup.Server/
COPY src/User0332.Backup.Shared/User0332.Backup.Shared.csproj src/User0332.Backup.Shared/

RUN dotnet restore "src/User0332.Backup.Server/User0332.Backup.Server.csproj" --force

COPY src/User0332.Backup.Server/ src/User0332.Backup.Server/
COPY src/User0332.Backup.Shared/ src/User0332.Backup.Shared/

WORKDIR /build/src/User0332.Backup.Server
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 5131
ENTRYPOINT [ "dotnet", "User0332.Backup.Server.dll" ]