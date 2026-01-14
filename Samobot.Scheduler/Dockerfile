FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Samobot.Scheduler/Samobot.Scheduler.csproj", "Samobot.Scheduler/"]
RUN dotnet restore "Samobot.Scheduler/Samobot.Scheduler.csproj"
COPY . .
WORKDIR "/src/Samobot.Scheduler"
RUN dotnet build "./Samobot.Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Samobot.Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Samobot.Scheduler.dll"]
