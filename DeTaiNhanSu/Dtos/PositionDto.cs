namespace DeTaiNhanSu.Dtos
{
    public class PositionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Level { get; set; }
        public int EmployeesCount { get; set; }
    }
}
