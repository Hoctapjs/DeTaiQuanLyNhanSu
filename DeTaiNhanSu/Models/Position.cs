namespace DeTaiNhanSu.Models
{
    public class Position
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Level { get; set; }
        public ICollection<Employee> Employees { get; set; } = [];
    }
}
