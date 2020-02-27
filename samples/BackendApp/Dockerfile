FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["BackendApp.csproj", "./"]
RUN dotnet restore "./BackendApp.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "BackendApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BackendApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BackendApp.dll"]
