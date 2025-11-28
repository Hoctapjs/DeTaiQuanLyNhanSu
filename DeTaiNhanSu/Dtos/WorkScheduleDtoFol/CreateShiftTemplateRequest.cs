using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class CreateShiftTemplateRequest
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = default!; // VD: HC, CA1

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = default!; // VD: Hành Chính

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        public int BreakDurationMinutes { get; set; } = 0;

        public string? Description { get; set; }
    }
}
