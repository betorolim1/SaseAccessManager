using System.Text.Json.Serialization;

namespace SaseAccessManager.DTOs
{
    public class SaseCreateUserRequest
    {
        [JsonPropertyName("idpType")]
        public string IdpType { get; set; } = default!;

        [JsonPropertyName("accessGroups")]
        public List<string> AccessGroups { get; set; } = new List<string> { "All Users" };

        [JsonPropertyName("email")]
        public string Email { get; set; } = default!;

        [JsonPropertyName("emailVerified")]
        public bool EmailVerified { get; set; }

        [JsonPropertyName("inviteMessage")]
        public string InviteMessage { get; set; } = "Conta criada automaticamente via API";

        [JsonPropertyName("profileData")]
        public SaseProfileData ProfileData { get; set; } = default!;

        [JsonPropertyName("origin")]
        public string Origin { get; set; } = "api";
    }
}
