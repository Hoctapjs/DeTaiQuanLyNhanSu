using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseQuestionDtoFol
{
    public class UpdateCourseQuestionRequest
    {
        public Guid? CourseId { get; set; }

        [MinLength(3)]
        public string? Content { get; set; }

        [StringLength(400)]
        public string? A { get; set; }
        [StringLength(400)]
        public string? B { get; set; }
        [StringLength(400)]
        public string? C { get; set; }
        [StringLength(400)]
        public string? D { get; set; }

        [RegularExpression("^(A|B|C|D)$", ErrorMessage = "Correct phải là A/B/C/D.")]
        public string? Correct { get; set; }
    }
}
