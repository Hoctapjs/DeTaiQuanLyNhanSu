using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Models
{
    public class UserNotification
    {
        // Khóa ngoại tới Bảng Notifications
        public Guid NotificationId { get; set; }

        // Khóa ngoại tới Bảng Users
        public Guid UserId { get; set; }

        // Trạng thái đọc (chỉ của user này)
        public DateTime? ReadAt { get; set; }

        // Navigation properties
        public Notification Notification { get; set; } = default!;
        public User User { get; set; } = default!;
    }
}