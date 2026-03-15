namespace nem.Mimir.Api.Hubs;

/// <summary>
/// Identity information extracted from authenticated WebWidget user claims.
/// </summary>
public sealed record ActorIdentity(string UserId, string? Email, string? DisplayName);
