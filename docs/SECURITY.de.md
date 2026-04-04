<!-- sync-hash: 46bf7a0dc8ff7e6e421ef039e9fdccfe -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Sicherheitsdokumentation: nem.Mimir

## Sicherheitsüberblick
Sicherheitskontrollen sind in API-Middleware/-Policies, Validator-Pipelines, Sanitization-Services und Tool-Governance-Komponenten implementiert.

Wesentliche Vertrauensgrenzen:
- Client -> API/SignalR
- API -> LiteLLM-Proxy
- API/Sync -> RabbitMQ
- API -> PostgreSQL
- API -> MCP-Server

## Authentifizierung und Autorisierung
- Authentifizierungsmodi:
  - Keycloak-basierte Auth über gemeinsame Contracts-Integration.
  - `StandaloneMode`-Development-Handler für lokale Admin-only-Einrichtung.
- Autorisierungs-Policies in `Program.cs`:
  - `RequireAdmin`
  - `RequireUser`
- MCP-Server-Management-Endpunkte sind nur für Admins zugänglich.

## Transport- und Perimeter-Kontrollen
- CORS-Policy mit konfigurierter Origin-Allowlist.
- Sicherheitsheader werden in Middleware ergänzt:
  - `X-Content-Type-Options`
  - `X-Frame-Options`
  - CSP und verwandte Header
- HSTS + HTTPS-Redirect sind außerhalb der Entwicklung aktiv.

## Eingabevalidierung und Sanitization
Validierungsebenen:
- FluentValidation-Behavior in der MediatR-Pipeline.
- Endpunkt-spezifische Validatoren für Konversationen/Nachrichten/Prompts/OpenAI-Requests.

Sanitization-Ebenen:
- `SanitizationService` entfernt gefährliche Tags, Handler und gängige Prompt-Injection-Marker.
- `OutputSanitizationMiddleware` loggt verdächtige Muster auf nachrichtenbezogenen Routen.

## Ratenbegrenzung und Missbrauchsprävention
- Fixed-Window-Per-User-Limiter: 100 Requests pro Minute.
- Maximale SignalR-Nachrichtengröße begrenzt (256 KB).
- Kestrel-Maximalgröße für Request Bodies auf 10 MB gesetzt.

## Tool- und MCP-Sicherheit
- Tool-Zugriff folgt einer Default-Deny-Whitelist pro MCP-Server.
- Tool-Ausführung wird auditierbar gemacht (`McpToolAuditLog`) mit Latenz, Erfolg/Fehler und Payload-Metadaten.
- `McpToolProvider` validiert die Whitelist zur Laufzeit erneut (Defense in Depth).

## Kontrollen für Sandbox-Ausführung
Der Codeausführungs-Endpunkt delegiert an den Sandbox-Service mit Docker-Isolationsannahmen und einem eingeschränkten Ausführungsprofil.
Repository-Infrastruktur und Compose-Konfiguration erzwingen Non-Root-/Read-Only-/Ressourcenlimit-Muster für zugehörige Worker.

## Datenschutz-Posture
- Zugriff auf Benutzer und Konversationen ist in den Handlern owner-scope-basiert.
- Soft-Delete und Audit-Metadaten erhalten die operative Nachvollziehbarkeit.
- Secrets werden über den Environment-/OpenBao-Integrationspfad erwartet; sie sind nicht hardcodiert in Handlern.

## Beobachtbarkeit mit Incident-Relevanz
- Correlation-ID-Middleware ermöglicht Request-Traceability.
- Strukturiertes Logging via Serilog.
- Ein Health-Report-Emitter publiziert Service-Health-Metriken über den Message Bus.

## STRIDE-Snapshot
| Threat | Primäre Minderung im Code |
| :--- | :--- |
| Spoofing | JWT-Auth, Policy-Checks |
| Tampering | Validatoren + Sanitization + kontrollierte Persistenz |
| Repudiation | Audit-Einträge + Event-Publikation + Correlation IDs |
| Information Disclosure | Ownership-Checks + Admin-Policy-Grenzen |
| Denial of Service | Rate Limiting + Body-/Message-Size-Limits |
| Elevation of Privilege | rollenbasierte Policies + geschützte Admin-Controller |

## Bekannte Einschränkungen
- Das Legacy-Identitätsmodell hängt in mehreren Pfaden weiterhin von primitiven IDs ab.
- Die Sicherheitslage unterscheidet sich zwischen Standalone-Modus und produktivem Auth-Modus; Standalone ist nur für lokale Entwicklung gedacht.

## Querverweise
- [COMPLIANCE](./COMPLIANCE.md)
- [AI](./AI.md)
- [QA](./QA.md)
- [INFRASTRUCTURE](./INFRASTRUCTURE.md)
