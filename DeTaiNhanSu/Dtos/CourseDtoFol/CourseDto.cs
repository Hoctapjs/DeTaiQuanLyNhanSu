namespace DeTaiNhanSu.Dtos.CourseDtoFol
{
    public class CourseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? ClassCode { get; set; } = default!;
        public int PassThreshold { get; set; }
        public DateTime CreatedAt { get; set; }
        public int QuestionCount { get; set; }
    }
}
