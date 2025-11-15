using DeTaiNhanSu.Models;

namespace DeTaiNhanSu.Services.Notification
{
    public interface IFirebaseMessagingService
    {
        Task SendNotificationAsync(Models.Notification notification, List<string> tokens);
    }
}
