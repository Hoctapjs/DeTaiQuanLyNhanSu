using FirebaseAdmin.Messaging;
using NotificationFireBase = FirebaseAdmin.Messaging.Notification;
using DeTaiNhanSu.Models;

namespace DeTaiNhanSu.Services.Notification
{
    public class FirebaseMessagingService : IFirebaseMessagingService
    {
        public async Task SendNotificationAsync(Models.Notification notification, List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                Console.WriteLine(" Không có token nào.");
                return;
            }

            var message = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new NotificationFireBase
                {
                    Title = notification.Title,
                    Body = notification.Content,
                    ImageUrl = "https://image.vietstock.vn/2025/07/16/HNR-ava_930363.png"
                },
                Data = new Dictionary<string, string>
                {
                    { "notificationId", notification.Id.ToString() },
                    { "type", notification.Type },
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK",
                        ChannelId = "com.company.AppVietStock.general"

                    }
                }
            };

            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);


                Console.WriteLine($"Gửi thành công {response.SuccessCount}/{tokens.Count}");
                foreach (var result in response.Responses)
                {
                    Console.WriteLine($" Result: Success = {result.IsSuccess}, MessageId = {result.MessageId}");
                    if (!result.IsSuccess)
                        Console.WriteLine($" Error: {result.Exception}");
                }
            }
            catch (FirebaseMessagingException fcmEx)
            {
                Console.WriteLine("FirebaseMessagingException: " + fcmEx.Message);
                Console.WriteLine("StatusCode: " + fcmEx.HttpResponse?.StatusCode);


                if (fcmEx.InnerException != null)
                    Console.WriteLine("Inner: " + fcmEx.InnerException.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi chung: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner: " + ex.InnerException.Message);
                    if (ex.InnerException.InnerException != null)
                        Console.WriteLine("Inner sâu hơn: " + ex.InnerException.InnerException.Message);
                }
            }
        }
    }
}
