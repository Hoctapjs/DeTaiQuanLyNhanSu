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
    }
}
