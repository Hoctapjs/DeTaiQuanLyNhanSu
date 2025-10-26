namespace DeTaiNhanSu.Dtos.InsuranceProfileDtoFol
{
    public class CreateInsuranceProfileRequest
    {
        public Guid EmployeeId { get; set; }
        public decimal? Bhxh { get; set; }
        public decimal? Bhyt { get; set; }
        public decimal? Bhtn { get; set; }
    }
}
