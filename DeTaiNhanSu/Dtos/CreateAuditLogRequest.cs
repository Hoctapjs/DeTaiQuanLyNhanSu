namespace DeTaiNhanSu.Dtos
{
    public class CreateAuditLogRequest
    {
        public Guid UserId { get; set; }
        public string Action { get; set; } = default!;
        public string? TableName { get; set; }
        public string? RecordId { get; set; }
        public string? Description { get; set; }
        public string? IpAddress { get; set; }
    }
}
