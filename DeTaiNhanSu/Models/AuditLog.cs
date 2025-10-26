namespace DeTaiNhanSu.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Action { get; set; } = default!;
        public string? TableName { get; set; }
        public string? RecordId { get; set; }
        public string? Description { get; set; }
        public string? IPAddress { get; set; }
        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = default!;
    }
}
