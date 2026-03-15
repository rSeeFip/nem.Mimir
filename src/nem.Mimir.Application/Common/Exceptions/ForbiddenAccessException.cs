namespace nem.Mimir.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when the current user lacks permission to perform the requested operation (HTTP 403).
/// </summary>
public class ForbiddenAccessException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class with a default message.
    /// </summary>
    public ForbiddenAccessException()
        : base("You do not have permission to access this resource.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class with a specified message.
    /// </summary>
    /// <param name="message">The message that describes the access violation.</param>
    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}
