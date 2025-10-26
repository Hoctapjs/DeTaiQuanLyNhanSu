using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class RenewContractRequest
    {
        [Required] public DateOnly? EndDate { get; set; }
        public string? Notes { get; set; }
    }
}
