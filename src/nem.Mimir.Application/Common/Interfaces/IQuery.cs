namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Marker interface for CQRS queries that return a result.
/// Queries represent read-only operations that do not change system state.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IQuery<out TResponse>;
