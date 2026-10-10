using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TeamNotes.Api.Tests;

/// <summary>In-memory logger provider: keeps every formatted log line the app writes.</summary>
public sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines.ToArray();

    public string All => string.Join("\n", _lines);

    public void Clear() => _lines.Clear();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
        // nothing to release
    }

    private sealed class Logger(CapturedLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => owner._lines.Enqueue($"{logLevel} {category}: {formatter(state, exception)}{(exception is null ? "" : " " + exception)}");
    }
}
