namespace DeTaiNhanSu.Models
{
    public class Course
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Provider { get; set; }
        public int? Hours { get; set; }
    }
}
