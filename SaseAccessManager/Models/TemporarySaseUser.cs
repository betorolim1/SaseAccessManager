namespace SaseAccessManager.Models
{
    public enum UserStatus
    {
        Active = 1,
        Removed = 2,
        Error = 3
    }

    public class TemporarySaseUser
    {
        public string Id { get; set; }

        public string Email { get; set; } = default!;

        public string Name { get; set; } = default!;
        public string? LastName { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public UserStatus Status { get; set; }

        public string? SaseUserId { get; set; }

        public DateTime? LastRemovalAttempt { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
