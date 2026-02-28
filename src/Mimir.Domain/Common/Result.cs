namespace Mimir.Domain.Common;

public sealed class Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    private Result(T? value, string? error)
    {
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; private set; }

    public bool IsFailure => !IsSuccess;

    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access Value on a failed Result.");

            return _value!;
        }
    }

    public string Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error on a successful Result.");

            return _error!;
        }
    }

    public static Result<T> Success(T value) => new(value, null) { IsSuccess = true };

    public static Result<T> Failure(string error) => new(default, error) { IsSuccess = false };

    public static implicit operator Result<T>(T value) => Success(value);
}
