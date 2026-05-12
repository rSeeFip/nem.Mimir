# ADR-005: Typed ID Adoption

**Status**: Accepted
**Date**: 2026-05-09
**Deciders**: nem.Mimir team

## Context
The previous version of nem.Mimir used primitive types (Guid, string) for entity identifiers. This approach often led to "primitive obsession" where IDs for different entities could be accidentally swapped in method signatures, leading to runtime bugs that are difficult to trace. As part of the migration to the nem.Mimir-typed-ids repository, we needed a more robust way to handle identity across the platform.

## Decision
We have decided to adopt strongly-typed IDs for all domain entities, leveraging the shared definitions in `nem.Contracts`. Every entity identifier is now its own type (e.g., `ConversationId`, `MessageId`, `AgentId`) rather than a raw Guid.

## Consequences
### Positive
- **Type Safety**: The compiler prevents accidental swapping of different ID types (e.g., passing a `MessageId` where a `ConversationId` is expected).
- **Domain Clarity**: Code is more self-documenting; method signatures explicitly state which ID they require.
- **Consistency**: Alignment with the broader `nem.*` ecosystem conventions for identity management.

### Negative
- **Breaking Change**: This is a significant breaking change from the legacy `nem.Mimir` code base, requiring updates to all layers from domain to API.
- **Boilerplate**: Requires defining and maintaining specific ID types, though these are largely centralized in `nem.Contracts`.

### Neutral
- **Serialization**: Requires custom JSON converters and EF Core value converters, which are provided by the `nem.Contracts.AspNetCore` package.
