using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class CreateWorkScheduleRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required(ErrorMessage = "ShiftTemplateId là bắt buộc")]
        public Guid ShiftTemplateId { get; set; } // <--- Thay thế cho StartTime/EndTime/Shift

        public string? Note { get; set; }
    }
}
