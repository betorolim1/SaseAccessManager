namespace SaseAccessManager.DTOs
{
    public class SaseCreateUserRequest
    {
        public string IdpType { get; set; } = "database";

        public List<string> AccessGroups { get; set; } = ["All Users"];

        public string Email { get; set; } = default!;

        public bool EmailVerified { get; set; } = true;

        public string InviteMessage { get; set; } = "Conta criada automaticamente via API";

        public SaseProfileData ProfileData { get; set; } = default!;

        public string Origin { get; set; } = "api";
    }
}
