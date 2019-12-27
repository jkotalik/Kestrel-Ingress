FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["Ingress.Controller/Ingress.Controller.csproj", "Ingress.Controller/"]
# COPY ["App/App.csproj", "App/"]
RUN dotnet restore Ingress.Controller/Ingress.Controller.csproj
# RUN dotnet restore App/App.csproj

COPY . .
WORKDIR "/src/Ingress.Controller"
RUN dotnet build "Ingress.Controller.csproj" -c Release -o /app/build

# WORKDIR "/src/App"
# RUN dotnet build "App.csproj" -c Release -o /app/build  

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ingress.Controller.dll"]
