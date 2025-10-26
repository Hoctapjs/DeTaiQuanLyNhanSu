namespace DeTaiNhanSu.Models
{
    public class Policy
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = default!;
        public string Category { get; set; } = default!;
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string? AttachmentUrl { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public DateOnly? ExpiredDate { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Version { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
