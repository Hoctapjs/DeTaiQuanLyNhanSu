namespace DeTaiNhanSu.Dtos
{
    public sealed class RoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int UsersCount { get; set; }
        public List<PermissionLiteDto> Permissions { get; set; } = [];
    }
}
