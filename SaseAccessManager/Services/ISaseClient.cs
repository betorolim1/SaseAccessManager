using SaseAccessManager.DTOs;

namespace SaseAccessManager.Services
{
    public interface ISaseClient
    {
        Task<(bool Success, string? UserId, string? Error)> CreateUser(SaseCreateUserRequest request);
        Task<(bool Success, string? Error)> DeleteUser(string saseUserId);
        Task<IReadOnlyList<SaseGroupDto>> GetGroupsAsync(CancellationToken ct);
    }
}
