namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class UpdateShiftTemplateRequest
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public int? BreakDurationMinutes { get; set; }
        public string? Description { get; set; }
    }
}
