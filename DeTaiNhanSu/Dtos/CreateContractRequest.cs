using DeTaiNhanSu.Enums;
using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class CreateContractRequest
    {
        [Required] public Guid EmployeeId { get; set; }
        //[Required, MaxLength(100)] public string? ContractNumber { get; set; }
        [MaxLength(255)] public string? Title { get; set; }
        [Required] public ContractType Type { get; set; }
        public DateOnly? SignedDate { get; set; }
        [Required] public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public WorkType? WorkType { get; set; } = DeTaiNhanSu.Enums.WorkType.fulltime;
        [Required] public decimal BasicSalary { get; set; }
        public decimal? InsuranceSalary { get; set; }
        public Guid? RepresentativeId { get; set; }
        [Required] public ContractStatus Status { get; set; }
        [MaxLength(500)] public string? AttachmentUrl { get; set; }
        public string? Notes { get; set; }
    }
}
