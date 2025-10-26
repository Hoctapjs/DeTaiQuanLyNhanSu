namespace DeTaiNhanSu.Models
{
    public class Role
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public ICollection<User> Users { get; set; } = [];
        public ICollection<RolePermission> RolePermissions { get; set; } = [];
    }
}
