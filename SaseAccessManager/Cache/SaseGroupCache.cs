using SaseAccessManager.DTOs;
using SaseAccessManager.Services;

namespace SaseAccessManager.Cache
{
    public class SaseGroupCache : ISaseGroupCache
    {
        private readonly ISaseClient _client;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private List<SaseGroupDto>? _cache;
        private DateTime _expires;

        public SaseGroupCache(ISaseClient client)
        {
            _client = client;
        }

        public async Task<IReadOnlyList<SaseGroupDto>> GetAsync()
        {
            if (_cache != null && DateTime.UtcNow < _expires)
                return _cache;

            await _lock.WaitAsync();
            try
            {
                if (_cache != null && DateTime.UtcNow < _expires)
                    return _cache;

                var groups = (await _client.GetGroupsAsync(CancellationToken.None)).ToList();

                if (groups.Count > 0)
                {
                    _cache = groups;
                    _expires = DateTime.UtcNow.AddMinutes(10);
                }

                return _cache ?? [];
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
