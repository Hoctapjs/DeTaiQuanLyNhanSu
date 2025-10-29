using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos
{
    public class GlobalSettingDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CreateGlobalSettingRequest
    {
        [Required]
        public string Key { get; set; } = string.Empty;
        [Required]
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
