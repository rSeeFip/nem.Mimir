FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS builder
WORKDIR /src

COPY ["nem.Contracts/", "nem.Contracts/"]
COPY ["nem.KnowHub/", "nem.KnowHub/"]
COPY ["nem.Mimir-typed-ids/", "nem.Mimir-typed-ids/"]

WORKDIR /src/nem.Mimir-typed-ids
RUN dotnet restore "src/nem.Mimir.Api/nem.Mimir.Api.csproj"
RUN dotnet publish "src/nem.Mimir.Api/nem.Mimir.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

RUN apk add --no-cache curl

COPY --from=builder /app/publish .

EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000
ENTRYPOINT ["dotnet", "nem.Mimir.Api.dll"]
