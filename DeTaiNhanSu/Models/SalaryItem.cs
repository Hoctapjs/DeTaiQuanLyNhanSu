using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class SalaryItem
    {
        public Guid Id { get; set; }
        public Guid SalaryId { get; set; }
        public SalaryItemType Type { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }

        public Salary Salary { get; set; } = default!;
    }
}
