# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CreditosApp/CreditosApp.csproj CreditosApp/
RUN dotnet restore CreditosApp/CreditosApp.csproj
COPY . .
RUN dotnet publish CreditosApp/CreditosApp.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Render inyecta $PORT: se expande aqui (no dentro de una variable de entorno).
CMD ["/bin/sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet CreditosApp.dll"]
