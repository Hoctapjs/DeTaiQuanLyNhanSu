using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public sealed class SetRoleRequest
    {
        [Required]
        public Guid RoleId { get; set; }
    }
}
