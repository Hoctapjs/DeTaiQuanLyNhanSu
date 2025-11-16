namespace DeTaiNhanSu.Models
{
    public class Course
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        //public string? Provider { get; set; }

        public string? ClassCode { get; set; } = default!;

        public int PassThreshold { get; set; } = 70;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //public int? Hours { get; set; }
        public ICollection<CourseQuestion> Questions { get; set; } = new List<CourseQuestion>();

    }
}
