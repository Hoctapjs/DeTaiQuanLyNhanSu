using DeTaiNhanSu.Enums;
using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public sealed class UpdateUserRequest
    {
        [Required, MaxLength(100)]
        public string? UserName { get; set; }
        [Required]
        public Guid RoleId { get; set; }
        public UserStatus? Status { get; set; }
    }
}
