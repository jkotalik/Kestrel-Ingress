FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["Ingress.Controller.csproj", "./"]
RUN dotnet restore "./Ingress.Controller.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Ingress.Controller.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ingress.Controller.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

WORKDIR /src
COPY ["../App/App.csproj", "./"]
RUN dotnet restore "./App.csproj"
COPY ../App /src/App
WORKDIR "/src/App"
RUN dotnet build "App.csproj" -c Release -o /app/App/build

FROM build AS publishApp
RUN dotnet publish "App.csproj" -c Release -o /app/App/publish

FROM base AS finalApp
WORKDIR /app/App
COPY --from=publish /app/App/publish .

ENTRYPOINT ["dotnet", "Ingress.Controller.dll"]
