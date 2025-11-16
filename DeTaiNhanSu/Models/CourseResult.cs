namespace DeTaiNhanSu.Models
{
    public class CourseResult
    {
        public Guid EmployeeId { get; set; }
        public Guid CourseId { get; set; }
        public Guid QuestionId { get; set; }

        // câu đã chọn
        public string Chosen { get; set; } = "A";
        // kết quả của câu khi so sánh đáp án
        public bool IsCorrect { get; set; }
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

        public CourseQuestion Question { get; set; } = default!;
        public Course Course { get; set; } = default!;
    }
}
