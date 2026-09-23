# =========================================================
# Stage 1: Build
# =========================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY ["src/Lowcord.Server/Lowcord.Server.csproj", "Lowcord.Server/"]
RUN dotnet restore "Lowcord.Server/Lowcord.Server.csproj"

# Copiar el resto del código y compilar en modo Release
COPY src/Lowcord.Server/ Lowcord.Server/
WORKDIR "/src/Lowcord.Server"
RUN dotnet publish "Lowcord.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =========================================================
# Stage 2: Runtime ultra-ligero (Alpine Linux ~100MB)
# =========================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Copiar binarios publicados desde el stage de compilación
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Lowcord.Server.dll"]
