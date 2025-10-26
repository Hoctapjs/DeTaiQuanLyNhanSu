using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos
{
    public class EmployeeFlat
    {
        public Guid? Id { get; set; }
        public string? Code { get; set; }
        public string? FullName { get; set; }
        public Gender? Gender { get; set; }
        public DateOnly? Dob { get; set; }
        public string? Cccd { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public DateOnly? HireDate { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public Guid? PositionId { get; set; }
        public string? PositionName { get; set; }
        public EmployeeStatus Status { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
