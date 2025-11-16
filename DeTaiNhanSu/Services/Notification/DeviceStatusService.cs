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

        public async Task UpdateDeviceStatusAsync(string deviceId, string connectionId, bool isAppOpen)
        {
            Console.WriteLine($"Updating device status: {deviceId} = {(isAppOpen ? "Open" : "Closed")}");

            var existingDevice = await _context.DeviceStatuses
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId);

            if (existingDevice != null)
            {
                // C?p nh?t device hi?n có
                existingDevice.ConnectionId = connectionId;
                existingDevice.IsAppOpen = isAppOpen;
                existingDevice.LastUpdateTime = DateTime.UtcNow;
            }
            else
            {
                // T?o m?i device status
                var newDevice = new DeviceStatus
                {
                    DeviceId = deviceId,
                    ConnectionId = connectionId,
                    IsAppOpen = isAppOpen,
                    LastUpdateTime = DateTime.UtcNow
                };
                _context.DeviceStatuses.Add(newDevice);
            }

            // L?y FCM token t? b?ng FcmTokens
            var fcmToken = await _context.FcmTokens
                .Where(f => f.UserId == deviceId)
                .Select(f => f.Token)
                .FirstOrDefaultAsync();

            if (existingDevice != null)
            {
                existingDevice.FcmToken = fcmToken;
            }
            else
            {
                var device = _context.DeviceStatuses.Local.FirstOrDefault(d => d.DeviceId == deviceId);
                if (device != null)
                {
                    device.FcmToken = fcmToken;
                }
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($" Device status updated: {deviceId}");
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
