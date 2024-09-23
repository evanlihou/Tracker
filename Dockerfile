FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src
COPY ["Tracker/Tracker.csproj", "Tracker/"]
COPY nuget.config .
RUN dotnet restore --configfile nuget.config "Tracker/Tracker.csproj"
COPY Tracker Tracker
WORKDIR "/src/Tracker"
RUN dotnet build "Tracker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tracker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tracker.dll"]
