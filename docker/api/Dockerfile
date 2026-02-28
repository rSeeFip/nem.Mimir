# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS builder
WORKDIR /app

# Copy solution and project files
COPY *.sln ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/Mimir.Domain/Mimir.Domain.csproj src/Mimir.Domain/
COPY src/Mimir.Application/Mimir.Application.csproj src/Mimir.Application/
COPY src/Mimir.Infrastructure/Mimir.Infrastructure.csproj src/Mimir.Infrastructure/
COPY src/Mimir.Sync/Mimir.Sync.csproj src/Mimir.Sync/
COPY src/Mimir.Api/Mimir.Api.csproj src/Mimir.Api/

# Restore dependencies
RUN dotnet restore

# Copy remaining source code
COPY src/ src/
COPY tests/ tests/

# Build
RUN dotnet build --no-restore -c Release

# Publish
RUN dotnet publish src/Mimir.Api/Mimir.Api.csproj --no-restore -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

COPY --from=builder /app/publish .

EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000
ENTRYPOINT ["dotnet", "Mimir.Api.dll"]
