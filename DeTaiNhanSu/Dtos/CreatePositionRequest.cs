using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class CreatePositionRequest
    {
        [Required, MaxLength(200)]
        public string? Name { get; set; }
        [MaxLength(100)]
        public string? Level { get; set; }
    }
}
