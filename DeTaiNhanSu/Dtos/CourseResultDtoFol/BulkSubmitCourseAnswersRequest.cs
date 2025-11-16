using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseResultDtoFol
{
    public class BulkSubmitCourseAnswersRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }
        [Required]
        public Guid CourseId { get; set; }

        // Danh sách answer cho các QuestionId thuộc course
        [Required]
        public List<BulkAnswerItem> Answers { get; set; } = new();

        public sealed class BulkAnswerItem
        {
            [Required]
            public Guid QuestionId { get; set; }
            [Required, RegularExpression("^(A|B|C|D)$")]
            public string Chosen { get; set; } = default!;
        }

        public sealed class BulkSubmitRequest
        {
            public Guid EmployeeId { get; set; }
            public Guid CourseId { get; set; }
            public List<SubmitAnswerItem> Answers { get; set; } = new();
        }

        public sealed class SubmitAnswerItem
        {
            public Guid QuestionId { get; set; }
            public string Chosen { get; set; } = default!;
        }
    }
}
