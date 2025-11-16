using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos
{
    public class EmployeeUpdateFormRequest
    {
        public string? Code { get; set; }
        public string? FullName { get; set; }
        public string? Cccd { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public Gender? Gender { get; set; }
        public EmployeeStatus? Status { get; set; }
        public DateOnly? Dob { get; set; }
        public DateOnly? HireDate { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? PositionId { get; set; }
    }
}
