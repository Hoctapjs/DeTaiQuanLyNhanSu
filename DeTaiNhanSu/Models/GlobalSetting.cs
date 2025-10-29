using System.ComponentModel.DataAnnotations.Schema;

namespace DeTaiNhanSu.Models
{
    [Table("GlobalSettings")]
    public class GlobalSetting
    {
        // Id theo quy ước EF Core, tự động nhận là Khóa chính (PK)
        public Guid Id { get; set; }

        // Cột Key, sẽ được cấu hình là UNIQUE trong DbContext
        [Column(TypeName = "nvarchar(100)")]
        public string Key { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string Value { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string? Description { get; set; }
    }
}
