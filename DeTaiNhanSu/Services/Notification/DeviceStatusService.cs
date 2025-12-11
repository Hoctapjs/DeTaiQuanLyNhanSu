using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.Notification;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Services.Notification
{
    public class DeviceStatusService : IDeviceStatusService
    {
        private readonly AppDbContext _context;

        public DeviceStatusService(AppDbContext context)
        {
            _context = context;
        }

        public async Task UpdateDeviceStatusAsync(string userId, string connectionId, bool isAppOpen, string fcmToken)
        {
            Console.WriteLine($"Updating status for User: {userId} - Token: {fcmToken?.Substring(0, 5)}... = {(isAppOpen ? "Open" : "Closed")}");

            // 1. Kiểm tra input quan trọng
            if (string.IsNullOrEmpty(fcmToken))
            {
                Console.WriteLine("Warning: FcmToken is empty during status update.");
                return;
            }

            // 2. Tìm thiết bị cụ thể dựa trên CẶP KHÓA (UserId + FcmToken)
            // Thay vì chỉ tìm theo DeviceId (UserId), ta tìm chính xác hàng có Token đó.
            var existingDevice = await _context.DeviceStatuses
                .FirstOrDefaultAsync(d => d.DeviceId == userId && d.FcmToken == fcmToken);

            if (existingDevice != null)
            {
                // TRƯỜNG HỢP UPDATE: Tìm thấy đúng thiết bị này của user này
                existingDevice.ConnectionId = connectionId;
                existingDevice.IsAppOpen = isAppOpen;
                existingDevice.LastUpdateTime = DateTime.UtcNow;
                // FcmToken đã khớp rồi nên không cần update lại, nhưng nếu muốn chắc chắn:
                existingDevice.FcmToken = fcmToken;

                Console.WriteLine($"Updated existing session for User {userId}");
            }
            else
            {
                // TRƯỜNG HỢP INSERT: User này đăng nhập trên một thiết bị MỚI (Token mới)
                var newDevice = new DeviceStatus
                {
                    DeviceId = userId, // Lưu UserId vào cột DeviceId như logic cũ của bạn
                    ConnectionId = connectionId,
                    IsAppOpen = isAppOpen,
                    LastUpdateTime = DateTime.UtcNow,
                    FcmToken = fcmToken // Lưu ngay lập tức Token được gửi lên
                };
                _context.DeviceStatuses.Add(newDevice);

                Console.WriteLine($"Created NEW session for User {userId} on new device");
            }

            // 3. (Tùy chọn) Đồng bộ ngược lại bảng FcmTokens nếu cần
            // Logic cũ của bạn đang lấy Token từ bảng FcmTokens đắp vào bảng DeviceStatus.
            // Logic mới: Token từ Client là nguồn sự thật (Source of Truth), nên ta làm ngược lại:
            // Kiểm tra xem token này đã có trong bảng FcmTokens chưa, nếu chưa thì thêm vào.
            var tokenExists = await _context.FcmTokens
                .AnyAsync(f => f.UserId == userId && f.Token == fcmToken);

            if (!tokenExists)
            {
                _context.FcmTokens.Add(new FcmToken
                {
                    UserId = userId,
                    Token = fcmToken,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsDeviceAppOpenAsync(string deviceId)
        {
            var device = await _context.DeviceStatuses
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId);

            var isOpen = device?.IsAppOpen ?? false;
            Console.WriteLine($" Device {deviceId} app status: {(isOpen ? "Open" : "Closed")}");
            return isOpen;
        }

        public async Task<List<string>> GetOpenDevicesAsync()
        {
            var openDevices = await _context.DeviceStatuses
                .Where(d => d.IsAppOpen)
                .Select(d => d.DeviceId)
                .ToListAsync();

            Console.WriteLine($" Found {openDevices.Count} devices with app OPEN");
            foreach (var device in openDevices)
            {
                Console.WriteLine($"    {device.Substring(0, 8)}...");
            }
            return openDevices;
        }

        public async Task<List<string>> GetClosedDevicesAsync()
        {
            var closedDevices = await _context.DeviceStatuses
                .Where(d => !d.IsAppOpen)
                .Select(d => d.DeviceId)
                .ToListAsync();

            Console.WriteLine($" Found {closedDevices.Count} devices with app CLOSED");
            foreach (var device in closedDevices)
            {
                Console.WriteLine($"  {device.Substring(0, 8)}...");
            }
            return closedDevices;
        }

        public async Task RemoveDeviceByConnectionAsync(string connectionId)
        {
            var device = await _context.DeviceStatuses
                .FirstOrDefaultAsync(d => d.ConnectionId == connectionId);

            if (device != null)
            {
                // Mark as closed instead of removing
                device.IsAppOpen = false;
                device.LastUpdateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                Console.WriteLine($" Device {device.DeviceId} marked as closed (connection {connectionId} lost)");
            }
        }

        public async Task<List<DeviceStatus>> GetAllDeviceStatusesAsync()
        {
            return await _context.DeviceStatuses
                .OrderByDescending(d => d.LastUpdateTime)
                .ToListAsync();
        }
    }
}
