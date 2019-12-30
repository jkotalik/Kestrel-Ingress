FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["Ingress.Controller/Ingress.Controller.csproj", "Ingress.Controller/"]
COPY ["Ingress/Ingress.csproj", "Ingress/"]
RUN dotnet restore Ingress.Controller/Ingress.Controller.csproj
RUN dotnet restore Ingress/Ingress.csproj

COPY . .
RUN dotnet build "Ingress.Controller/Ingress.Controller.csproj" -c Release -o /app/build
RUN dotnet build "Ingress/Ingress.csproj" -c Release -o /app/build  

FROM build AS publish
RUN dotnet publish Ingress.Controller/Ingress.Controller.csproj -c Release -o /app/publish
RUN dotnet publish Ingress/Ingress.csproj -c Release -o /app/Ingress/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /app/Ingress/publish Ingress/
ENTRYPOINT ["dotnet", "Ingress.Controller.dll"]
