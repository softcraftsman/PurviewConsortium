FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PurviewConsortium.sln .
COPY src/PurviewConsortium.Core/*.csproj src/PurviewConsortium.Core/
COPY src/PurviewConsortium.Infrastructure/*.csproj src/PurviewConsortium.Infrastructure/
COPY src/PurviewConsortium.Api/*.csproj src/PurviewConsortium.Api/
RUN dotnet restore src/PurviewConsortium.Api/PurviewConsortium.Api.csproj

COPY . .
WORKDIR /src/src/PurviewConsortium.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PurviewConsortium.Api.dll"]
