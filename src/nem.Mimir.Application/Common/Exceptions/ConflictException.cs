namespace nem.Mimir.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a conflicting operation is attempted (HTTP 409).
/// </summary>
public class ConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictException"/> class with a default message.
    /// </summary>
    public ConflictException()
        : base("A conflict occurred while processing your request.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictException"/> class with a specified message.
    /// </summary>
    /// <param name="message">The message that describes the conflict.</param>
    public ConflictException(string message)
        : base(message)
    {
    }
}
