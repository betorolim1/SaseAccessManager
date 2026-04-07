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

    public async Task<(bool Success, bool AlreadyExists, string? UserId, string? Error)> CreateUser(SaseCreateUserRequest request)
    {
        try
        {
            var body = new SaseCreateUserRequest
            {
                IdpType = request.IdpType,
                Email = request.Email,
                AccessGroups = request.AccessGroups,
                EmailVerified = request.EmailVerified,
                InviteMessage = request.InviteMessage,
                Origin = request.Origin,
                ProfileData = new SaseProfileData
                {
                    FirstName = request.ProfileData.FirstName,
                    LastName = request.ProfileData.LastName
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "users")
            {
                Content = JsonContent.Create(
                    body,
                    options: new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    })
            };

            var response = await SendAsync(httpRequest, CancellationToken.None);

            var content = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Conflict)
                return (false, true, null, "USER_ALREADY_EXISTS");

            if (!response.IsSuccessStatusCode)
                return (false, false, null, $"HTTP {(int)response.StatusCode}: {content}");

            using var json = JsonDocument.Parse(content);
            var id = json.RootElement.GetProperty("id").GetString();

            return (true, false, id, null);
        }
        catch (Exception ex)
        {
            return (false, false, null, ex.Message);
        }
    }

    public async Task<SaseUserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var q = Uri.EscapeDataString(System.Text.Json.JsonSerializer.Serialize(new { email }));
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"users?page=1&limit=25&q={q}&qType=partial");

            var response = await SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var stream = await response.Content.ReadAsStreamAsync(ct);
            var result = await System.Text.Json.JsonSerializer.DeserializeAsync<SaseUserSearchResponse>(
                stream, JsonOptions.Default, ct);

            return result?.Data?.FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                !u.Terminated);
        }
        catch
        {
            return null;
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

    public async Task<IReadOnlyList<GroupItem>> GetGroupsAsync(CancellationToken ct)
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

            return result?.Data ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<(bool Success, string? Error)> AddUserToGroup(string groupId, string userId)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"groups/{groupId}/member/{userId}");

            var response = await SendAsync(request, CancellationToken.None);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var content = await response.Content.ReadAsStringAsync();

            return (false, $"HTTP {(int)response.StatusCode}: {content}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> RemoveUserFromGroup(string groupId, string userId)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"groups/{groupId}/member/{userId}");

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