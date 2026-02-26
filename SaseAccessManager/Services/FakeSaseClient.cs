using SaseAccessManager.DTOs;

namespace SaseAccessManager.Services
{
    public class FakeSaseClient : ISaseClient
    {
        public Task<(bool Success, string? UserId, string? Error)> CreateUser(SaseCreateUserRequest request)
        {
            return Task.FromResult<(bool, string?, string?)>((true, Guid.NewGuid().ToString(), null));
        }

        public Task<(bool Success, string? Error)> DeleteUser(string saseUserId)
        {
            return Task.FromResult<(bool, string?)>((true, null));
        }
    }
}
