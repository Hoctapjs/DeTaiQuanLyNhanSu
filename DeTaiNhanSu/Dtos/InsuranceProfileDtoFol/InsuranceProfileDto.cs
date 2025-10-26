namespace DeTaiNhanSu.Dtos.InsuranceProfileDtoFol
{
    public class InsuranceProfileDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }

        public string? EmployeeFullName { get; set; }   // NEW
        public Guid? DepartmentId { get; set; }         // NEW
        public string? DepartmentName { get; set; }     // NEW
        public Guid? PositionId { get; set; }           // NEW
        public string? PositionName { get; set; }       // NEW

        public decimal? Bhxh { get; set; }
        public decimal? Bhyt { get; set; }
        public decimal? Bhtn { get; set; }
    }
}
