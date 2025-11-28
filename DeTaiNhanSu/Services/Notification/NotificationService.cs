using DeTaiNhanSu.Controllers;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
                Type = "new",
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
        // ==========================================================
        // ✅ HÀM UPDATE HOÀN CHỈNH (KHÔNG RESET ReadAt, CÓ SYNC TargetUserIds)
        // ==========================================================
        public async Task<bool> UpdateNotificationAsync(Guid notificationId, UpdateNotificationRequest request)
        {
            // LOGIC PHÂN LUỒNG: Ưu tiên UserId
            if (request.UserId.HasValue)
            {
                // ===============================================
                // TRƯỜNG HỢP 1: CÓ UserId -> "Tạo bản sao" (Clone)
                // (Logic này đã hoàn chỉnh, giữ nguyên)
                // ===============================================
                if (request.TargetUserIds != null)
                {
                    Console.WriteLine($"CẢNH BÁO (UpdateNotificationAsync): Cả UserId ({request.UserId.Value}) và TargetUserIds được cung cấp. " +
                                      $"Hệ thống sẽ ƯU TIÊN UserId và BỎ QUA TargetUserIds.");
                }

                // (Logic "Tạo bản sao" - Xóa cũ, Thêm mới)
                var oldUserNotificationLink = await _context.UserNotifications
                    .FirstOrDefaultAsync(un => un.NotificationId == notificationId && un.UserId == request.UserId.Value);
                if (oldUserNotificationLink == null) return false;
                var originalNotification = await _context.Notifications
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == notificationId);
                if (originalNotification == null) return false;
                var newNotification = new Models.Notification
                {
                    Id = Guid.NewGuid(),
                    Title = request.Title,
                    Content = request.Content,
                    Type = request.Type ?? originalNotification.Type,
                    ActorId = request.ActorId ?? originalNotification.ActorId,
                    ActionUrl = request.ActionUrl,
                    CreatedAt = GetVietnamTime()
                };

                _context.Notifications.Add(newNotification);
                _context.UserNotifications.Remove(oldUserNotificationLink);
                _context.UserNotifications.Add(new UserNotification
                {
                    NotificationId = newNotification.Id,
                    UserId = request.UserId.Value,
                    ReadAt = null
                });

                await _context.SaveChangesAsync();

                // 7. Gửi thông báo (SignalR + Firebase)
                string userIdString = request.UserId.Value.ToString();
                var device = await _context.DeviceStatuses.FirstOrDefaultAsync(d => d.DeviceId == userIdString);

                if (device != null && device.IsAppOpen && !string.IsNullOrEmpty(device.ConnectionId))
                {
                    // 7a. Gửi SignalR (App đang mở)
                    await _hubContext.Clients.Client(device.ConnectionId).SendAsync("ReceiveNotificationUpdate", newNotification);
                }
                else
                {
                    // 7b. ✅ GỬI FIREBASE (App đang đóng hoặc không tìm thấy device status)
                    var fcmTokens = await _context.FcmTokens
                        .Where(t => t.UserId == userIdString && !string.IsNullOrEmpty(t.Token))
                        .Select(t => t.Token)
                        .ToListAsync();

                    if (fcmTokens.Any())
                    {
                        Console.WriteLine($"🔥 Đang gửi FCM (Update User-Specific) đến: {userIdString}");
                        await _firebaseService.SendNotificationAsync(newNotification, fcmTokens);
                    }
                }
                return true;
            }
            else
            {
                // ===============================================
                // TRƯỜNG HỢP 2: KHÔNG CÓ UserId -> Update (Admin)
                // (PHẦN ĐÃ HOÀN THIỆN)
                // ===============================================
                var notification = await _context.Notifications.FindAsync(notificationId);
                if (notification == null) return false;

                // (Logic cập nhật thông báo gốc)
                notification.Title = request.Title;
                notification.Content = request.Content;
                notification.Type = request.Type ?? notification.Type;
                notification.ActorId = request.ActorId ?? notification.ActorId;
                notification.ActionUrl = request.ActionUrl;

                // (PHẦN RESET ReadAt ĐÃ BỊ XÓA THEO YÊU CẦU CỦA BẠN)

                // ===============================================
                // ✅ LOGIC SYNC LIST (PHẦN HOÀN CHỈNH)
                // ===============================================
                if (request.TargetUserIds != null)
                {
                    // Lấy danh sách liên kết user HIỆN TẠI
                    var currentLinks = await _context.UserNotifications
                        .Where(un => un.NotificationId == notificationId)
                        .ToListAsync();

                    var existingUserIds = currentLinks.Select(l => l.UserId).ToHashSet();
                    var targetUserIdsSet = request.TargetUserIds.ToHashSet();

                    // 1. Tìm các liên kết để XÓA
                    // (Những user có trong DB nhưng KHÔNG có trong danh sách mới)
                    var linksToRemove = currentLinks
                        .Where(l => !targetUserIdsSet.Contains(l.UserId))
                        .ToList();

                    // 2. Tìm các UserId để THÊM MỚI
                    // (Những user có trong danh sách mới nhưng KHÔNG có trong DB)
                    var userIdsToAdd = targetUserIdsSet
                        .Where(id => !existingUserIds.Contains(id))
                        .ToList();

                    // Thực hiện xóa
                    if (linksToRemove.Any())
                    {
                        _context.UserNotifications.RemoveRange(linksToRemove);
                    }

                    // Thực hiện thêm mới
                    if (userIdsToAdd.Any())
                    {
                        var linksToAdd = userIdsToAdd.Select(userId => new UserNotification
                        {
                            NotificationId = notificationId,
                            UserId = userId,
                            ReadAt = null // User mới luôn là 'chưa đọc'
                        });
                        _context.UserNotifications.AddRange(linksToAdd);
                    }
                }

                // Lưu tất cả thay đổi (cả Notification và UserNotification)
                await _context.SaveChangesAsync();

                // 4. Lấy TẤT CẢ user liên quan (logic gửi thông báo giữ nguyên)
                var allInvolvedUserIds = await _context.UserNotifications
                    .Where(un => un.NotificationId == notificationId)
                    .Select(un => un.UserId)
                    .Distinct()
                    .ToListAsync();

                if (!allInvolvedUserIds.Any()) return true; // Không có ai để gửi

                var allInvolvedUserIdStrings = allInvolvedUserIds.Select(g => g.ToString()).ToList();

                // 5. Gửi SignalR (App đang mở)
                var openDevices = await _context.DeviceStatuses
                    .Where(d => allInvolvedUserIdStrings.Contains(d.DeviceId) && d.IsAppOpen)
                    .Select(d => d.ConnectionId)
                    .ToListAsync();

                if (openDevices.Any())
                {
                    await _hubContext.Clients.Clients(openDevices).SendAsync("ReceiveNotificationUpdate", notification);
                }

                // 6. ✅ GỬI FIREBASE (App đang đóng)
                var closedDeviceUserIds = await _context.DeviceStatuses
                    .Where(d => allInvolvedUserIdStrings.Contains(d.DeviceId) && !d.IsAppOpen)
                    .Select(d => d.DeviceId) // Lấy UserId (string)
                    .ToListAsync();

                if (closedDeviceUserIds.Any())
                {
                    var closedTokens = await _context.FcmTokens
                        .Where(t => closedDeviceUserIds.Contains(t.UserId) && !string.IsNullOrEmpty(t.Token))
                        .Select(t => t.Token)
                        .ToListAsync();

                    if (closedTokens.Any())
                    {
                        Console.WriteLine($"🔥 Đang gửi FCM (Update Admin) đến {closedTokens.Count} thiết bị đang đóng...");
                        await _firebaseService.SendNotificationAsync(notification, closedTokens);
                    }
                }
                return true;
            }
        }

        public async Task SendLeaveRequestNotificationAsync(string title, string content, Guid targetUserId)
        {
            // 1. Tạo Notification (Lưu ý: KHÔNG gán UserId ở đây vì Model không có)
            var notification = new Models.Notification
            {
                Id = Guid.NewGuid(),
                Title = title,
                Content = content,
                Type = "new", // Loại thông báo
                CreatedAt = GetVietnamTime(),
                ActorId = null, // Có thể truyền ID người duyệt vào đây nếu muốn
                ActionUrl = null
            };

            // 2. Tạo liên kết trong bảng UserNotification (Đây mới là chỗ lưu người nhận)
            var userNotification = new UserNotification
            {
                // Giả định UserNotification có Id tự sinh hoặc bạn new Guid()
                NotificationId = notification.Id,
                UserId = targetUserId,
                ReadAt = null // Chưa đọc
            };

            // 3. Lưu vào Database (Lưu cả 2 bảng)
            _context.Notifications.Add(notification);
            _context.UserNotifications.Add(userNotification);

            await _context.SaveChangesAsync();

            // ================================================================
            // 4. Gửi Realtime (SignalR + Firebase)
            // ================================================================

            // Tạo object dữ liệu để gửi đi (Mapping cho khớp với App Client)
            var notificationPayload = new
            {
                id = notification.Id,
                title = notification.Title,
                content = notification.Content,
                type = notification.Type,
                createdAt = notification.CreatedAt,
                isRead = false // Mặc định là chưa đọc
            };

            string targetUserIdString = targetUserId.ToString();

            // A. Tìm thiết bị của user
            var userDevices = await _context.DeviceStatuses
                .Where(d => d.DeviceId == targetUserIdString)
                .ToListAsync();

            // B. Tách luồng Online (SignalR)
            var onlineConnectionIds = userDevices
                .Where(d => d.IsAppOpen && !string.IsNullOrEmpty(d.ConnectionId))
                .Select(d => d.ConnectionId)
                .ToList();

            if (onlineConnectionIds.Any())
            {
                // Gửi SignalR
                await _hubContext.Clients.Clients(onlineConnectionIds)
                    .SendAsync("ReceiveNotification", notificationPayload);

                Console.WriteLine($"✅ [SignalR] Sent to user {targetUserId}");
            }

            // C. Tách luồng Offline (Firebase)
            // Logic: Nếu không có thiết bị nào online thì gửi Firebase
            if (!onlineConnectionIds.Any())
            {
                var fcmTokens = await _context.FcmTokens
                    .Where(t => t.UserId == targetUserIdString)
                    .Select(t => t.Token)
                    .ToListAsync();

                if (fcmTokens.Any())
                {
                    // Gửi Firebase (Cần đảm bảo hàm SendNotificationAsync nhận đúng Model hoặc Payload)
                    // Vì hàm SendNotificationAsync của bạn nhận Model Notification, ta truyền notification vào
                    await _firebaseService.SendNotificationAsync(notification, fcmTokens);

                    Console.WriteLine($"🔥 [Firebase] Sent to user {targetUserId}");
                }
            }
        }

    }
}
