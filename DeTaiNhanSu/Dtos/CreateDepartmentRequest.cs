using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class CreateDepartmentRequest
    {
        [Required, MaxLength(200)]
        public string? Name { get; set; }
        [MaxLength(1000)]
        public string? Description { get; set; } = default!;
        public Guid? ManagerId { get; set; }
    }
}
