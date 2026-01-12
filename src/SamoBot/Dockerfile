FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SamoBot/SamoBot.csproj", "src/SamoBot/"]
RUN dotnet restore "src/SamoBot/SamoBot.csproj"
COPY . .
WORKDIR "/src/src/SamoBot"
RUN dotnet build "./SamoBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/src/SamoBot"
RUN dotnet publish "./SamoBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SamoBot.dll"]
