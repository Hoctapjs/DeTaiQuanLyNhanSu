using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos
{
    public sealed class UserDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = default!;
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = default!;
        public UserStatus Status { get; set; }
        public DateTime? LastLoginAt { get; set; }

        public Guid EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = default!;
        public string EmployeeName { get; set; } = default!;
        public string EmployeeEmail { get; set; } = default!;
    }
}
