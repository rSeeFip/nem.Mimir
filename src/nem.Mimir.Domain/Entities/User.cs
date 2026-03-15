namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;

public class User : BaseAuditableEntity<Guid>
{
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool IsActive { get; private set; } = true;

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
            IsActive = true,
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
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
