using MediatR;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Marker interface for CQRS commands that return a result.
/// Commands represent operations that change system state.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>
/// Marker interface for CQRS commands that do not return a value.
/// </summary>
public interface ICommand : IRequest;
