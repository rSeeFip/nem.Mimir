# ADR-003: Wolverine for Message-Based Architecture

## Status
Accepted

## Date
2025-01

## Context
We needed a messaging backbone for inter-service communication, especially for chat message handling and background processing.

## Decision
Use **Wolverine** (formerly Marten) as the messaging framework:
- HTTP-based endpoints for synchronous operations
- Message handlers for asynchronous processing
- RabbitMQ transport for distributed messaging
- In-memory transport for development

## Consequences

### Positive
- Native .NET integration
- Easy to add handlers (just implement `IHandler`)
- Works with existing PostgreSQL (no extra infrastructure)
- Flexible transport switching

### Negative
- Learning curve for developers new to Wolverine
- Additional abstraction layer

## Services Using Wolverine
- `Mimir.Sync` - Message processing
- `Mimir.Api` - HTTP endpoints
- `Mimir.Telegram` - Bot message handling

## References
- Implementation: `src/Mimir.Sync/`
- Transport: RabbitMQ (production), InMemory (development)
