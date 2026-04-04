<!-- sync-hash: 33da7bfd35caeb005c8863a529d73bb2 -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Infrastruktur-Dokumentation: nem.Mimir

## Laufzeit-Topologie
Die primäre Laufzeit wird per `docker-compose.yml` containerisiert und umfasst folgende Services:
- `mimir-api`
- `mimir-chat`
- `mimir-db` (PostgreSQL)
- `mimir-rabbitmq`
- `mimir-keycloak`
- `mimir-litellm`
- `mimir-telegram`
- `mimir-sandbox` (Build-Profil für das Codeausführungs-Image)

## Zentrale Infrastrukturabhängigkeiten
- PostgreSQL 16 für die Applikationspersistenz.
- RabbitMQ-Management-Image für den Wolverine-Transport.
- Keycloak für OIDC/JWT-Identität.
- LiteLLM-Proxy mit gemounteter `litellm_config.yaml`.
- Docker-Daemon-Zugriff für den Sandbox-Ausführungsdienst.

## Infrastrukturverdrahtung des API-Hosts
`Program.cs` und `DependencyInjection.cs` verdrahten:
- Npgsql DbContext und Interceptors
- LiteLLM HttpClient mit Retry- + Circuit-Breaker-Resilienz
- Wolverine-Messaging mit RabbitMQ-Durable-Patterns
- SignalR-Chat-Hub
- Health Checks für DB und LiteLLM-URL-Probe

## Layout der Deployment-Artefakte
- `docker/api/Dockerfile` für das API-Image.
- `docker/telegram/Dockerfile.telegram` für den Bot-Worker.
- `src/mimir-chat/Dockerfile` für das Next.js-Frontend.

## Konfigurationsquellen
Primäre Konfigurationsdateien:
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`
- Umgebungsvariablen (Compose und Host)

Kritische Abschnitte:
- `LiteLlm:*`
- `ConnectionStrings:DefaultConnection`
- `RabbitMQ:ConnectionString`
- JWT-/Keycloak-Werte
- erlaubte CORS-Origins

## Kapazitäts- und Ressourcenbeschränkungen
Compose-seitige Limits umfassen:
- `mimir-api`: Speicher- + PID-Limits
- `mimir-telegram`: Speicher/CPU/PID-Limits und schreibgeschütztes Dateisystem

Sandbox-Annahmen:
- One-Shot-Ausführung in Containern
- keine offenen Ports
- kein Netzwerkmodus in Ausführungscontainern (durch das Laufzeitverhalten des Sandbox-Services erzwungen)

## Health und Observability
- `/health`-Endpunkt mit DB- und LiteLLM-Probes.
- Serilog-Request- und Anwendungs-Logging.
- Correlation-ID-Middleware für Trace-Verknüpfung.
- Background-Health-Reporter publiziert `ServiceHealthReport` via Wolverine.

## Operative Laufzeitmuster
### Lokaler Start
`docker compose up -d` mit konfigurierten Umgebungswerten.

### Build/Test
- `dotnet build nem.Mimir.slnx`
- `dotnet test nem.Mimir.slnx`

### MCP-Runtime-Verhalten
- Aktivierte MCP-Server-Konfigurationen werden beim Start automatisch verbunden.
- Der Config-Change-Listener pollt und erneuert Verbindungen.

## Infrastrukturrisiken und Vorbehalte
- Die Legacy-/Active-Branch-Trennung kann zu Drift in Deployment-Annahmen führen.
- Modellnamen im Code und in der LiteLLM-Konfiguration können auseinanderlaufen.
- Dockerfile-Referenzen verwenden in der Repo-Historie sowohl `.sln`- als auch `.slnx`-Konventionen; Release-Skripte sollten konsistent gehalten werden.

## Wiederherstellungs- und Rollback-Hinweise
- Die DB-Wiederherstellung ist extern zur App; die App unterstützt Soft-Delete-Restore für ausgewählte Entitäten.
- Service-Rollback erfolgt in Compose-/Kubernetes-ähnlichen Deployments per Image-Tag.
- MCP-Fehlkonfigurationen lassen sich durch Deaktivieren spezifischer Server-Konfigurationen isolieren.

## Querverweise
- [ARCHITECTURE](./ARCHITECTURE.md)
- [SECURITY](./SECURITY.md)
- [COMPLIANCE](./COMPLIANCE.md)
- [USER-MANUAL](./USER-MANUAL.md)
