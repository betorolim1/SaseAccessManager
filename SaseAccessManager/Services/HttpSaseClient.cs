using SaseAccessManager.DTOs;
using System.Net;
using System.Text.Json;

namespace SaseAccessManager.Services;

public class HttpSaseClient : ISaseClient
{
    private readonly HttpClient _http;

    public HttpSaseClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, string? UserId, string? Error)> CreateUser(SaseCreateUserRequest request)
    {
        try
        {
            var body = new SaseCreateUserRequest
            {
                Email = request.Email,
                ProfileData = new SaseProfileData
                {
                    FirstName = request.ProfileData.FirstName,
                    LastName = request.ProfileData.LastName
                }
            };

            var response = await _http.PostAsJsonAsync("users", body);

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, null, $"HTTP {(int)response.StatusCode}: {content}");

            // resposta da API contém "id"
            using var json = JsonDocument.Parse(content);
            var id = json.RootElement.GetProperty("id").GetString();

            return (true, id, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteUser(string saseUserId)
    {
        try
        {
            var response = await _http.DeleteAsync($"users/{saseUserId}");

            if (response.IsSuccessStatusCode ||
                response.StatusCode == HttpStatusCode.NotFound)
                return (true, null);

            var content = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {content}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}