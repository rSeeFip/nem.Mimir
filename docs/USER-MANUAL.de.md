<!-- sync-hash: e017123e4454b30a5cdcbbf2d486ae71 -->
---
repo: nem.Mimir
tier: tier-1
status: regenerated
---

# Benutzerhandbuch: nem.Mimir

## Zielgruppe
Dieses Handbuch richtet sich an:
- Anwendungsnutzer, die Konversations-APIs/UI verwenden,
- Betreiber, die Prompts/Plugins/MCP-Endpunkte administrieren,
- Support-Engineers, die Chat- und Modellverhalten analysieren.

## Voraussetzungen
- Gültiger Authentifizierungs-Token (außer im lokalen Standalone-Modus).
- Erreichbarer API-Host.
- Mindestens ein verfügbares LiteLLM-Modell für erfolgreiche Completions.

## Zentrale Nutzerflüsse
### 1) Konversation anlegen
Endpoint: `POST /api/conversations`

Erforderliche Eingabe:
- `title`

Optionale Eingaben:
- `systemPrompt`
- `model`

Erwartetes Ergebnis:
- Neue Conversation-Resource mit Identifier wird zurückgegeben.

### 2) Nachricht senden und Antwort erhalten
Endpoint: `POST /api/conversations/{conversationId}/messages`

Eingabe:
- `content`
- optional `model`

Verhalten:
- Systemprompt und Historie werden automatisch einbezogen.
- Die Assistant-Antwort wird generiert und persistiert.

### 3) OpenAI-kompatible Completion
Endpoint: `POST /v1/chat/completions`

Unterstützt:
- nicht-streamende JSON-Antworten
- streamende SSE-Antworten (`stream=true`)

### 4) Systemprompts verwalten
Endpunkte unter `api/systemprompts` unterstützen create/list/get/update/delete/render.
Verwenden Sie den Render-Endpunkt, um Template-Variablen vor der produktiven Nutzung zu prüfen.

### 5) Modellverfügbarkeit prüfen
Verwenden Sie:
- `GET /api/models`
- `GET /api/models/{modelId}/status`
- `GET /v1/models` (OpenAI-kompatibel)

### 6) Admin-only-Operationen
Admins können:
- Benutzer und Rollen verwalten
- Audit-Logs einsehen
- logisch gelöschte Entitäten wiederherstellen
- MCP-Server und Tool-Whitelists konfigurieren

## Fehlerbehebung
### 401/403-Antworten
- Token und Rollen-Claims validieren.
- Prüfen, ob der Endpunkt `RequireAdmin` erfordert.

### 404 „conversation not found“
- Bestätigen, dass die Conversation-ID zum authentifizierten Benutzer gehört.

### Completion-Fehler
- LiteLLM-Erreichbarkeit und Modellverfügbarkeit prüfen.
- `/health` und Logs auf Upstream-Fehler untersuchen.

### Tool-/MCP-Probleme
- Sicherstellen, dass die MCP-Server-Konfiguration aktiviert und verbunden ist.
- Prüfen, dass das Tool explizit whitelisted ist.
- MCP-Tool-Audit-Logs für Fehlermeldungen auswerten.

## Operative Leitplanken
- Admin-Endpunkte niemals mit User-Tokens ausführen.
- Den Standalone-Auth-Modus nicht außerhalb der lokalen Entwicklung verwenden.
- Dieses Repository als Legacy-Branch behandeln; Verhalten für Migrationsplanung gegen den aktiven Typed-ID-Branch validieren.

## FAQ
**F: Welches Modell wird standardmäßig verwendet?**
A: Das hängt vom Aufrufpfad ab; `SendMessageCommand` verwendet standardmäßig `phi-4-mini`, während andere Defaults konfigurationsgetrieben sind.

**F: Warum wird die Historie manchmal gekürzt?**
A: Der Context-Window-Service kürzt die ältesten Nachrichten, um in die Token-Limits des Modells zu passen.

**F: Kann das Modell automatisch Tools aufrufen?**
A: Ja, wenn Tool-Definitionen verfügbar und whitelisted sind, führt der Tool-Loop die Aufrufe aus und speist die Ergebnisse zurück.

## Glossar
- **LiteLLM**: OpenAI-kompatibler Proxy, der vom Backend-Inferenzadapter verwendet wird.
- **MCP**: Model Context Protocol für externe Tools/Ressourcen/Prompts.
- **Tool-Whitelist**: pro Server definierte Allowlist, die ausführbare Tools begrenzt.
- **Context Window**: begrenzte Nachrichtenhistorie, die an das Modell gesendet wird.
- **Standalone Mode**: lokaler Dev-Auth-Modus, der Keycloak umgeht.

## Querverweise
- [AI](./AI.md)
- [ARCHITECTURE](./ARCHITECTURE.md)
- [SECURITY](./SECURITY.md)
- [INFRASTRUCTURE](./INFRASTRUCTURE.md)
