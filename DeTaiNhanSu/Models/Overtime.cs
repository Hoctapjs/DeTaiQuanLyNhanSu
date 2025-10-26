namespace DeTaiNhanSu.Models
{
    public class Overtime
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public decimal Hours { get; set; }
        public decimal Rate { get; set; }
        public string? Reason { get; set; }
        public Employee Employee { get; set; } = default!;
    }
}
