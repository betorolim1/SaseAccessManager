namespace SaseAccessManager.Services
{
    public interface ISaseClient
    {
        Task<(bool Success, string? UserId, string? Error)> CreateUser(string email, string? name);
        Task<(bool Success, string? Error)> DeleteUser(string saseUserId);
    }
}
