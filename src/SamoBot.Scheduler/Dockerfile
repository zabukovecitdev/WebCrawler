FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SamoBot.Scheduler/SamoBot.Scheduler.csproj", "src/SamoBot.Scheduler/"]
RUN dotnet restore "src/SamoBot.Scheduler/SamoBot.Scheduler.csproj"
COPY . .
WORKDIR "/src/src/SamoBot.Scheduler"
RUN dotnet build "./SamoBot.Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/src/SamoBot.Scheduler"
RUN dotnet publish "./SamoBot.Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SamoBot.Scheduler.dll"]
