# Changelog: nem.Mimir

All notable changes to the Mimir project from Wave 1 to Wave 6 are documented here.

## [Wave 6] - Testing & Reliability (Current)
- **Negative Testing**: Added 131+ tests covering failure modes, security edge cases, and invalid inputs.
- **Integration Robustness**: Improved Testcontainers setup for PostgreSQL and RabbitMQ.
- **Race Condition Fix**: Resolved a critical race condition in `ConversationMemoryService` during concurrent message processing.
- **Resource Management**: Fixed a timer disposal issue in the `AuthenticationService`.

## [Wave 5] - Security & Standardization
- **JSON Standardization**: Introduced `JsonDefaults` class to unify serialization across REST and SignalR.
- **ProblemDetails**: Fully implemented RFC 7807 for consistent error reporting.
- **Global Exception Handler**: Replaced custom middleware with the new ASP.NET Core `IExceptionHandler`.
- **Correlation IDs**: Integrated `CorrelationIdMiddleware` for end-to-end request tracing.

## [Wave 4] - Validation & Refinement
- **FluentValidation**: Added 28 comprehensive validators for all Command/Query inputs.
- **API Hardening**: Implemented rate limiting and Kestrel request body size limits.
- **OIDC Shared Contracts**: Migrated Keycloak authentication to use shared `nem.Contracts.AspNetCore`.
- **Swagger Enhancements**: Improved API documentation with detailed security definitions and response types.

## [Wave 3] - Messaging & Events
- **Wolverine Integration**: Replaced MediatR for out-of-process messaging.
- **Durable Messaging**: Configured RabbitMQ with durable inbox/outbox patterns.
- **Event Sourcing**: Introduced Marten for conversation event storage.
- **Audit Service**: Created a centralized service for immutable event logging.

## [Wave 2] - Infrastructure & AI Proxy
- **LiteLLM Implementation**: Unified multiple LLM providers behind the LiteLLM proxy.
- **SSE Streaming**: Enabled real-time Server-Sent Events for chat completions.
- **Docker Sandbox**: Developed the isolated execution environment for the `CodeRunner` plugin.
- **Multi-Client Support**: Initial support for Telegram and TUI clients.

## [Wave 1] - Core Foundation
- **Clean Architecture**: established the 4-layer project structure.
- **Persistence Layer**: EF Core integration with PostgreSQL.
- **Keycloak Integration**: Initial OIDC setup for user authentication.
- **Base Entities**: Defined the core Domain model (Conversations, Messages, Users).
