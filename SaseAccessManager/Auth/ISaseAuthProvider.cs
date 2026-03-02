namespace SaseAccessManager.Auth
{
    public interface ISaseAuthProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken ct = default);
        Task InvalidateAsync(); // usado após 401
    }
}
