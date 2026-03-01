# ADR-002: Plugin Architecture with AssemblyLoadContext Isolation

## Status
Accepted

## Date
2025-01

## Context
We needed a plugin system that allows loading third-party extensions while maintaining isolation and preventing memory leaks.

## Decision
Use .NET's `AssemblyLoadContext` for plugin isolation with the following design:
- `PluginLoadContext` inherits from `AssemblyLoadContext`
- Plugins are loaded into isolated contexts
- Plugins can be unloaded by disposing their context
- `IPlugin` interface defines the contract

## Consequences

### Positive
- Memory isolation between plugins
- No version conflicts between plugins
- Safe unloading capability
- Type safety via IPlugin interface

### Negative
- Complexity in cross-plugin communication
- Shared services require careful design

## References
- Implementation: `Mimir.Infrastructure/Plugins/`
- SDK: `nem.Plugins.SDK`
