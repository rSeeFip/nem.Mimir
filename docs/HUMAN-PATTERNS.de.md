<!-- sync-hash: eeb47f4d9b38c76f3b813db288423fc1 -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Dokumentation zu menschlichen Mustern: nem.Mimir

## Team-Konventionen in diesem Repository
- Behandeln Sie `nem.Mimir` als **Legacy-Wartungsbranch**.
- Halten Sie Architekturentscheidungen konsistent mit dem bestehenden Dual-Stack-Muster (MediatR + Wolverine).
- Bevorzugen Sie Repository-Interfaces und Command-/Query-Handler gegenüber controller-lastiger Logik.
- Halten Sie Validierung in FluentValidation und Domäneninvarianten, nicht in ad-hoc Controller-Prüfungen.

## Branching- und Change-Policy
- Neue feature-orientierte Arbeit sollte auf `nem.Mimir-typed-ids` zielen.
- Änderungen in diesem Branch sollten auf Wartung, Stabilisierung und Dokumentationsparität beschränkt sein.
- Cross-Repo-Abhängigkeitsänderungen (KnowHub-Agents/Contracts) sollten auf Kompatibilitätsauswirkungen geprüft werden.

## Code-Review-Checkliste
1. Bewahrt die Request-Verarbeitung Ownership- und Auth-Grenzen?
2. Sind Prompt- und Message-Eingaben validiert und sanitisiert?
3. Werden Tool-Calls über Whitelist-/Audit-Decorator abgesichert?
4. Sind Wolverine-Messages weiterhin durch Durable Inbox/Outbox geschützt?
5. Bewahrt die Änderung das OpenAI-kompatible Verhalten auf `/v1/*`?
6. Sind Tests in den relevanten Testprojekten ergänzt/aktualisiert?

## Onboarding-Reihenfolge für Engineers
Empfohlene Lesereihenfolge:
1. `src/nem.Mimir.Api/Program.cs`
2. `OpenAiCompatController` und `MessagesController`
3. `SendMessageCommand` und `ContextWindowService`
4. `LiteLlmClient` + LiteLLM-DTO-Modelle
5. MCP-Stack (`McpClientManager`, `McpToolProvider`, Controller)
6. Security-/Sanitization-Middleware und Validatoren

## Entwicklungs-Workflow
- Verhalten zuerst in Application oder Infrastructure implementieren.
- API-Endpunkte erst verdrahten, wenn das Handler-/Service-Verhalten stabil ist.
- Unit-Tests sowie relevante Integrations-/E2E-Tests ergänzen.
- `dotnet build` und `dotnet test` für betroffene Projekte bzw. die Solution verifizieren.
- Dokumentation mit konkreten Codepfaden synchron halten.

## Policy für KI-gestützte Entwicklung
Erlaubt:
- Entwurfsdokumente aus verifizierten Code-Referenzen generieren,
- Testfälle für Validator- und Tool-Loop-Ecken vorschlagen,
- Endpunktverhalten aus Controller-/Handler-Quellen zusammenfassen.

Nicht erlaubt:
- nicht dokumentierte Modell-/Provider-Annahmen einführen,
- generierten Code ohne Validierung gegen die bestehende Architektur übernehmen,
- Sicherheitsprüfungen zugunsten schnellerer Implementierung umgehen.

## Muster für Wissenssicherung
- ADRs für Architekturwechsel verwenden (die Legacy-ADR-Historie bleibt hilfreicher Kontext).
- Betriebs- und Compliance-Claims evidenzbasiert und branch-spezifisch halten.
- Bekannte Abweichungen (z. B. Modell-Default-Drift, Legacy-vs-Active-Branch-Split) explizit festhalten.

## Praktische Kollaborationsgrenzen
- API, App, Domain, Infrastructure und Sync haben klare Zuständigkeitsgrenzen.
- MCP-Admin-/Config-Änderungen erfordern Aufmerksamkeit eines Security-Reviewers.
- Änderungen, die Authentifizierung oder Sanitization betreffen, brauchen sicherheitsfokussierte Review.

## Fokus der kontinuierlichen Verbesserung
- Drift zwischen Config-/Modellnamen in Code-Konstanten und LiteLLM-Konfiguration reduzieren.
- Typed-ID-Migrationsarbeit im aktiven Branch priorisieren und Legacy-Stabilität bewahren.
- Negativtestabdeckung für Injection-/Tool-Missbrauchsszenarien hoch halten.

## Querverweise
- [ARCHITECTURE](./ARCHITECTURE.md)
- [AI](./AI.md)
- [QA](./QA.md)
- [SECURITY](./SECURITY.md)
