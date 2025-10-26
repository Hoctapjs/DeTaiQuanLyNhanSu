using System.ComponentModel.DataAnnotations;
using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos
{
    public class CreateEmployeeRequest
    {
        [MaxLength(50)]
        public string? Code { get; set; }
        [Required, MaxLength(200)]
        public string FullName { get; set; } = default!;

        public Gender? Gender { get; set; }
        public DateOnly? Dob { get; set; }

        [MaxLength(30)]
        public string? Cccd { get; set; }
        [Required, EmailAddress, MaxLength(320)]
        public string? Email { get; set; }
        [MaxLength(20)]
        public string? Phone { get; set; }
        [MaxLength(500)]
        public string? Address { get; set; }
        public DateOnly? HireDate { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? PositionId { get; set; }
        public EmployeeStatus? Status { get; set; }
        public string? AvatarUrl { get; set; }

    }
}
