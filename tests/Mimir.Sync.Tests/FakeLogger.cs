using Microsoft.Extensions.Logging;

namespace Mimir.Sync.Tests;

/// <summary>
/// A simple fake logger that captures log entries for test assertions.
/// Used instead of NSubstitute mocks for <see cref="ILogger{T}"/> when T is an internal type,
/// which Castle.DynamicProxy cannot proxy.
/// </summary>
public sealed class FakeLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
    }

    public sealed record LogEntry(LogLevel LogLevel, EventId EventId, string Message, Exception? Exception);
}
