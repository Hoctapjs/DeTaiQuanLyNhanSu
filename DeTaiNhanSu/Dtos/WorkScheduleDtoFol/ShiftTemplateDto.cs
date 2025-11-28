namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class ShiftTemplateDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int BreakDurationMinutes { get; set; }
        public decimal TotalWorkingHours { get; set; }
        public string? Description { get; set; }

        // công
        public double WorkDay { get; set; }
    }
}
