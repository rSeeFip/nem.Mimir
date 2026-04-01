namespace nem.Mimir.Infrastructure.Adapters;

using MediatR;
using nem.MCP.Core.Cognitive;

public sealed record WorkspaceBroadcastNotification(WorkspaceBroadcastEvent BroadcastEvent) : INotification;
