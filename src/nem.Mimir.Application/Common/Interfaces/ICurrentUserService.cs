namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Service interface for accessing information about the currently authenticated user.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the unique identifier of the current user, or null if not authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the tenant identifier of the current user, or null if unavailable.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets a value indicating whether the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the roles assigned to the current user.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Gets a value indicating whether the current user is a platform administrator.
    /// </summary>
    bool IsPlatformAdmin { get; }

    /// <summary>
    /// Gets a value indicating whether the current user is a tenant administrator.
    /// </summary>
    bool IsTenantAdmin { get; }
}
