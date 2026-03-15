namespace nem.Mimir.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when one or more validation failures occur (HTTP 400/422).
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a default message.
    /// </summary>
    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a specified message.
    /// </summary>
    /// <param name="message">The message that describes the validation failure.</param>
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a dictionary of validation errors.
    /// </summary>
    /// <param name="errors">A dictionary of property names to their associated validation error messages.</param>
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the dictionary of validation errors, keyed by property name.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }
}
