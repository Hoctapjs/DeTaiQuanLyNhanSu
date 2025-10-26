using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class CreateRoleRequest
    {
        [Required, MaxLength(255)]
        public string? Name { get; set; }
        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
