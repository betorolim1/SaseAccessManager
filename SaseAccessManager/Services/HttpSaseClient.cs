using SaseAccessManager.Auth;
using SaseAccessManager.DTOs;
using SaseAccessManager.Helper;
using System.Net;
using System.Text.Json;

namespace SaseAccessManager.Services;

public class HttpSaseClient : ISaseClient
{
    private readonly HttpClient _http;
    private readonly ISaseAuthProvider _auth;

    public HttpSaseClient(HttpClient http, ISaseAuthProvider auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<(bool Success, string? UserId, string? Error)> CreateUser(SaseCreateUserRequest request)
    {
        try
        {
            var body = new SaseCreateUserRequest
            {
                AccessGroups = request.AccessGroups,
                IdpType = request.IdpType,
                Email = request.Email,
                ProfileData = new SaseProfileData
                {
                    FirstName = request.ProfileData.FirstName,
                    LastName = request.ProfileData.LastName
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "users")
            {
                Content = JsonContent.Create(body)
            };

            var response = await SendAsync(httpRequest, CancellationToken.None);

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, null, $"HTTP {(int)response.StatusCode}: {content}");

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
            var request = new HttpRequestMessage(HttpMethod.Delete, $"users/{saseUserId}");

            var response = await SendAsync(request, CancellationToken.None);

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

    public async Task<IReadOnlyList<SaseGroupDto>> GetGroupsAsync(CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "groups?page=1&limit=200");

            var response = await SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return [];

            var stream = await response.Content.ReadAsStreamAsync(ct);

            var result = await JsonSerializer.DeserializeAsync<GroupResponse>(
                stream,
                JsonOptions.Default,
                ct);

            return result?.Data?
                .Select(g => new SaseGroupDto
                {
                    Id = g.Id,
                    Name = g.Name
                })
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        request.Headers.Authorization = new("Bearer", token);

        var response = await _http.SendAsync(request, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        await _auth.InvalidateAsync();

        var retry = await CloneAsync(request);

        token = await _auth.GetAccessTokenAsync(ct);
        retry.Headers.Authorization = new("Bearer", token);

        response.Dispose();

        return await _http.SendAsync(retry, ct);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        foreach (var h in request.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms);
            ms.Position = 0;

            clone.Content = new StreamContent(ms);

            foreach (var h in request.Content.Headers)
                clone.Content.Headers.Add(h.Key, h.Value);
        }

        return clone;
    }
}