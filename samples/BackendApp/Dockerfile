FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["kubetest.csproj", "./"]
RUN dotnet restore "./kubetest.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "kubetest.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "kubetest.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "kubetest.dll"]
