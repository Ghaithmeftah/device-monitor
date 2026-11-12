FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config DeviceMonitor.slnx ./
COPY src/DeviceMonitor.Web/DeviceMonitor.Web.csproj src/DeviceMonitor.Web/
RUN dotnet restore src/DeviceMonitor.Web/DeviceMonitor.Web.csproj
COPY src/ src/
RUN dotnet publish src/DeviceMonitor.Web/DeviceMonitor.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DeviceMonitor.Web.dll"]
