FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SamoBot.Indexer/SamoBot.Indexer.csproj", "src/SamoBot.Indexer/"]
RUN dotnet restore "src/SamoBot.Indexer/SamoBot.Indexer.csproj"
COPY . .
WORKDIR "/src/src/SamoBot.Indexer"
RUN dotnet build "./SamoBot.Indexer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SamoBot.Indexer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SamoBot.Indexer.dll"]
