using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos
{
    public class ContractDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = default!;
        public string EmployeeName { get; set; } = default!;
        public string ContractNumber { get; set; } = default!;
        public string? Title { get; set; }
        public ContractType Type { get; set; }
        public DateOnly? SignedDate { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public WorkType WorkType { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal? InsuranceSalary { get; set; }
        public Guid? RepresentativeId { get; set; }
        public string? RepresentativeUserName { get; set; }
        public ContractStatus Status { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? Notes { get; set; }
    }
}
