FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SamoBot.Crawler/SamoBot.Crawler.csproj", "src/SamoBot.Crawler/"]
RUN dotnet restore "src/SamoBot.Crawler/SamoBot.Crawler.csproj"
COPY . .
WORKDIR "/src/src/SamoBot.Crawler"
RUN dotnet build "./SamoBot.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/src/SamoBot.Crawler"
RUN dotnet publish "./SamoBot.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SamoBot.Crawler.dll"]
