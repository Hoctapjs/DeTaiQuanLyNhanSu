using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class Request
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public RequestCategory Category { get; set; }
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public string? AttachmentUrl { get; set; }
        public RequestStatus Status { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public Employee Employee { get; set; } = default!;
        public User? ApprovedByUser { get; set; }
    }
}
