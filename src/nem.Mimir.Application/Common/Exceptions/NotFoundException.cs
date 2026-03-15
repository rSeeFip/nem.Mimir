namespace nem.Mimir.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a requested entity cannot be found (HTTP 404).
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    public NotFoundException()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class with a specified message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class for a specific entity type and key.
    /// </summary>
    /// <param name="name">The name of the entity type that was not found.</param>
    /// <param name="key">The key value used to look up the entity.</param>
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}
