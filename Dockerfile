FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MartenDbRecipes.sln global.json ./
COPY src/MartenDbRecipes/MartenDbRecipes/MartenDbRecipes.csproj src/MartenDbRecipes/MartenDbRecipes/
COPY src/MartenDbRecipes/MartenDbRecipes.Client/MartenDbRecipes.Client.csproj src/MartenDbRecipes/MartenDbRecipes.Client/
COPY src/MartenDbRecipes/MartenDbRecipes.Contracts/MartenDbRecipes.Contracts.csproj src/MartenDbRecipes/MartenDbRecipes.Contracts/
COPY tests/MartenDbRecipes.Tests/MartenDbRecipes.Tests.csproj tests/MartenDbRecipes.Tests/
RUN dotnet restore MartenDbRecipes.sln

COPY . .
RUN dotnet publish src/MartenDbRecipes/MartenDbRecipes/MartenDbRecipes.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MartenDbRecipes.dll"]
