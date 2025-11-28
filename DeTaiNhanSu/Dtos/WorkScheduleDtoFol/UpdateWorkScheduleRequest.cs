namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class UpdateWorkScheduleRequest
    {
        public Guid? EmployeeId { get; set; }
        public DateOnly? Date { get; set; }

        public Guid? ShiftTemplateId { get; set; } // <--- Thay thế cho StartTime/EndTime/Shift

        public string? Note { get; set; }
    }
}
