namespace DeTaiNhanSu.Dtos.OvertimeDtoFol
{
    public class OvertimeDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }

        public string? EmployeeFullName { get; set; }   // NEW
        public Guid? DepartmentId { get; set; }         // NEW
        public string? DepartmentName { get; set; }     // NEW
        public Guid? PositionId { get; set; }           // NEW
        public string? PositionName { get; set; }       // NEW

        public DateOnly Date { get; set; }
        public decimal Hours { get; set; }
        public decimal Rate { get; set; }
        public string? Reason { get; set; }
    }
}
