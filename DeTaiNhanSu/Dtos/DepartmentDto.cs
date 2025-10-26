namespace DeTaiNhanSu.Dtos
{
    public sealed class DepartmentDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public Guid? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public int EmployeesCount { get; set; }
    }
}
