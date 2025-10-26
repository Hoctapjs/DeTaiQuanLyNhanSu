namespace DeTaiNhanSu.Dtos.OvertimeDtoFol
{
    public class CreateOvertimeRequest
    {
        public Guid EmployeeId { get; set; }
        public DateOnly? Date { get; set; }
        public decimal? Hours { get; set; }
        public decimal? Rate { get; set; }
        public string? Reason { get; set; }
    }
}
