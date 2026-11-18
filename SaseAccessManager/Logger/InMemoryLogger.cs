namespace SaseAccessManager.Logging
{
    public class InMemoryLogger : ILogger
    {
        private readonly string _category;
        private readonly InMemoryLogStore _store;

        public InMemoryLogger(string category, InMemoryLogStore store)
        {
            _category = category;
            _store = store;
        }

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= LogLevel.Information
               && _category.StartsWith("SaseAccessManager");

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            if (exception != null)
                message += " | " + DescribeException(exception);

            _store.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = logLevel,
                Category = _category,
                Message = message
            });
        }

        private static string DescribeException(Exception exception)
        {
            var root = exception.GetBaseException();

            var text = root == exception
                ? $"{root.GetType().Name}: {root.Message}"
                : $"{exception.GetType().Name} → {root.GetType().Name}: {root.Message}";

            var location = (exception.StackTrace ?? root.StackTrace)?
                .Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("at SaseAccessManager", StringComparison.Ordinal));

            if (location != null)
                text += $" ({location})";

            return text;
        }
    }

    public class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly InMemoryLogStore _store;

        public InMemoryLoggerProvider(InMemoryLogStore store)
        {
            _store = store;
        }

        public ILogger CreateLogger(string categoryName)
            => new InMemoryLogger(categoryName, _store);

        public void Dispose() { }
    }
}