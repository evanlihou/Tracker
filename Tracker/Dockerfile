FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Install Node.js
RUN curl -fsSL https://deb.nodesource.com/setup_14.x | bash - \
    && apt-get install -y \
        nodejs \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY ["Tracker.csproj", "Tracker/"]
RUN dotnet restore "Tracker/Tracker.csproj"
COPY . Tracker
WORKDIR "/src/Tracker"
RUN dotnet build "Tracker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tracker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tracker.dll"]
