using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class CreatePermissionRequest
    {
        [Required(ErrorMessage = "Mã quyền không được để trống")]
        [MaxLength(50)]
        public string Code { get; set; } = default!; // VD: Employees.View

        [MaxLength(255)]
        public string? Description { get; set; }
    }
}
