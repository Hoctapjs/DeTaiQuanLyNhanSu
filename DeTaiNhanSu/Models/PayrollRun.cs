using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class PayrollRun
    {
        public Guid Id { get; set; }
        public string Period { get; set; } = default!;
        public PayrollRunStatus Status { get; set; }
        public ICollection<Salary> Salaries { get; set; } = [];
    }
}
