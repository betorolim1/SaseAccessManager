using Microsoft.Extensions.Options;
using SaseAccessManager.Options;
using System.Text.Json;

namespace SaseAccessManager.Auth
{
    public class SaseAuthProvider : ISaseAuthProvider
    {
        private readonly HttpClient _http;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly SaseOptions _options;

        private string? _token;
        private DateTime _expiresAtUtc;

        public SaseAuthProvider(HttpClient http, IOptions<SaseOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            if (IsValid())
                return _token!;

            await _lock.WaitAsync(ct);
            try
            {
                if (IsValid())
                    return _token!;

                await Authenticate(ct);
                return _token!;
            }
            finally
            {
                _lock.Release();
            }
        }

        public Task InvalidateAsync()
        {
            _token = null;
            _expiresAtUtc = DateTime.MinValue;
            return Task.CompletedTask;
        }

        private bool IsValid()
            => _token != null && DateTime.UtcNow < _expiresAtUtc;

        private async Task Authenticate(CancellationToken ct)
        {
            var apiKey = _options.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Sase:ApiKey não configurado");

            var response = await _http.PostAsJsonAsync(
                "auth/authorize",
                new { grantType = "api_key", apiKey },
                ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Falha autenticação SASE HTTP {(int)response.StatusCode}: {content}");

            AuthEnvelope? envelope;

            try
            {
                envelope = JsonSerializer.Deserialize<AuthEnvelope>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                throw new Exception($"Resposta inválida da autenticação SASE: {content}");
            }

            var auth = envelope?.Data;

            if (auth == null || string.IsNullOrWhiteSpace(auth.AccessToken))
                throw new Exception($"SASE não retornou accessToken: {content}");

            _token = auth.AccessToken;

            var expireUtc = DateTimeOffset.FromUnixTimeSeconds(auth.AccessTokenExpire).UtcDateTime;

            _expiresAtUtc = expireUtc.AddMinutes(-2);
        }

        private class AuthEnvelope
        {
            public AuthData Data { get; set; } = default!;
        }

        private class AuthData
        {
            public string AccessToken { get; set; } = default!;
            public long AccessTokenExpire { get; set; }
        }
    }
}
