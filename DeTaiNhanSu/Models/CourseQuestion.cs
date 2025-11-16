namespace DeTaiNhanSu.Models
{
    public class CourseQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CourseId { get; set; }
        public Course Course { get; set; } = default!;
        public string Content { get; set; } = default!;
        public string A { get; set; } = default!;
        public string B { get; set; } = default!;
        public string C { get; set; } = default!;
        public string D { get; set; } = default!;
        public string Correct { get; set; } = "A";
    }
}
