using SaseAccessManager.DTOs;

namespace SaseAccessManager.Cache
{
    public interface ISaseGroupCache
    {
        Task<IReadOnlyList<SaseGroupDto>> GetAsync();
    }
}
