using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class Attendance
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly? CheckIn { get; set; }
        public TimeOnly? CheckOut { get; set; }
        public AttendanceStatus Status { get; set; }
        public string? Note { get; set; }

        public Employee Employee { get; set; } = default!;
    }
}
