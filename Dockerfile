FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/FloodShield.Shelters.Api/FloodShield.Shelters.Api.csproj src/FloodShield.Shelters.Api/
RUN dotnet restore src/FloodShield.Shelters.Api/FloodShield.Shelters.Api.csproj
COPY src/ src/
RUN dotnet publish src/FloodShield.Shelters.Api -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Em produção defina via variáveis de ambiente: ConnectionStrings__Default, Jwt__Key, Seed__AdminPassword
ENTRYPOINT ["dotnet", "FloodShield.Shelters.Api.dll"]
