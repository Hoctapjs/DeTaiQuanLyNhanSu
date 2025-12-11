using DeTaiNhanSu.Services.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeTaiNhanSu.Controllers.Notification
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceStatusController : ControllerBase
    {
        private readonly IDeviceStatusService _deviceStatusService;

        public DeviceStatusController(IDeviceStatusService deviceStatusService)
        {
            _deviceStatusService = deviceStatusService;
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllDeviceStatuses()
        {
            try
            {
                var devices = await _deviceStatusService.GetAllDeviceStatusesAsync();
                return Ok(devices);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("open")]
        public async Task<IActionResult> GetOpenDevices()
        {
            try
            {
                var openDevices = await _deviceStatusService.GetOpenDevicesAsync();
                return Ok(new { OpenDevices = openDevices, Count = openDevices.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("closed")]
        public async Task<IActionResult> GetClosedDevices()
        {
            try
            {
                var closedDevices = await _deviceStatusService.GetClosedDevicesAsync();
                return Ok(new { ClosedDevices = closedDevices, Count = closedDevices.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("check/{deviceId}")]
        public async Task<IActionResult> CheckDeviceStatus(string deviceId)
        {
            try
            {
                var isAppOpen = await _deviceStatusService.IsDeviceAppOpenAsync(deviceId);
                return Ok(new
                {
                    DeviceId = deviceId,
                    IsAppOpen = isAppOpen,
                    Status = isAppOpen ? "Open" : "Closed"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateDeviceStatus([FromBody] UpdateDeviceStatusRequest request)
        {
            try
            {
                // Truyền thêm request.FcmToken vào service
                await _deviceStatusService.UpdateDeviceStatusAsync(
                    request.DeviceId,
                    request.ConnectionId ?? "manual",
                    request.IsAppOpen,
                    request.FcmToken // Token từ client gửi lên
                );

                return Ok($"Device status updated for User {request.DeviceId}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }

    public class UpdateDeviceStatusRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? ConnectionId { get; set; }
        public bool IsAppOpen { get; set; }
        public string FcmToken { get; set; } = string.Empty; // Thêm trường này
    }
}
