using System.Collections.Concurrent;

namespace SaseAccessManager.Logging
{
    public class LogEntry
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string Category { get; init; } = "";
        public string Message { get; init; } = "";
    }

    public class InMemoryLogStore
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private readonly int _maxEntries;

        public InMemoryLogStore(int maxEntries = 500)
        {
            _maxEntries = maxEntries;
        }

        public void Add(LogEntry entry)
        {
            _entries.Enqueue(entry);

            while (_entries.Count > _maxEntries)
                _entries.TryDequeue(out _);
        }

        public List<LogEntry> GetAll()
            => _entries.ToList();
    }
}