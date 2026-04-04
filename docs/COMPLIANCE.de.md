<!-- sync-hash: bb6c99c4f441ea3ac9fbf7bb02403704 -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Compliance-Dokumentation: nem.Mimir

## Umfang
Dieses Dokument beschreibt implementierte Kontrollen im Legacy-Dienst `nem.Mimir`. Es behauptet keine Kontrollen, die nicht im Code oder in der Konfiguration vorhanden sind.

Im Scope enthaltene Oberflächen:
- authentifizierte API- und SignalR-Chat-Endpunkte
- Persistenz von Konversationen/Nachrichten/Benutzern/Prompts/MCP-Telemetrie
- Audit- und Admin-Operationen
- Integration für sandboxierte Codeausführung

## Zuordnung zu Standards (implementierte Evidenz)
| Kontrollbereich | Framework-Zuordnung | Implementierungsartefakt |
| :--- | :--- | :--- |
| Authentifizierter Zugriff | GDPR Art. 32 / SOC2 CC6 | JWT-Auth-Setup + Policies in `Program.cs` und Auth-Extensions |
| Eingabevalidierung | SOC2 CC7 | FluentValidation-Pipeline + Endpoint-Validatoren |
| Logging von Sicherheitsereignissen | SOC2 CC7 / ISO 27001 A.12 | `AuditEntry`, `AuditService`, Wolverine-Audit-Events |
| Admin-Aktionen mit Least Privilege | SOC2 CC6 | `RequireAdmin`-Policy auf Admin- und MCP-Config-Controllern |
| Tool-Governance | SOC2 CC8 | MCP-Whitelist + Tool-Audit-Log-Entitäten/-Services |
| Datenlebenszyklus-Kontrollen | GDPR Art. 5/17 | Soft-Delete mit Wiederherstellungs-Command-Pfaden |

## Daten-Governance
Vom Service-Logikpfad verarbeitete Datenklassen:
- Konversationstext und Prompt-Inhalte
- Benutzeridentitätsmetadaten aus JWT-Claims
- administrative Audit-Datensätze
- MCP-Tool-Ein-/Ausgabe-Telemetrie

Implementierte Governance-Maßnahmen:
- nutzerbezogene Zugriffskontrollen in Konversations-/Nachrichten-Handlern
- Soft-Delete-Flags und Query-Filter für zentrale Entitäten
- Admin-only-Wiederherstellungsendpunkte für kontrollierte Recovery

## Auditierbarkeit und Evidenz
Evidenz erzeugende Komponenten:
- Persistenzmodell `AuditEntry`
- `MimirEventPublisher` + `AuditEventHandler`
- Request-Correlation-Middleware für Traceability-Verknüpfung
- MCP-Tool-Audit-Logger (`McpToolAuditLog`)

## Aufbewahrung und Löschung
Aktuelles Verhalten im Code:
- Soft-Delete wird für `Conversation`, `User`, `SystemPrompt` verwendet.
- Ein Restore-Endpunkt ermöglicht die Rückgängigmachung für admingeführte Recovery.

Was hier nicht als explizite Policy-Logik implementiert ist:
- fester Retention-Scheduler mit gesetzlichen Aufbewahrungsfristen.
- automatisierte Orchestrierung von Betroffenenanfragen.

## Drittanbieter- und Abhängigkeitslage
Betriebsabhängigkeiten mit Relevanz für Compliance:
- Keycloak (Identität)
- PostgreSQL (Primärdatenspeicher)
- RabbitMQ (Nachrichten-Transport)
- LiteLLM und Upstream-Model-Provider

Eine separate Notiz zu Abhängigkeiten/Lizenzen/Schwachstellen existierte früher (`compliance-audit.md`); diese Regeneration fokussiert auf repository-natives Kontrollverhalten.

## Zusammenfassung des Risikoregisters
| Risiko | Verbleibendes Niveau | Vorhandene Minderung |
| :--- | :--- | :--- |
| Prompt-/Datenabfluss durch schädliche Eingaben | Mittel | Validatoren + Sanitization + Auth-Grenzen |
| Zu weitreichende Tool-Berechtigungen über MCP | Mittel | Default-Deny-Whitelist und Audit-Logging |
| Legacy-Modell-/Config-Drift | Mittel | Explizite Defaults und Runtime-Modellprüfungen |
| Missbrauch von Soft-Delete / nicht gelöschte sensible Daten | Mittel | Admin-Controls für Restore + Audit-Trail |

## Verifikations-Checkpoints
- Auth-Policy auf allen privilegierten Endpunkten validieren.
- Prüfen, dass für Admin-Aktionen und Tool-Ausführungen Audit-Einträge erstellt werden.
- Conversation-Ownership und Isolierung in API-/E2E-Suiten prüfen.
- Sanitizer- und Validator-Negativtests müssen weiterhin bestehen.

## Dokument-Governance
Dieses Compliance-Snapshot ist an den Code im Legacy-Branch `nem.Mimir` gebunden und sollte aktualisiert werden, sobald sich Folgendes ändert:
- MCP-Policy-Verhalten,
- Auth-Policy-Wiring,
- Soft-Delete-/Restore-Verhalten,
- neue Datenklassen.

## Querverweise
- [SECURITY](./SECURITY.md)
- [DATA-MODEL](./DATA-MODEL.md)
- [QA](./QA.md)
- [AI](./AI.md)
