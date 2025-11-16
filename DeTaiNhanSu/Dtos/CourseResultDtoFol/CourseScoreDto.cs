namespace DeTaiNhanSu.Dtos.CourseResultDtoFol
{
    public class CourseScoreDto
    {
        public Guid EmployeeId { get; set; }
        public Guid CourseId { get; set; }
        public int TotalQuestions { get; set; }
        public int Answered { get; set; }
        public int Correct { get; set; }
        public decimal ScorePercent { get; set; } // 0..100
        public bool? Passed { get; set; }
    }
}
