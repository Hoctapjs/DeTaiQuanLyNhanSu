using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class Employee
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string FullName { get; set; } = default!;
        public Gender? Gender { get; set; }
        public DateOnly? Dob { get; set; }
        public string? Cccd { get; set; }
        public string Email { get; set; } = default!;
        public string? Phone { get; set; } 
        public string? Address { get; set; }
        public DateOnly HireDate { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? PositionId { get; set; }
        public EmployeeStatus Status { get; set; }
        public string? AvatarUrl { get; set; }

        public Department? Department { get; set; }
        public Position? Position { get; set; }
        public User? User { get; set; }
        public ICollection<Attendance> Attendances { get; set; } = [];

    }
}
