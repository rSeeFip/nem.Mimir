<!-- sync-hash: ac08f39e1ab54791f6338b5bdba4009b -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
last_verified_against: src/* (legacy branch)
---

# KI-Dokumentation: nem.Mimir

## Umfang und Branch-Status
`nem.Mimir` ist der Legacy-Dienst für Konversations-KI. Der aktive Refactor-Branch ist `nem.Mimir-typed-ids`, doch dieses Dokument beschreibt bewusst das kanonische Legacy-Repository.

Der zentrale KI-Pfad ist implementiert in:
- `src/nem.Mimir.Api/Controllers/OpenAiCompatController.cs`
- `src/nem.Mimir.Application/Conversations/Commands/SendMessage.cs`
- `src/nem.Mimir.Infrastructure/LiteLlm/LiteLlmClient.cs`
- `src/nem.Mimir.Infrastructure/Services/ContextWindowService.cs`

## LLM-Routing-Architektur (code-verifiziert)
Mimir leitet sämtliche Modellinferenz über `ILlmService`, implementiert durch `LiteLlmClient`.

Routing-Ebenen:
1. **API-Transportebene**: `/v1/chat/completions` akzeptiert OpenAI-kompatible Requests.
2. **Applikations-Command-Ebene**: `CreateChatCompletionCommand` und `SendMessageCommand` bauen anbieterneutrale `LlmMessage`-Listen auf.
3. **Provider-Adapter-Ebene**: `LiteLlmClient` konvertiert Domänenmeldungen in OpenAI-kompatibles JSON für LiteLLM.
4. **Provider-Proxy-Ebene**: LiteLLM leitet an konkrete Modelle weiter, die in `litellm_config.yaml` definiert sind.

Im Runtime-Pfad des Backends wird kein direktes OpenAI/Azure/Anthropic SDK verwendet; der Provider-Wechsel erfolgt über den Proxy.

## Inferenz-Pipeline
### Nicht-streamende Pipeline
- Einstieg: `OpenAiCompatController.HandleNonStreamingResponse`
- Command: `CreateChatCompletionCommand`
- Ausführung: `ILlmService.SendMessageAsync`
- Rückgabe: `ChatCompletionResult` mit Nutzung (`prompt_tokens`, `completion_tokens`, `total_tokens`)

### Streaming-Pipeline
- Einstieg: `OpenAiCompatController.HandleStreamingResponse` (SSE)
- Ausführung: `ILlmService.StreamMessageAsync`
- Parsing: `SseStreamParser`
- Verhalten: gibt segmentierte `ChatCompletionChunk` aus und anschließend `[DONE]`

### Pipeline für Konversations-Commands
- Einstieg: `MessagesController.Send`
- Command: `SendMessageCommand`
- Kontextaufbau: `ContextWindowService.BuildLlmMessagesAsync`
- Verzweigung:
  - wenn keine Tools vorhanden sind: `StreamResponseAsync`
  - wenn Tools verfügbar sind: `ExecuteToolLoopAsync` mit maximal 5 Iterationen

## Tool-Calling und MCP-Orchestrierung
`SendMessageCommand` unterstützt Function-/Tool-Calling über `ToolDefinition` und `LlmToolCall`.

Ausführungsmodell:
- `ILlmService.SendMessageAsync(model, messages, tools)` kann `ToolCalls` zurückgeben.
- Die Applikation führt Tools über `IToolProvider` aus.
- Tool-Ergebnisse werden als `role="tool"`-Nachrichten angehängt und erneut an das Modell gesendet.

Die Provider-Zusammensetzung befindet sich in `Infrastructure/DependencyInjection.cs`:
- `PluginToolProvider` (Legacy-Plugin-Tools)
- `McpToolProvider` (verbundene MCP-Server)
- `CompositeToolProvider` + `AuditingToolProviderDecorator`

MCP-Steuerungen werden über Folgendes umgesetzt:
- `McpClientManager` (stdio/sse/streamable-http-Transport)
- `McpClientStartupService` (Auto-Connect aktivierter Konfigurationen)
- `McpConfigChangeListener` + `McpConfigChangeHandler` (Runtime-Refresh)
- Whitelist-Erzwingung und Audit-Logging für Tool-Ausführungen

## Prompt-Management
Prompt-Entitäten und Services:
- Domäne: `SystemPrompt` (name/template/default/active)
- Repository: `SystemPromptRepository`
- API: `SystemPromptsController`
- Rendering: `SystemPromptService.RenderTemplate` (Regex-Substitution)

Runtime-Prompt-Auflösung für den Chat-Kontext:
- `ContextWindowService` löst den Standardprompt aus der DB auf (`GetDefaultAsync`)
- Fallback-Konstante: `"You are Mimir, a helpful AI assistant."`

Prompt-Schutzmechanismen:
- API-Validatoren (`OpenAiCompatValidators`, `MessageValidators`, `ConversationValidators`) enthalten Schutz vor Prompt Injection.
- `SanitizationService` entfernt bekannte Instruction-Hijack-Marker und gefährliche HTML-/Script-Muster.

## Behandlung des Konversationskontexts
Hauptstrategie für LLM-Requests:
- Merging-Reihenfolge in `ContextWindowService`:
  1. Systemnachricht
  2. historische Konversationsnachrichten (nach `CreatedAt` sortiert)
  3. aktuelle Benutzernachricht
- Token-Budgetierung per Zeichenheuristik `(len + 3) / 4`
- Älteste Historie wird gekürzt, bis das Token-Limit des Modells eingehalten ist.

Modell-Token-Limits im Code:
- `phi-4-mini`: 16,384
- `qwen-2.5-72b`: 131,072
- `qwen-2.5-coder-32b`: 131,072

## Modell-Auswahlstrategie
Die Auswahl erfolgt auf mehreren Ebenen:
- Auf Request-Ebene: Der Aufrufer kann `model` setzen.
- Applikations-Default: `SendMessageCommand` verwendet ohne Angabe `phi-4-mini`.
- LiteLLM-Adapter-Default: `LiteLlmClient` fällt auf `LiteLlmOptions.DefaultModel` zurück.
- Kontextkürzung nutzt `ContextWindowService.GetTokenLimit(model)`.

Die deklarierten Modellkataloge driften derzeit auseinander:
- `LlmModels`-Konstanten referenzieren weiterhin qwen-2.5-Namen.
- `litellm_config.yaml` definiert qwen3-/glm-Modellnamen (inkl. `glm-4.6v-flash`) sowie Embeddings.

Diese Abweichung ist für die Betriebsdokumentation relevant und sollte explizit bleiben.

## Sicherheits- und Governance-Kontrollen im KI-Pfad
- Authentifizierung und Autorisierung auf allen Chat-Endpunkten.
- Ratenbegrenzung (`per-user`, 100/min).
- Eingabevalidierung über MediatR `ValidationBehavior` und Endpoint-Validatoren.
- Ausgabe- und Eingabesanitisierung über `SanitizationService` und `OutputSanitizationMiddleware`-Logging.
- Audit der Tool-Ausführung (`McpToolAuditLog`) und Whitelist-Prüfungen.

## Bekannte architektonische Grenzen
- Das Legacy-`nem.Mimir` verwendet in vielen Entitäten weiterhin primitive IDs.
- `ConversationSettings.Default` setzt das Modell weiterhin auf `gpt-4`, während Laufzeit-Defaults andernorts LiteLLM-Modell-IDs sind.
- Der agentenseitige Memory-Service (`ConversationMemoryService`) ist im Speicher, obwohl Kommentare auf Marten-ähnliche Persistenz verweisen.

## Querverweise
- [ARCHITECTURE](./ARCHITECTURE.md)
- [BUSINESS-LOGIC](./BUSINESS-LOGIC.md)
- [DATA-MODEL](./DATA-MODEL.md)
- [SECURITY](./SECURITY.md)
- [QA](./QA.md)
