using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public sealed class SetPermissionsRequest
    {
        [Required]
        public List<Guid> Permissions { get; set; } = [];
    }
}
