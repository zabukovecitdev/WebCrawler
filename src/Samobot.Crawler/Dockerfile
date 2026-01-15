FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Samobot.Crawler/Samobot.Crawler.csproj", "src/Samobot.Crawler/"]
RUN dotnet restore "src/Samobot.Crawler/Samobot.Crawler.csproj"
COPY . .
WORKDIR "/src/src/Samobot.Crawler"
RUN dotnet build "./Samobot.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/src/Samobot.Crawler"
RUN dotnet publish "./Samobot.Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Samobot.Crawler.dll"]
