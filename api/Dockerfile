# syntax=docker/dockerfile:1

# ---- 1) build + publish ------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api/Api.csproj api/
RUN dotnet restore api/Api.csproj
COPY api/ api/
RUN dotnet publish api/Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- 2) small runtime image, non-root ---------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish ./
COPY uploads/ ./uploads/
RUN useradd -u 1001 -m appuser && chown -R appuser:appuser /app
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "TeamNotes.Api.dll"]
