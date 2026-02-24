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
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Email { get; set; }
        public string? Name { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }

        public UserStatus Status { get; set; } = UserStatus.Active;

        public string? SaseUserId { get; set; }
        public DateTime? LastRemovalAttempt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
