FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS base
WORKDIR /app

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
ENTRYPOINT ["dotnet", "Ingress.Controller.dll"]
