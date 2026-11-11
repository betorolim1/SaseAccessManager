using SaseAccessManager.Models;
using System.Text.Json.Serialization;

namespace SaseAccessManager.DTOs
{
    public class SaseUserDto
    {
        public string Id { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool Terminated { get; set; }
        public string? Role { get; set; }
        public List<string>? Roles { get; set; }

        [JsonIgnore]
        public bool IsAdmin =>
            SaseRoles.IsAdmin(Role) || (Roles?.Any(SaseRoles.IsAdmin) ?? false);

        [JsonIgnore]
        public string? Perfil => IsAdmin ? SaseRoles.Admin : Role;
    }

    public class SaseUserSearchResponse
    {
        public List<SaseUserDto> Data { get; set; } = [];
    }
}
