namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Data transfer object representing a user.
/// </summary>
/// <param name="Id">The user's unique identifier.</param>
/// <param name="Username">The user's display name.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="Role">The user's role name.</param>
/// <param name="LastLoginAt">The timestamp of the user's last login, if any.</param>
/// <param name="IsActive">Whether the user account is active.</param>
/// <param name="CreatedAt">The timestamp when the user was created.</param>
public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);
