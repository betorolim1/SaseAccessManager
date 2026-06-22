using SaseAccessManager.DTOs;

namespace SaseAccessManager.Services
{
    public interface ISaseClient
    {
        Task<(bool Success, bool AlreadyExists, string? UserId, string? Error)> CreateUser(SaseCreateUserRequest request);
        Task<(bool Success, string? Error)> DeleteUser(string saseUserId);
        Task<IReadOnlyList<GroupItem>> GetGroupsAsync(CancellationToken ct);
        Task<(bool Success, string? Error)> AddUserToGroup(string groupId, string userId);
        Task<(bool Success, string? Error)> RemoveUserFromGroup(string groupId, string userId);
        Task<SaseUserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default);
        Task<List<SaseUserDto>> GetAllUsersAsync(CancellationToken ct = default);
    }
}
