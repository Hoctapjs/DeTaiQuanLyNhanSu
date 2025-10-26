namespace DeTaiNhanSu.Models
{
    public class Notification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Type { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
        public DateTime? ReadAt { get; set; }

        public User User { get; set; } = default!;
    }
}
