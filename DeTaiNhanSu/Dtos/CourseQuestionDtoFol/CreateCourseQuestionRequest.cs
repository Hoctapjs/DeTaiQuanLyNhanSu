using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseQuestionDtoFol
{
    public class CreateCourseQuestionRequest
    {
        [Required]
        public Guid CourseId { get; set; }

        [Required, MinLength(3)]
        public string Content { get; set; } = default!;

        [Required, MinLength(1), StringLength(400)]
        public string A { get; set; } = default!;
        [Required, MinLength(1), StringLength(400)]
        public string B { get; set; } = default!;
        [Required, MinLength(1), StringLength(400)]
        public string C { get; set; } = default!;
        [Required, MinLength(1), StringLength(400)]
        public string D { get; set; } = default!;

        [Required, RegularExpression("^(A|B|C|D)$", ErrorMessage = "Correct phải là A/B/C/D.")]
        public string Correct { get; set; } = default!;
    }
}
