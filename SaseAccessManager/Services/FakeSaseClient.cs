namespace SaseAccessManager.Services
{
    public class FakeSaseClient : ISaseClient
    {
        public Task<(bool Success, string? UserId, string? Error)> CreateUser(string email, string? name)
        {
            return Task.FromResult<(bool, string?, string?)>((true, Guid.NewGuid().ToString(), null));
        }

        public Task<(bool Success, string? Error)> DeleteUser(string saseUserId)
        {
            return Task.FromResult<(bool, string?)>((true, null));
        }
    }
}
