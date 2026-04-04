<!-- sync-hash: e34e95e62e4ae5299eec859813ab7f4b -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Qualitätssicherungsdokumentation: nem.Mimir

## Qualitätsstrategie
Das Repository nutzt gestaffelte Tests, um Domäneninvarianten, Application-Handler, Infrastructure-Adapter, API-Oberflächen und End-to-End-Verhalten zu validieren (inklusive MCP-Tool-Pipelines).

## Zusammensetzung der Testsuite
Unter `tests/` befinden sich:
- `nem.Mimir.Domain.Tests`
- `nem.Mimir.Application.Tests`
- `nem.Mimir.Infrastructure.Tests`
- `nem.Mimir.Api.Tests`
- `nem.Mimir.Api.IntegrationTests`
- `nem.Mimir.Sync.Tests`
- `nem.Mimir.Telegram.Tests`
- `nem.Mimir.Tui.Tests`
- `nem.Mimir.Integration.Tests`
- `nem.Mimir.E2E.Tests`

Das per Repo-Scan beobachtete Testdatei-Inventar ist groß (100+ Testdateien) und enthält spezialisierte Negativtestsuiten über mehrere Schichten hinweg.

## Zuordnung zur Testpyramide
### Unit-Tests
- Domänenentitäten/Value Objects/Events.
- Application-Commands/-Queries/-Behaviors.
- Infrastructure-Services (Sanitization, Context Window, LiteLLM-Serialisierung/Tool-Calls).

### Integrationstests
- API-Controller gegen Testhost.
- Repository-Verhalten gegen DB-gestützte Kontexte.
- Cross-Channel-Identitäts- und Channel-Integrationsfixtures.

### E2E-Tests
- Auth, Konversationen, Chat Completion.
- MCP-Tool-Pipeline und Fehlerbehandlung.
- Health Checks und Negativpfade bei Request-Validierung.

## Hochwertige Qualitätsbereiche
- Prompt Injection und Ablehnung fehlerhafter Eingaben.
- Isolation der Conversation-Ownership.
- Tool-Loop-Verhalten und Grenzen der maximalen Iterationen.
- MCP-Verbindungsfehler und sanfte Degradierung.
- SSE-Stream-Parsing und Fallback-Verhalten.

## Automatisierungsbefehle
- Build: `dotnet build nem.Mimir.slnx`
- Gesamttests: `dotnet test nem.Mimir.slnx`

Es ist kein eigener globaler Runner erforderlich; die Ausführung auf Solution-Ebene ist kanonisch.

## Empfohlene CI-Qualitätsgates
1. Build muss für alle Projekte erfolgreich sein.
2. Unit-, Integrations- und E2E-Tests müssen bestehen.
3. Sicherheitsrelevante Negativtests müssen bestehen (Validierung/Sanitization/MCP-Autorisierung).
4. Die Doc-Regeneration sollte einen Placeholder-Scan für generierte Doks enthalten.

## Risikobasierte Testmatrix
| Risiko | Auswirkung | Mitigierende Suite |
| :--- | :--- | :--- |
| Ungültige/schädliche Eingaben erreichen LLM/Tooling | Hoch | API-Negativtests + Sanitizer-Tests |
| Tool-Ausführung umgeht Governance | Hoch | MCP-Negativtests + Tool-Whitelist-Tests |
| Regression bei Datenisolation | Hoch | Conversation-Ownership-Tests + E2E-User-Boundary-Tests |
| Streaming-Instabilität | Mittel | ChatHub-Tests + SSE-Parser-Tests |
| Messaging-Regression | Mittel | Sync-Publisher-/Handler-Tests |

## Beobachtete Stärken
- Umfangreiche Negativtests über API, Domäne, Infra und MCP hinweg.
- Dedizierte Abdeckung für Tool-Calling-Serialisierung und MCP-Fehlermodi.
- Getrennte Testprojekte bewahren die Architekturgrenzen.

## Beobachtete Lücken, die beobachtet werden sollten
- Legacy-Branch-Drift gegenüber dem aktiven Typed-ID-Branch kann Annahmen im Zeitverlauf ungültig machen.
- Modell-/Katalog-Drift erfordert Regressionen rund um `ListModels` und Default-Auswahl.

## Querverweise
- [BUSINESS-LOGIC](./BUSINESS-LOGIC.md)
- [AI](./AI.md)
- [SECURITY](./SECURITY.md)
- [INFRASTRUCTURE](./INFRASTRUCTURE.md)
