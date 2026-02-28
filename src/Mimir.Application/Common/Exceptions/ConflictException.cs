namespace Mimir.Application.Common.Exceptions;

public class ConflictException : Exception
{
    public ConflictException()
        : base("A conflict occurred while processing your request.")
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }
}
