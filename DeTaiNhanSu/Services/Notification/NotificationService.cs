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

                // 4️⃣ LOGIC MỚI: Phân luồng gửi theo trạng thái từng thiết bị
                var userIdsToNotifyStrings = userNotifications.Select(un => un.UserId.ToString()).ToList();

                // Lấy TẤT CẢ thiết bị của danh sách user cần gửi
                // (Lưu ý: Cột DeviceId trong bảng DeviceStatuses đang chứa UserId)
                var allUserDevices = await _context.DeviceStatuses
                    .Where(d => userIdsToNotifyStrings.Contains(d.DeviceId))
                    .ToListAsync();

                // Tạo 2 danh sách chứa đích đến
                var signalRConnectionIds = new List<string>();
                var firebaseTokens = new List<string>();

                // Duyệt qua từng thiết bị để phân loại
                foreach (var device in allUserDevices)
                {
                    if (device.IsAppOpen && !string.IsNullOrEmpty(device.ConnectionId))
                    {
                        // ✅ Thiết bị đang MỞ -> Gửi SignalR
                        signalRConnectionIds.Add(device.ConnectionId);
                    }
                    else if (!device.IsAppOpen && !string.IsNullOrEmpty(device.FcmToken))
                    {
                        // 💤 Thiết bị đang ĐÓNG -> Gửi Firebase
                        firebaseTokens.Add(device.FcmToken);
                    }
                }

                Console.WriteLine($"📊 Routing Summary:");
                Console.WriteLine($"   - Online Devices (SignalR): {signalRConnectionIds.Count}");
                Console.WriteLine($"   - Offline Devices (Firebase): {firebaseTokens.Count}");

                // 5️⃣ Gửi SignalR cho các thiết bị đang mở (Gửi song song)
                if (signalRConnectionIds.Any())
                {
                    Console.WriteLine("📡 Sending SignalR to open devices...");
                    // Dùng Clients.Clients để gửi đến danh sách ConnectionId cụ thể
                    await _hubContext.Clients.Clients(signalRConnectionIds).SendAsync("ReceiveHRNotification", notificationData);
                }

                // 6️⃣ Gửi Firebase cho các thiết bị đang đóng (Gửi song song)
                if (firebaseTokens.Any())
                {
                    Console.WriteLine("🔥 Sending FCM to closed devices...");
                    // FcmTokens có thể trùng lặp nếu 1 thiết bị đăng ký lại nhiều lần, nên Distinct() để tối ưu
                    var uniqueTokens = firebaseTokens.Distinct().ToList();
                    await _firebaseService.SendNotificationAsync(notification, uniqueTokens);
                }

                Console.WriteLine($"✅ Notification process completed!");
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
        // ✅ HÀM UPDATE HOÀN CHỈNH (LOGIC ROUTING GIỐNG SendHRNotificationAsync)
        // ==========================================================
        public async Task<bool> UpdateNotificationAsync(Guid notificationId, UpdateNotificationRequest request)
        {
            // LOGIC PHÂN LUỒNG: Ưu tiên UserId
            if (request.UserId.HasValue)
            {
                // ===============================================
                // TRƯỜNG HỢP 1: CÓ UserId -> "Tạo bản sao" (Clone)
                // ===============================================
                if (request.TargetUserIds != null)
                {
                    Console.WriteLine($"CẢNH BÁO (UpdateNotificationAsync): Cả UserId ({request.UserId.Value}) và TargetUserIds được cung cấp. " +
                                      $"Hệ thống sẽ ƯU TIÊN UserId và BỎ QUA TargetUserIds.");
                }

                // Logic "Tạo bản sao" - Xóa cũ, Thêm mới
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

                // ✅ LOGIC ROUTING MỚI: Xử lý TẤT CẢ thiết bị của user (giống SendHRNotificationAsync)
                string userIdString = request.UserId.Value.ToString();

                // Lấy TẤT CẢ thiết bị của user này
                var userDevices = await _context.DeviceStatuses
                    .Where(d => d.DeviceId == userIdString)
                    .ToListAsync();

                if (userDevices.Any())
                {
                    // Tạo 2 danh sách chứa đích đến
                    var signalRConnectionIds = new List<string>();
                    var firebaseTokens = new List<string>();

                    // Duyệt qua từng thiết bị để phân loại
                    foreach (var device in userDevices)
                    {
                        if (device.IsAppOpen && !string.IsNullOrEmpty(device.ConnectionId))
                        {
                            // ✅ Thiết bị đang MỞ -> Gửi SignalR
                            signalRConnectionIds.Add(device.ConnectionId);
                        }
                        else if (!device.IsAppOpen && !string.IsNullOrEmpty(device.FcmToken))
                        {
                            // 💤 Thiết bị đang ĐÓNG -> Gửi Firebase
                            firebaseTokens.Add(device.FcmToken);
                        }
                    }

                    Console.WriteLine($"📊 Update Routing Summary for User {userIdString}:");
                    Console.WriteLine($"   - Online Devices (SignalR): {signalRConnectionIds.Count}");
                    Console.WriteLine($"   - Offline Devices (Firebase): {firebaseTokens.Count}");

                    // Gửi SignalR cho các thiết bị đang mở
                    if (signalRConnectionIds.Any())
                    {
                        Console.WriteLine("📡 Sending SignalR update to open devices...");
                        await _hubContext.Clients.Clients(signalRConnectionIds).SendAsync("ReceiveNotificationUpdate", newNotification);
                    }

                    // Gửi Firebase cho các thiết bị đang đóng
                    if (firebaseTokens.Any())
                    {
                        Console.WriteLine("🔥 Sending FCM update to closed devices...");
                        var uniqueTokens = firebaseTokens.Distinct().ToList();
                        await _firebaseService.SendNotificationAsync(newNotification, uniqueTokens);
                    }
                }
                else
                {
                    // Fallback: Nếu không tìm thấy trong DeviceStatus, tìm trong FcmTokens
                    var fcmTokens = await _context.FcmTokens
                        .Where(t => t.UserId == userIdString && !string.IsNullOrEmpty(t.Token))
                        .Select(t => t.Token)
                        .ToListAsync();

                    if (fcmTokens.Any())
                    {
                        Console.WriteLine($"🔥 Fallback: Sending FCM update to all tokens for user {userIdString}");
                        await _firebaseService.SendNotificationAsync(newNotification, fcmTokens);
                    }
                }

                return true;
            }
            else
            {
                // ===============================================
                // TRƯỜNG HỢP 2: KHÔNG CÓ UserId -> Update (Admin)
                // ===============================================
                var notification = await _context.Notifications.FindAsync(notificationId);
                if (notification == null) return false;

                // Cập nhật thông báo gốc
                notification.Title = request.Title;
                notification.Content = request.Content;
                notification.Type = request.Type ?? notification.Type;
                notification.ActorId = request.ActorId ?? notification.ActorId;
                notification.ActionUrl = request.ActionUrl;
                notification.CreatedAt = GetVietnamTime(); // ✅ THÊM DÒNG NÀY: Cập nhật thời gian tạo

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
                    var linksToRemove = currentLinks
                        .Where(l => !targetUserIdsSet.Contains(l.UserId))
                        .ToList();

                    // 2. Tìm các UserId để THÊM MỚI
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

                // Lưu tất cả thay đổi
                await _context.SaveChangesAsync();

                // ===============================================
                // ✅ LOGIC ROUTING MỚI: Xử lý TẤT CẢ user và thiết bị (giống SendHRNotificationAsync)
                // ===============================================

                // Lấy TẤT CẢ user liên quan sau khi sync
                var allInvolvedUserIds = await _context.UserNotifications
                    .Where(un => un.NotificationId == notificationId)
                    .Select(un => un.UserId)
                    .Distinct()
                    .ToListAsync();

                if (!allInvolvedUserIds.Any()) return true; // Không có ai để gửi

                var allInvolvedUserIdStrings = allInvolvedUserIds.Select(g => g.ToString()).ToList();

                // Lấy TẤT CẢ thiết bị của danh sách user cần gửi
                var allUserDevices = await _context.DeviceStatuses
                    .Where(d => allInvolvedUserIdStrings.Contains(d.DeviceId))
                    .ToListAsync();

                // Tạo 2 danh sách chứa đích đến
                var signalRConnectionIds = new List<string>();
                var firebaseTokens = new List<string>();

                // Duyệt qua từng thiết bị để phân loại
                foreach (var device in allUserDevices)
                {
                    if (device.IsAppOpen && !string.IsNullOrEmpty(device.ConnectionId))
                    {
                        // ✅ Thiết bị đang MỞ -> Gửi SignalR
                        signalRConnectionIds.Add(device.ConnectionId);
                    }
                    else if (!device.IsAppOpen && !string.IsNullOrEmpty(device.FcmToken))
                    {
                        // 💤 Thiết bị đang ĐÓNG -> Gửi Firebase
                        firebaseTokens.Add(device.FcmToken);
                    }
                }

                Console.WriteLine($"📊 Update Admin Routing Summary:");
                Console.WriteLine($"   - Total Users: {allInvolvedUserIds.Count}");
                Console.WriteLine($"   - Online Devices (SignalR): {signalRConnectionIds.Count}");
                Console.WriteLine($"   - Offline Devices (Firebase): {firebaseTokens.Count}");

                // Gửi SignalR cho các thiết bị đang mở (song song)
                if (signalRConnectionIds.Any())
                {
                    Console.WriteLine("📡 Sending SignalR update to open devices...");
                    await _hubContext.Clients.Clients(signalRConnectionIds).SendAsync("ReceiveNotificationUpdate", notification);
                }

                // Gửi Firebase cho các thiết bị đang đóng (song song)
                if (firebaseTokens.Any())
                {
                    Console.WriteLine("🔥 Sending FCM update to closed devices...");
                    var uniqueTokens = firebaseTokens.Distinct().ToList();
                    await _firebaseService.SendNotificationAsync(notification, uniqueTokens);
                }

                // Fallback: Xử lý các user không có trong DeviceStatus (gửi tất cả FCM tokens)
                var usersWithoutDeviceStatus = allInvolvedUserIdStrings
                    .Where(userId => !allUserDevices.Any(d => d.DeviceId == userId))
                    .ToList();

                if (usersWithoutDeviceStatus.Any())
                {
                    var fallbackTokens = await _context.FcmTokens
                        .Where(t => usersWithoutDeviceStatus.Contains(t.UserId) && !string.IsNullOrEmpty(t.Token))
                        .Select(t => t.Token)
                        .ToListAsync();

                    if (fallbackTokens.Any())
                    {
                        Console.WriteLine($"🔥 Fallback: Sending FCM to {fallbackTokens.Count} tokens for users without device status");
                        await _firebaseService.SendNotificationAsync(notification, fallbackTokens);
                    }
                }

                return true;
            }
        }

        public async Task SendLeaveRequestNotificationAsync(string title, string content, Guid targetUserId)
        {
            var notification = new Models.Notification
            {
                Id = Guid.NewGuid(),
                Type = "new", // Hoặc "new" tùy bạn định nghĩa
                Title = title,
                Content = content,
                CreatedAt = GetVietnamTime(),
                ActorId = null,
                ActionUrl = null
            };

            // ✅ GỌI LẠI HÀM GỐC: Đóng gói 1 user vào List để tái sử dụng logic phân luồng
            await SendHRNotificationAsync(notification, new List<Guid> { targetUserId });
        }


        public async Task SendRewardPenaltyNotificationAsync(Guid targetUserId, string typeName, string kind, decimal amount, string? reason, DateOnly date)
        {
            // 1. Xác định tiêu đề và nội dung dựa trên loại (Thưởng hay Phạt)
            // kind thường là "Reward" hoặc "Penalty" từ Enum
            string titleStr = kind.Equals("Reward", StringComparison.OrdinalIgnoreCase)
                ? "Quyết định Khen thưởng mới"
                : "Quyết định Kỷ luật mới";

            string reasonStr = string.IsNullOrWhiteSpace(reason) ? typeName : reason;

            // Format tiền tệ (VD: 500,000 VND)
            string amountStr = amount.ToString("N0") + " VND";

            // Tạo nội dung thông báo
            string contentStr = $"Bạn có một quyết định {kind}: {typeName}.\n" +
                                $"Ngày: {date:dd/MM/yyyy}\n" +
                                $"Số tiền: {amountStr}\n" +
                                $"Lý do: {reasonStr}";

            // 2. Tạo đối tượng Notification Model
            var notification = new Models.Notification
            {
                Id = Guid.NewGuid(),
                Type = "new",
                Title = titleStr,
                Content = contentStr,
                CreatedAt = GetVietnamTime(),
                ActorId = null, // Hệ thống gửi hoặc lấy ID người quyết định nếu cần
                ActionUrl = null // Có thể link tới màn hình chi tiết nếu App hỗ trợ
            };

            // 3. Gọi hàm gửi chung (Tái sử dụng logic lưu DB, SignalR, Firebase)
            // Đóng gói targetUserId vào List để hàm xử lý đúng
            await SendHRNotificationAsync(notification, new List<Guid> { targetUserId });
        }


        public async Task SendTrainingNotificationAsync(string courseName, Guid targetUserId)
        {
            var notification = new Models.Notification
            {
                Id = Guid.NewGuid(),
                Type = "new", // Loại thông báo để App hiện icon sách vở/đào tạo
                Title = "Bạn có yêu cầu đào tạo mới",
                Content = $"Bạn vừa được phân bổ tham gia khóa học: {courseName}. Vui lòng tham gia khóa học",
                CreatedAt = GetVietnamTime(),
                ActorId = null,
                ActionUrl = null
            };

            // Tái sử dụng hàm gửi chung (lưu DB, bắn SignalR/Firebase)
            await SendHRNotificationAsync(notification, new List<Guid> { targetUserId });
        }
    }
}
