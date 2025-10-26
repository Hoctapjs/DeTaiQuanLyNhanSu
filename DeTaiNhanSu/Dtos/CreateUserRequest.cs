using DeTaiNhanSu.Enums;
using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class CreateUserRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }
        [Required, MaxLength(100)]
        public string? UserName { get; set; }
        [Required]
        public Guid RoleId { get; set; }
        public UserStatus? Status { get; set; } = UserStatus.active;
        [MaxLength(100)]
        public string? TempPassword { get; set; }
    }
}
