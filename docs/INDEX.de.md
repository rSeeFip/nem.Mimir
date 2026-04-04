# nem.Mimir — Dokumentationsindex

**Stufe**: Tier 1
**Repository**: `nem.Mimir`

## Übersicht

Conversational AI service with multi-channel chat, LLM routing, and agent hierarchy.
Diese Dokumentationssammlung umfasst die Architektur, Geschäftslogik, Qualitätssicherung, Sicherheitsarchitektur und betrieblichen Richtlinien des Services gemäß dem nem.* Dokumentationsstandard.

### Dokumentationsstandard

Alle Dokumentationen in diesem Repository folgen den nem.* Dokumentationskonventionen:

- **GROSSBUCHSTABEN**-Dateibenennung für alle Dokumentationsdateien
- **Zweisprachig**: Englisch als Primärsprache mit deutschen (`.de.md`) Übersetzungen
- **Strukturiert**: Jedes Dokument hat eine Hauptüberschrift, mindestens drei Abschnitte und substanziellen Inhalt
- **Querverwiesen**: Dokumente verlinken auf verwandte Dateien innerhalb des Repositorys

## Kerndokumentation

Diese Dokumente behandeln die primären Aspekte des Services:

| Dokument | Beschreibung |
|----------|-------------|
| [AI](./AI.de.md) | AI/ML capabilities, model integration, and inference patterns |
| [ARCHITECTURE](./ARCHITECTURE.de.md) | System architecture, component structure, and design decisions |
| [BUSINESS-LOGIC](./BUSINESS-LOGIC.de.md) | Core business rules, domain concepts, and operational workflows |
| [COMPLIANCE](./COMPLIANCE.de.md) | Regulatory compliance, audit requirements, and governance policies |
| [DATA-MODEL](./DATA-MODEL.de.md) | Data persistence strategy, entity models, and schema management |
| [HUMAN-PATTERNS](./HUMAN-PATTERNS.de.md) | Team practices, decision-making patterns, and collaboration guidelines |
| [INFRASTRUCTURE](./INFRASTRUCTURE.de.md) | Deployment architecture, CI/CD pipeline, and observability setup |
| [QA](./QA.de.md) | Quality assurance strategy, test pyramid, and quality gates |
| [SECURITY](./SECURITY.de.md) | Security architecture, authentication, authorization, and data protection |
| [USER-MANUAL](./USER-MANUAL.de.md) | User guide for developers and operators working with the service |

## Navigationsführer

### Für Entwickler

Beginnen Sie mit der Architekturdokumentation, um das Systemdesign zu verstehen, dann überprüfen Sie die Geschäftslogik für Domänenregeln und die QA-Dokumentation für Testkonventionen.

### Für Betriebsteams

Beginnen Sie mit der Infrastrukturdokumentation für Deployment-Details und dann dem Benutzerhandbuch für Betriebsverfahren.

### Für Sicherheitsprüfungen

Überprüfen Sie die Sicherheitsdokumentation für die Sicherheitsarchitektur und die Compliance-Anforderungen.

## Wartung

### Letzte Aktualisierung

Dieser Index wurde als Teil des nem.* Dokumentationsvalidierungsprozesses erstellt. Er spiegelt den aktuellen Stand der Dokumentationsdateien in diesem Repository wider.

### Validierung

Diese Dokumentationssammlung wird mit der nem.* Dokumentationsvalidierungssuite validiert, die folgendes prüft:

- Dateistruktur und Namenskonventionen
- Inhaltsqualität (Mindestzeilenanzahl, Abschnittsanzahl)
- Link-Integrität (keine defekten internen Links)
- Zweisprachige Abdeckung (deutsche Übersetzungen vorhanden)
- Markdown-Lint-Konformität
- Glossar-Begriffsverwendung

## Schnellreferenz

### Build-Befehle

```bash
# Lösung erstellen
dotnet build nem.Mimir.slnx

# Alle Tests ausführen
dotnet test nem.Mimir.slnx

# Mit spezifischer Konfiguration erstellen
dotnet build nem.Mimir.slnx --configuration Release
```

### Schlüsselkontakte

- **Repository-Besitzer**: nem.* Plattform-Team
- **Dokumentation**: Wird zusammen mit Code-Änderungen gepflegt
- **Issue-Tracking**: Repository-Issue-Tracker

### Konventionen

- Alle Dokumentationen folgen dem [nem.* Dokumentationsstandard](../../docs/040426/GLOSSARY.md)
- Deutsche Übersetzungen sind für alle Dokumentationsdateien erforderlich
- Dateinamen verwenden GROSSBUCHSTABEN-Konvention mit Bindestrichen
