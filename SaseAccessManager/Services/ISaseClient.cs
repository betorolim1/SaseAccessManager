using SaseAccessManager.DTOs;

namespace SaseAccessManager.Services
{
    public interface ISaseClient
    {
        Task<(bool Success, string? UserId, string? Error)> CreateUser(SaseCreateUserRequest request);
        Task<(bool Success, string? Error)> DeleteUser(string saseUserId);
        Task<IReadOnlyList<SaseGroupDto>> GetGroupsAsync(CancellationToken ct);
        Task<(bool Success, string? Error)> AddUserToGroup(string groupId, string userId);
        Task<(bool Success, string? Error)> RemoveUserFromGroup(string groupId, string userId);
    }
}
