using DeTaiNhanSu.Services.Notification;
using Microsoft.AspNetCore.SignalR;

namespace DeTaiNhanSu.Services.Hubs
{
    public class PublicNotificationHub : Hub
    {
        private readonly IDeviceStatusService _deviceStatusService;

        public PublicNotificationHub(IDeviceStatusService deviceStatusService)
        {
            _deviceStatusService = deviceStatusService;
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"📡 Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"📡 Client disconnected: {Context.ConnectionId}");
            await _deviceStatusService.RemoveDeviceByConnectionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // ✅ Client sẽ gọi khi app mở/tắt → server lưu trạng thái thiết bị
        public async Task UpdateDeviceStatus(string deviceId, bool isAppOpen)
        {
            await _deviceStatusService.UpdateDeviceStatusAsync(deviceId, Context.ConnectionId, isAppOpen);
            Console.WriteLine($"📲 Device {deviceId} is {(isAppOpen ? "OPEN" : "CLOSED")}");
        }
    }
}
