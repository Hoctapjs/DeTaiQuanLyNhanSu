namespace DeTaiNhanSu.Dtos.CourseQuestionDtoFol
{
    public class CourseQuestionDto
    {
        public Guid Id { get; set; }
        public Guid CourseId { get; set; }
        public string CourseName { get; set; } = default!;

        // câu hỏi
        public string Content { get; set; } = default!;

        // nội dung các câu trả lời
        public string A { get; set; } = default!;
        public string B { get; set; } = default!;
        public string C { get; set; } = default!;
        public string D { get; set; } = default!;

        // đáp án đúng
        public string Correct { get; set; } = default!;
    }
}
