## Multi-stage build for VIP_GATERING.WebUI (ASP.NET Core 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy csproj files and restore as distinct layers
COPY VIP_GATERING.sln ./
COPY src/VIP_GATERING.Domain/VIP_GATERING.Domain.csproj src/VIP_GATERING.Domain/
COPY src/VIP_GATERING.Application/VIP_GATERING.Application.csproj src/VIP_GATERING.Application/
COPY src/VIP_GATERING.Infrastructure/VIP_GATERING.Infrastructure.csproj src/VIP_GATERING.Infrastructure/
COPY src/VIP_GATERING.WebUI/VIP_GATERING.WebUI.csproj src/VIP_GATERING.WebUI/
COPY src/VIP_GATERING.Tests/VIP_GATERING.Tests.csproj src/VIP_GATERING.Tests/

RUN dotnet restore VIP_GATERING.sln

# Copy the rest of the source (excluding bin/obj via .dockerignore)
COPY src/ src/

# Publish the web app
RUN dotnet publish src/VIP_GATERING.WebUI/VIP_GATERING.WebUI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Set a predictable URL binding inside the container
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

# Run as non-root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser \
    && chown -R appuser:appgroup /app
USER appuser

# Expose HTTP port
EXPOSE 8080

ENTRYPOINT ["dotnet", "VIP_GATERING.WebUI.dll"]
