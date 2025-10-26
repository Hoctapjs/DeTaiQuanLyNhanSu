namespace DeTaiNhanSu.Dtos
{
    public class PermissionLiteDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string? Description { get; set; }
    }
}
