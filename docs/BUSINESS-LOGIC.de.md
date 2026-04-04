<!-- sync-hash: a3a30b5c566abaf1cc0ee982cdd1fdb7 -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Geschäftlogik-Dokumentation: nem.Mimir

## Zweck
`nem.Mimir` implementiert Konversations-Workflows für authentifizierte Nutzer über Web-, Terminal- und Bot-Kanäle, inklusive Modellinferenz, Prompt-Governance und optionaler Tool-Ausführung.

## Zentrale Regeldomänen
1. Verwaltung des Konversationslebenszyklus.
2. Nachrichtensendung und Generierung von Assistant-Antworten.
3. Verwaltung und Rendering von Prompt-Templates.
4. Ermittlung des Modellkatalogs und Statusprüfungen.
5. Governance für Plugins und MCP-Tools.
6. Administrative Operationen und Wiederherstellung logisch gelöschter Entitäten.

## Regelinventar (code-zugeordnet)
- **Besitzregel**: Nur der Besitzer einer Konversation darf sie sehen, senden, aktualisieren, archivieren oder löschen (`SendMessageCommand`, Konversationsabfragen).
- **Archivierungsregel**: Nach dem Archivieren können keine Nachrichten mehr hinzugefügt werden (`Conversation.AddMessage`).
- **Eingabebegrenzung**: Validatoren erzwingen Modellnamensmuster, maximale Inhaltslänge und tokenbezogene Bereiche.
- **Prompt-Template-Regel**: Prompt-Name und Template dürfen nicht leer sein (`SystemPrompt` + Validatoren).
- **Tool-Loop-Regel**: Maximal 5 Tool-Iterationen in `SendMessageCommand`.
- **MCP-Privilegregel**: MCP-Admin-Endpunkte erfordern die Policy `RequireAdmin`.

## Haupt-Workflows
### Konversation anlegen
1. API empfängt `CreateConversationRequest`.
2. Der Command-Handler löst den aktuellen Benutzer auf.
3. Die Domäne erstellt `Conversation` mit aktivem Status und Standard-Einstellungen.
4. Das Repository speichert das Aggregate.

### Nachricht senden (native API)
1. Request-Validierung für Inhalts- und Modellbeschränkungen.
2. Besitzprüfung für die Zielkonversation.
3. Aufbau des Kontextfensters aus Systemprompt + Historie + neuer Nachricht.
4. Die Benutzernachricht wird angehängt.
5. Falls Tools verfügbar sind, läuft der Tool-Loop; andernfalls direkter Stream-Pfad.
6. Die Assistant-Nachricht wird mit geschätzter Tokenanzahl persistiert.

### OpenAI-kompatible Chat Completion
1. `/v1/chat/completions` validiert ein OpenAI-ähnliches Payload.
2. Der Streaming-Pfad sendet SSE-Chunks und das Marker `[DONE]`.
3. Der Nicht-Streaming-Pfad nutzt `CreateChatCompletionCommand`.
4. Chat-Lifecycle-Events werden an Wolverine publiziert.

### Systemprompt rendern
1. Prompt-Template per ID abrufen.
2. `SystemPromptService.RenderTemplate` ersetzt `{{variable}}`-Tokens.
3. Das gerenderte Ergebnis wird zurückgegeben, ohne die Template-Daten zu verändern.

## Messaging-Semantik
- Zu den Event-Publisher-Methoden gehören:
  - `PublishChatRequestAsync`
  - `PublishChatCompletedAsync`
  - `PublishMessageSentAsync`
  - `PublishConversationCreatedAsync`
  - `PublishAuditEventAsync`
- Die Zustellgarantien beruhen auf Wolverine Durable Inbox/Outbox.

## Semantik der Tool-Ausführung
- Tools werden über den Composite-Provider aus Plugins und MCP-Servern bereitgestellt.
- MCP-Tools werden per Whitelist gefiltert und kollisionsbehandelt.
- Tool-Fehler werden in Content-Payloads umgewandelt und zurück in den Modellkontext gespeist.

## Validierung und Fehlerverhalten
- FluentValidation-Pipelines laufen vor den Handlern.
- Domäneninvarianten werfen früh bei ungültigen Zustandswechseln.
- API-Level-Ausnahmen werden durch globale Fehlerbehandlung in ProblemDetails-ähnliche Antworten gemappt.

## Matrix der Geschäftsregeln
| Rule ID | Beschreibung | Erzwingt durch | Kritikalität |
| :--- | :--- | :--- | :--- |
| BR-001 | Nutzer darf nur auf eigene Konversationen zugreifen | Conversation-Handler + Benutzerkontext | Kritisch |
| BR-002 | Archivierte Konversationen sind für neue Nachrichten unveränderlich | `Conversation.AddMessage` | Hoch |
| BR-003 | Prompt- und Nachrichten-Payloads müssen Injection-bewusste Validierung bestehen | API-Validatoren + Sanitization | Kritisch |
| BR-004 | Tool-Ausführungsrekursion ist begrenzt, um unendliche Schleifen zu verhindern | `SendMessageCommand.MaxToolIterations` | Hoch |
| BR-005 | MCP-Konfigurationsänderungen dürfen nur durch Admins erfolgen | `McpServersController` + Policy | Hoch |
| BR-006 | Logisch gelöschte Entitäten dürfen nur über den Admin-Flow wiederhergestellt werden | Wiederherstellungs-Command im `AdminController` | Mittel |

## Operativ relevante Lücken
- Die Defaults sind nicht vollständig angeglichen (`ConversationSettings.Default` vs. Runtime-Modell-Defaults).
- Der Reasoner-Agent-Memory-Pfad ist im Speicher und nicht persistent im Dokumentenspeicher.

## Querverweise
- [AI](./AI.md)
- [ARCHITECTURE](./ARCHITECTURE.md)
- [DATA-MODEL](./DATA-MODEL.md)
- [QA](./QA.md)
