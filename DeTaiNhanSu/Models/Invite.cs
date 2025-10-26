namespace DeTaiNhanSu.Models
{
    public sealed class Invite
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = default!;  
        public DateTime ExpiresAtUtc { get; set; }         
        public DateTime? UsedAtUtc { get; set; }           
        public string Purpose { get; set; } = "SetPassword";

        public User User { get; set; } = default!;
    }
}
