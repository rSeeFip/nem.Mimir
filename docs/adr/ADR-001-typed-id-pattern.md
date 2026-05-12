# ADR-001: Strongly-Typed IDs via nem.Mimir-typed-ids

**Date**: 2024-12-20
**Status**: Accepted

## Context

Using raw `Guid` or `int` for entity IDs across the nem* platform leads to accidental ID mixing (passing a SkillId where a UserId is expected). This class of bug is invisible at compile time and only surfaces at runtime.

## Decision

Use strongly-typed IDs from nem.Mimir-typed-ids for all entity identifiers. Each entity type has a dedicated ID type (e.g., `SkillId`, `UserId`, `TenantId`) that wraps a `Guid`. The type system prevents mixing IDs of different entity types at compile time.

## Consequences

### Positive
- Compile-time prevention of ID type confusion
- Self-documenting code (parameter types communicate intent)
- JSON serialization/deserialization handled transparently

### Negative
- Boilerplate for each new entity type
- EF Core and Marten require value converters for each ID type

### Neutral
- nem.Mimir-typed-ids is a source generator; no runtime overhead
