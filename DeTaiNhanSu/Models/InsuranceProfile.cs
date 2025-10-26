namespace DeTaiNhanSu.Models
{
    public class InsuranceProfile
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public decimal? Bhxh { get; set; }
        public decimal? Bhyt { get; set; }
        public decimal? Bhtn { get; set; }

        public Employee Employee { get; set; } = default!;
    }
}
