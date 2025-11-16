using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Services.Hubs;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Services.Notification
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<PublicNotificationHub> _hubContext;
        private readonly IFirebaseMessagingService _firebaseService;
        private readonly IDeviceStatusService _deviceStatusService;
        private readonly AppDbContext _context;

        // Helper method để lấy thời gian Việt Nam
        private static DateTime GetVietnamTime()
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
        }
        public NotificationService(
               IHubContext<PublicNotificationHub> hubContext,
               IFirebaseMessagingService firebaseService,
               IDeviceStatusService deviceStatusService,
               AppDbContext context)
        {
            _hubContext = hubContext;
            _firebaseService = firebaseService;
            _deviceStatusService = deviceStatusService;
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách User ID có hợp đồng còn hiệu lực
        /// </summary>
        private async Task<List<Guid>> GetUsersWithActiveContractsAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            var activeUserIds = await _context.Users
                .Where(u => u.Employee != null && // User phải có Employee
                           _context.Contracts.Any(c => 
                               c.EmployeeId == u.Employee.Id &&
                               c.Status == ContractStatus.active &&
                               c.StartDate <= today &&
                               (c.EndDate == null || c.EndDate >= today))) // Hợp đồng vô thời hạn hoặc chưa hết hạn
                .Select(u => u.Id)
                .ToListAsync();

            return activeUserIds;
        }

        // Method chính cho việc gửi thông báo HR
        public async Task SendHRNotificationAsync(Models.Notification notification, List<Guid>? targetUserIds = null)
        {
            Console.WriteLine($"📋 Sending HR notification: {notification.Title}");

            var notificationData = new
            {
                id = notification.Id,
                title = notification.Title,
                content = notification.Content, // Đổi "Body" thành "content"
                type = notification.Type,
                createdAt = notification.CreatedAt, // Đổi "Timestamp" thành "createdAt"
                isRead = false
            };

            try
            {
                // 1️⃣ Lưu Notification vào database
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // 2️⃣ Lấy danh sách user có hợp đồng còn hiệu lực
                var usersWithActiveContracts = await GetUsersWithActiveContractsAsync();
                
                if (!usersWithActiveContracts.Any())
                {
                    Console.WriteLine("⚠️ No users with active contracts found. Notification not sent.");
                    return;
                }

                // 3️⃣ Tạo UserNotification cho từng user
                List<UserNotification> userNotifications = new List<UserNotification>();

                if (targetUserIds != null && targetUserIds.Any())
                {
                    // Gửi cho danh sách user cụ thể, nhưng chỉ những user có hợp đồng còn hiệu lực
                    var filteredUserIds = targetUserIds.Intersect(usersWithActiveContracts).ToList();
                    
                    if (!filteredUserIds.Any())
                    {
                        Console.WriteLine($"⚠️ None of the target users have active contracts. Original count: {targetUserIds.Count}, Filtered count: 0");
                        return;
                    }

                    Console.WriteLine($"📊 Target users filtered: {targetUserIds.Count} -> {filteredUserIds.Count} (only users with active contracts)");

                    foreach (var userId in filteredUserIds)
                    {
                        userNotifications.Add(new UserNotification
                        {
                            NotificationId = notification.Id,
                            UserId = userId,
                            ReadAt = null // Chưa đọc
                        });
                    }
                }
                else
                {
                    // Gửi cho tất cả user có hợp đồng còn hiệu lực (broadcast)
                    Console.WriteLine($"📊 Broadcasting to {usersWithActiveContracts.Count} users with active contracts");

                    foreach (var userId in usersWithActiveContracts)
                    {
                        userNotifications.Add(new UserNotification
                        {
                            NotificationId = notification.Id,
                            UserId = userId,
                            ReadAt = null // Chưa đọc
                        });
                    }
                }

                // Lưu UserNotifications
                _context.UserNotifications.AddRange(userNotifications);
                await _context.SaveChangesAsync();

                // 4️⃣ Gửi realtime notifications (SignalR + Firebase)
                var userIdsToNotify = userNotifications.Select(un => un.UserId).ToList();
                
                var openDevices = await _deviceStatusService.GetOpenDevicesAsync();
                var closedDevices = await _deviceStatusService.GetClosedDevicesAsync();

                if (userIdsToNotify.Any())
                {
                    openDevices = openDevices.Where(id => userIdsToNotify.Contains(Guid.Parse(id))).ToList();
                    closedDevices = closedDevices.Where(id => userIdsToNotify.Contains(Guid.Parse(id))).ToList();
                }

                Console.WriteLine($"📱 Devices - Open: {openDevices.Count}, Closed: {closedDevices.Count}");

                // 5️⃣ Gửi SignalR cho thiết bị đang mở
                if (openDevices.Any())
                {
                    Console.WriteLine("📡 Sending SignalR HR notification...");
                    await _hubContext.Clients.All.SendAsync("ReceiveHRNotification", notificationData);
                }

                // 6️⃣ Gửi Firebase cho thiết bị đã đóng
                if (closedDevices.Any())
                {
                    Console.WriteLine("🔥 Sending FCM HR notification...");
                    var closedTokens = await _context.FcmTokens
                        .Where(t => closedDevices.Contains(t.UserId) && !string.IsNullOrEmpty(t.Token))
                        .Select(t => t.Token)
                        .ToListAsync();

                    if (closedTokens.Any())
                    {
                        await _firebaseService.SendNotificationAsync(notification, closedTokens);
                    }
                }

                Console.WriteLine($"✅ HR notification sent to {userNotifications.Count} users with active contracts!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending HR notification: {ex.Message}");
                throw;
            }
        }

        public async Task SendPayrollNotificationAsync(string title, string content, List<Guid>? targetUserIds = null)
        {
            var notification = new Models.Notification
            {
                Id = Guid.NewGuid(),
                Type = "payroll",
                Title = title,
                Content = content,
                CreatedAt = GetVietnamTime(), // Sử dụng thời gian Việt Nam
                ActorId = null, // Thông báo hệ thống không có ActorId
                ActionUrl = null // Có thể null hoặc gán URL nếu cần
            };

            await SendHRNotificationAsync(notification, targetUserIds);
        }

        public async Task SendAttendanceNotificationAsync(string title, string content, List<Guid>? targetUserIds = null)
        {
            var notification = new Models.Notification
            {
                Id = Guid.NewGuid(),
                Type = "attendance",
                Title = title,
                Content = content,
                CreatedAt = GetVietnamTime(), // Sử dụng thời gian Việt Nam
                ActorId = null, // Thông báo hệ thống không có ActorId
                ActionUrl = null // Có thể null hoặc gán URL nếu cần
            };

            await SendHRNotificationAsync(notification, targetUserIds);
        }

        public async Task SendLeaveRequestNotificationAsync(string title, string content, List<Guid>? targetUserIds = null)
        {
            var notification = new Models.Notification
            {
                Id = Guid.NewGuid(),
                Type = "leave_request",
                Title = title,
                Content = content,
                CreatedAt = GetVietnamTime(), // Sử dụng thời gian Việt Nam
                ActorId = null, // Thông báo hệ thống không có ActorId
                ActionUrl = null // Có thể null hoặc gán URL nếu cần
            };

            await SendHRNotificationAsync(notification, targetUserIds);
        }
    }
}
