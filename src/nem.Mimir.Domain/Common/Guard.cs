namespace nem.Mimir.Domain.Common;

public static class Guard
{
    public static class Against
    {
        public static void Null<T>(T? value, string paramName)
        {
            if (value is null)
                throw new ArgumentNullException(paramName);
        }

        public static void NullOrEmpty(string? value, string paramName)
        {
            if (value is null)
                throw new ArgumentNullException(paramName);

            if (value == string.Empty)
                throw new ArgumentException("Value cannot be empty.", paramName);
        }

        public static void NullOrWhiteSpace(string? value, string paramName)
        {
            if (value is null)
                throw new ArgumentNullException(paramName);

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be null or whitespace.", paramName);
        }

        public static void OutOfRange<T>(T value, T min, T max, string paramName)
            where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
                throw new ArgumentOutOfRangeException(
                    paramName,
                    $"Value must be between {min} and {max}.");
        }

        public static void Default<T>(T? value, string paramName)
        {
            if (EqualityComparer<T>.Default.Equals(value, default))
                throw new ArgumentException($"Value cannot be the default value for type {typeof(T).Name}.", paramName);
        }
    }
}
