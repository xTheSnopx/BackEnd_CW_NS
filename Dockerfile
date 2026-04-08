# Generador de Build (Versión estable .NET 9.0)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copiar el archivo de proyecto y restaurar dependencias
COPY *.csproj ./
RUN dotnet restore

# Copiar el resto del código y compilar la aplicación para producción
COPY ./ ./
RUN dotnet publish CWNS.BackEnd.csproj -c Release -o out

# Imagen base superligera para correr la aplicación
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

# Render dinámicamente asigna el puerto en produccion mediante la variable de entorno PORT.
# ASPNETCORE_HTTP_PORTS indicará a .NET en qué puertos escuchar solicitudes HTTP
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CWNS.BackEnd.dll"]
