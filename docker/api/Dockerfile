# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS builder
WORKDIR /app

# Copy solution and project files
COPY *.sln ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/nem.Mimir.Domain/nem.Mimir.Domain.csproj src/nem.Mimir.Domain/
COPY src/nem.Mimir.Application/nem.Mimir.Application.csproj src/nem.Mimir.Application/
COPY src/nem.Mimir.Infrastructure/nem.Mimir.Infrastructure.csproj src/nem.Mimir.Infrastructure/
COPY src/nem.Mimir.Sync/nem.Mimir.Sync.csproj src/nem.Mimir.Sync/
COPY src/nem.Mimir.Api/nem.Mimir.Api.csproj src/nem.Mimir.Api/

# Restore dependencies
RUN dotnet restore src/nem.Mimir.Api/nem.Mimir.Api.csproj

# Copy remaining source code
COPY src/ src/

# Build
RUN dotnet build src/nem.Mimir.Api/nem.Mimir.Api.csproj -c Release

# Publish
RUN dotnet publish src/nem.Mimir.Api/nem.Mimir.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

COPY --from=builder /app/publish .

EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000
# Performance: Use Server GC for better throughput
ENV DOTNET_GCHeapHardLimit=1
ENV DOTNET_GCHeapHardLimit_SO=1
ENV DOTNET_GCTuningRefreshIntervalMS=10000
# Enable Tiered Compilation for faster JIT
ENV DOTNET_TieredCompilation="1"
ENV DOTNET_TieredCompilationQuickJit="1"
ENTRYPOINT ["dotnet", "nem.Mimir.Api.dll"]
