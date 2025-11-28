namespace DeTaiNhanSu.Dtos
{
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string? Description { get; set; }
        public int RolesCount { get; set; }
    }
}
