using DeTaiNhanSu.Controllers;
using DeTaiNhanSu.Models;

namespace DeTaiNhanSu.Services.Notification
{
    public interface INotificationService
    {

        // Gửi thông báo HR
        Task SendHRNotificationAsync(Models.Notification notification, List<Guid>? targetUserIds = null);
        Task SendPayrollNotificationAsync(string title, string content, List<Guid>? targetUserIds = null);
        Task SendAttendanceNotificationAsync(string title, string content, List<Guid>? targetUserIds = null);
        Task SendLeaveRequestNotificationAsync(string title, string content, List<Guid>? targetUserIds = null);
        /// <summary>
        /// Cập nhật thông báo và trả về true nếu thành công, false nếu không tìm thấy
        /// </summary>
        Task<bool> UpdateNotificationAsync(Guid notificationId, UpdateNotificationRequest request);

        /// <summary>
        /// Xóa thông báo cho 1 user và trả về true nếu thành công, false nếu không tìm thấy
        /// </summary>
  
    }
}
