FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY MQTTSimulator/*.csproj ./MQTTSimulator/
RUN dotnet restore MQTTSimulator/MQTTSimulator.csproj
COPY MQTTSimulator/ ./MQTTSimulator/
RUN dotnet publish MQTTSimulator/MQTTSimulator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MQTTSimulator.dll"]
