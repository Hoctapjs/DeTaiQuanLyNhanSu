using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeTaiNhanSu.Models
{
    public class Notification
    {
        [Key]
        public Guid Id { get; set; }

        // Thời gian tạo (sẽ được cấu hình default trong DbContext)
        public DateTime CreatedAt { get; set; }

        // ID của người gây ra hành động (sếp, hệ thống)
        public Guid? ActorId { get; set; }

        // Đường dẫn điều hướng trong app MAUI (VD: //LeaveRequestDetail?id=123)
        [MaxLength(500)]
        public string? ActionUrl { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = default!;

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = default!;

        [Required]
        public string Content { get; set; } = default!;

        // --- Navigation Properties (Quan hệ) ---

        // Liên kết đến người GÂY RA HÀNH ĐỘNG (ActorId)
        [ForeignKey("ActorId")]
        public User? Actor { get; set; }

        // Mối quan hệ MỘT-NHIỀU với bảng nối UserNotification
        public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    }
}