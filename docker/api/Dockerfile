FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS builder
WORKDIR /src

COPY ["NuGet.Config", "./NuGet.Config"]
COPY ["nem.Contracts/", "nem.Contracts/"]
COPY ["nem.KnowHub/", "nem.KnowHub/"]
COPY ["nem.KnowHub-arachne/", "nem.KnowHub-arachne/"]
COPY ["nem.MCP/", "nem.MCP/"]
COPY ["nem.Mimir-typed-ids/", "nem.Mimir-typed-ids/"]

WORKDIR /src/nem.Mimir-typed-ids
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/nem.Mimir.Api/nem.Mimir.Api.csproj" --configfile /src/NuGet.Config --disable-parallel
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/nem.Mimir.Api/nem.Mimir.Api.csproj" -c Release -o /app/publish --no-restore -p:RunAnalyzers=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

RUN apk add --no-cache curl

COPY --from=builder /app/publish .

EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000
ENTRYPOINT ["dotnet", "nem.Mimir.Api.dll"]
