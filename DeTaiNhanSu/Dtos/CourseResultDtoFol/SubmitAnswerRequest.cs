namespace DeTaiNhanSu.Dtos.CourseResultDtoFol
{
    public class SubmitAnswerRequest
    {
        public Guid EmployeeId { get; set; }
        public Guid CourseId { get; set; }
        public Guid QuestionId { get; set; }
        public string Chosen { get; set; } = default!;
    }
}
