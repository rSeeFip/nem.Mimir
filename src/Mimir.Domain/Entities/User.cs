namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;
using Mimir.Domain.Enums;
using Mimir.Domain.Events;

public class User : BaseAuditableEntity<Guid>
{
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private User() { }

    public static User Create(string username, string email, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };

        user.AddDomainEvent(new UserCreatedEvent(user.Id, username));

        return user;
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
    }

    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
