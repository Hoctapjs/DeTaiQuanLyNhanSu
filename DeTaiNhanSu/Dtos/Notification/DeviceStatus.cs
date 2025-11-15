namespace DeTaiNhanSu.Dtos.Notification
{
    public class DeviceStatus
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public bool IsAppOpen { get; set; }
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
        public string? FcmToken { get; set; }
    }
}
