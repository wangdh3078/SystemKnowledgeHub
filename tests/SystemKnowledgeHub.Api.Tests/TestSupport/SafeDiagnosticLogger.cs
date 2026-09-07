using Microsoft.Extensions.Logging;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

internal sealed class SafeDiagnosticLogger<T> : ILogger<T>
{
    public List<string> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Assert.Null(exception);
        Entries.Add(formatter(state, exception));
    }
}
