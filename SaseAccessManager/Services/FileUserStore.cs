using SaseAccessManager.Models;
using System.Text.Json;

namespace SaseAccessManager.Services
{
    public class FileUserStore
    {
        private readonly string _path;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public FileUserStore()
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(folder);

            _path = Path.Combine(folder, "users.json");

            if (!File.Exists(_path))
                File.WriteAllText(_path, "[]");
        }

        public async Task<List<TemporarySaseUser>> GetAll()
        {
            await _lock.WaitAsync();
            try
            {
                var json = await File.ReadAllTextAsync(_path);
                return JsonSerializer.Deserialize<List<TemporarySaseUser>>(json)!;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAll(List<TemporarySaseUser> users)
        {
            await _lock.WaitAsync();
            try
            {
                var temp = _path + ".tmp";
                var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });

                await File.WriteAllTextAsync(temp, json);

                File.Copy(temp, _path, true);
                File.Delete(temp);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
