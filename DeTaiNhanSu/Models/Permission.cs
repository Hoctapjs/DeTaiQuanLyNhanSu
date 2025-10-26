namespace DeTaiNhanSu.Models
{
    public class Permission
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string? Description { get; set; }
        public ICollection<RolePermission> RolePermissions { get; set; } = [];
    }
}
