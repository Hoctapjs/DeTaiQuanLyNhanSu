namespace DeTaiNhanSu.Dtos.CourseResultDtoFol
{
    public class CourseResultDto
    {
        public Guid EmployeeId { get; set; }
        public Guid CourseId { get; set; }
        public Guid QuestionId { get; set; }
        public string Chosen { get; set; } = default!;
        public bool IsCorrect { get; set; }
        public DateTime AnsweredAt { get; set; }

        public string? CourseName { get; set; }
        public string? QuestionContent { get; set; }
    }
}
