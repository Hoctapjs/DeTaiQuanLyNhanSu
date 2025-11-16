using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseResultDtoFol
{
    public class SubmitCourseAnswerRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }
        [Required]
        public Guid CourseId { get; set; }
        [Required]
        public Guid QuestionId { get; set; }

        [Required, RegularExpression("^(A|B|C|D)$", ErrorMessage = "Chosen phải là A/B/C/D.")]
        public string Chosen { get; set; } = default!;
    }
}
