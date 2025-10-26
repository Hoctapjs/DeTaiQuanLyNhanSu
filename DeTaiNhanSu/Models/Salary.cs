using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class Salary
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid PayrollRunId { get; set; }
        public decimal Gross { get; set; }
        public decimal Net { get; set; }
        public string? Details { get; set; }

        public Employee Employee { get; set; } = default!;
        public PayrollRun PayrollRun { get; set; } = default!;
        public ICollection<SalaryItem> Items { get; set; } = [];
    }
}
