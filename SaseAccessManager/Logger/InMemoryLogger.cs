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

        //public bool IsEnabled(LogLevel logLevel)
        //    => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            _store.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = logLevel,
                Category = _category,
                Message = formatter(state, exception)
            });
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