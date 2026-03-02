using System.Text.Json;

namespace SaseAccessManager.Helper
{
    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }
}
