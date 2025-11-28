using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;
    }
}
