namespace DeTaiNhanSu.Models
{
    public class Department
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public Guid? ManagerId { get; set; }
        public Employee? Manager { get; set; }
        public ICollection<Employee> Employees { get; set; } = [];
    }
}
