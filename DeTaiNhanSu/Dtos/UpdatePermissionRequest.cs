using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class UpdatePermissionRequest
    {
        [MaxLength(50)]
        public string? Code { get; set; } // Cho phép sửa code nếu cần

        [MaxLength(255)]
        public string? Description { get; set; }
    }
}
