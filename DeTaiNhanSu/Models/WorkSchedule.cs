namespace DeTaiNhanSu.Models
{
    public class WorkSchedule
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public string? Shift { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? Note { get; set; }

        public Employee Employee { get; set; } = default!;
    }
}
