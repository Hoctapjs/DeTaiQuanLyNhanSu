using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Threading.Tasks;

// NOTE: Cần thêm các using cho các Models còn thiếu (Position, Department, User, Contract, AppDbContext)

namespace DeTaiNhanSu.Controllers
{
    // =================================================================
    //                  DTOs (Data Transfer Objects)
    // =================================================================
    public class CheckinRequest
    {
        public string EmployeeId { get; set; } = default!;
        public string WifiName { get; set; } = default!;
        public string Bssid { get; set; } = default!;
        public string Shift { get; set; } = default!;
    }

    public class CheckoutRequest
    {
        public string EmployeeId { get; set; } = default!;
        public string WifiName { get; set; } = default!;
        public string Bssid { get; set; } = default!;
    }


    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AttendanceController(AppDbContext context) => _context = context;

        // Phương thức hỗ trợ lấy ngày và giờ hiện tại theo múi giờ Việt Nam
        private (DateOnly Date, TimeOnly Time) GetVnTime()
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
            // Dùng DateOnly/TimeOnly (yêu cầu .NET 6 trở lên)
            return (DateOnly.FromDateTime(vnNow), TimeOnly.FromDateTime(vnNow));
        }

        // Phương thức hỗ trợ tạo phản hồi lỗi nhất quán
        private IActionResult CreateErrorResponse(int statusCode, string message)
        {
            return StatusCode(statusCode, new
            {
                statusCode = statusCode,
                success = false,
                message = message
            });
        }


        // Thêm struct lưu trữ cấu hình
        private struct AttendanceConfig
        {
            public int CheckinToleranceMinutes { get; set; }
            public TimeOnly AbsentMarkThresholdTime { get; set; }
            public int EarlyLeaveToleranceMinutes { get; set; }
            public int SevereEarlyLeaveMinutes { get; set; }
            public TimeOnly AutoCheckoutTime { get; set; }
        }

        // Hàm đọc cấu hình từ GlobalSettings
        private async Task<AttendanceConfig> GetAttendanceConfig(CancellationToken ct)
        {
            var settings = await _context.GlobalSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith("CHECKIN_") || s.Key.StartsWith("ABSENT_") || s.Key.StartsWith("EARLY_") || s.Key.StartsWith("SEVERE_") || s.Key == "AUTO_CHECKOUT_TIME")
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

            int ParseInt(string key, int defaultValue) =>
                settings.TryGetValue(key, out var val) && int.TryParse(val, out var result) ? result : defaultValue;

            TimeOnly ParseTime(string key, TimeOnly defaultValue) =>
                settings.TryGetValue(key, out var val) && TimeOnly.TryParse(val, out var result) ? result : defaultValue;

            return new AttendanceConfig
            {
                CheckinToleranceMinutes = ParseInt("CHECKIN_TOLERANCE_MINUTES", 5),
                AbsentMarkThresholdTime = ParseTime("ABSENT_MARK_THRESHOLD_TIME", new TimeOnly(10, 0, 0)),
                EarlyLeaveToleranceMinutes = ParseInt("EARLY_LEAVE_TOLERANCE_MINUTES", 5),
                SevereEarlyLeaveMinutes = ParseInt("SEVERE_EARLY_LEAVE_MINUTES", 10),
                AutoCheckoutTime = ParseTime("AUTO_CHECKOUT_TIME", new TimeOnly(23, 59, 0))
            };
        }

        // Hàm lấy Ca làm việc (ShiftTemplate) dựa trên Lịch làm việc (WorkSchedule)
        private async Task<ShiftTemplate?> GetEmployeeShiftForToday(Guid empId, DateOnly today, CancellationToken ct = default)
        {
            var schedule = await _context.WorkSchedules
                .AsNoTracking()
                .Include(ws => ws.ShiftTemplate)
                .FirstOrDefaultAsync(ws => ws.EmployeeId == empId && ws.Date == today, ct);

            return schedule?.ShiftTemplate;
        }

        // =================================================================
        //                 PHƯƠNG THỨC HỖ TRỢ DB/LOGIC
        // =================================================================
        private async Task<List<(string WifiName, string Bssid)>> GetAllowedWifisFromDb()
        {
            var wifiConfigs = await _context.GlobalSettings
                .Where(s => s.Key.StartsWith("WIFI_BSSID_"))
                .ToListAsync();

            return wifiConfigs.Select(w =>
            {
                string wifiName = w.Description?.Split(':').Last().Trim() ?? string.Empty;
                return (WifiName: wifiName, Bssid: w.Value);
            }).ToList();
        }

        // Kiểm tra xem ngày đó có nằm trong kỳ nghỉ phép đã được duyệt hay không
        private async Task<bool> IsDayApprovedForLeave(Guid empId, DateOnly date)
        {
            return await _context.Requests
                .AnyAsync(r => r.EmployeeId == empId
                            && r.Status == RequestStatus.approved
                            && r.Category == RequestCategory.leave
                            && r.FromDate.HasValue && r.FromDate.Value <= date
                            && r.ToDate.HasValue && r.ToDate.Value >= date);
        }

        // ------------------- CHECK-IN -------------------
        [HttpPost("checkin")]
        public async Task<IActionResult> Checkin([FromBody] CheckinRequest request, CancellationToken ct = default)
        {
            if (!Guid.TryParse(request.EmployeeId, out var empId)) return CreateErrorResponse(400, "EmployeeId không hợp lệ.");
            var (today, vnNowTime) = GetVnTime();

            // 1. LẤY CẤU HÌNH & CA LÀM VIỆC
            var config = await GetAttendanceConfig(ct);
            var shift = await GetEmployeeShiftForToday(empId, today, ct);

            // 2. KIỂM TRA LỊCH LÀM VIỆC & NGÀY NGHỈ
            if (await IsDayApprovedForLeave(empId, today))
                return CreateErrorResponse(400, "Bạn đã được duyệt đơn nghỉ phép hôm nay nên không cần checkin.");

            if (shift == null)
            {
                // Kiểm tra ngày nghỉ cuối tuần từ cấu hình
                var weekendSetting = await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "WEEKEND_DAYS", ct);
                string weekendValue = weekendSetting?.Value ?? "Saturday,Sunday";
                var weekendDays = weekendValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => Enum.TryParse(s, true, out DayOfWeek day) ? (DayOfWeek?)day : null)
                                             .Where(d => d.HasValue).Select(d => d.Value).ToList();

                if (weekendDays.Contains(today.DayOfWeek))
                    return CreateErrorResponse(400, $"Hôm nay là {today.DayOfWeek}, ngày nghỉ cuối tuần theo cấu hình.");

                return CreateErrorResponse(400, "Không tìm thấy lịch làm việc được xếp cho bạn hôm nay. Vui lòng liên hệ HR.");
            }

            // 3. KIỂM TRA WIFI & TRẠNG THÁI (Giữ nguyên logic cũ nhưng thay config)
            var allowedWifis = await GetAllowedWifisFromDb();
            if (!allowedWifis.Any(w => string.Equals(w.Bssid, request.Bssid, StringComparison.OrdinalIgnoreCase)))
                return CreateErrorResponse(400, "Bạn không kết nối WiFi công ty.");

            var existing = await _context.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == empId && a.Date == today, ct);
            if (existing != null && existing.CheckIn != null)
                return CreateErrorResponse(400, $"Bạn đã check-in hôm nay lúc {existing.CheckIn:HH:mm:ss} rồi!");

            // 4. XỬ LÝ TRẠNG THÁI (Dùng Shift StartTime & Config Tolerance)
            var shiftStartTime = shift.StartTime;
            var lateThreshold = shiftStartTime.Add(TimeSpan.FromMinutes(config.CheckinToleranceMinutes));

            AttendanceStatus status;
            string note;

            if (vnNowTime <= lateThreshold)
            {
                status = AttendanceStatus.present;
                note = $"Đúng giờ - Ca: {shift.Code}";
            }
            else
            {
                status = AttendanceStatus.late;
                TimeSpan lateTime = vnNowTime > shiftStartTime ? vnNowTime - shiftStartTime : TimeSpan.Zero;
                note = $"Đi muộn {(int)lateTime.TotalMinutes} phút - Ca: {shift.Code}";
            }

            // 5. LƯU DATABASE
            if (existing == null)
            {
                _context.Attendances.Add(new Attendance { Id = Guid.NewGuid(), EmployeeId = empId, Date = today, CheckIn = vnNowTime, Status = status, Note = note });
            }
            else
            {
                existing.CheckIn = vnNowTime;
                existing.Status = status;
                existing.Note = note;
            }

            await _context.SaveChangesAsync(ct);
            return Ok(new { statusCode = 200, Success = true, Message = $"Check-in thành công. Trạng thái: {status}" });
        }

        // ------------------- CHECK-OUT -------------------
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct = default)
        {
            if (!Guid.TryParse(request.EmployeeId, out var empId)) return CreateErrorResponse(400, "EmployeeId không hợp lệ.");
            var (today, vnNowTime) = GetVnTime();

            // 1. LẤY CẤU HÌNH & CA LÀM VIỆC
            var config = await GetAttendanceConfig(ct);
            var shift = await GetEmployeeShiftForToday(empId, today, ct);

            var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == empId && a.Date == today, ct);

            if (attendance == null || attendance.CheckIn == null) return CreateErrorResponse(400, "Bạn chưa check-in hôm nay!");
            if (attendance.CheckOut != null) return CreateErrorResponse(400, "Bạn đã check-out rồi!");
            if (shift == null) return CreateErrorResponse(500, "Lỗi: Không tìm thấy ca làm việc.");

            // 2. CHECK WIFI (Giữ nguyên)
            var allowedWifis = await GetAllowedWifisFromDb();
            if (!allowedWifis.Any()) return CreateErrorResponse(500, "Lỗi cấu hình: Danh sách WiFi công ty chưa được thiết lập.");

            var isAllowed = allowedWifis.Any(w => string.Equals(w.WifiName, request.WifiName, StringComparison.OrdinalIgnoreCase) && string.Equals(w.Bssid, request.Bssid, StringComparison.OrdinalIgnoreCase));
            if (!isAllowed) return CreateErrorResponse(400, "Bạn không kết nối WiFi công ty. Vui lòng kết nối để checkout");

            // 3. XỬ LÝ VỀ SỚM / OT (Dựa trên Shift EndTime & Config)
            TimeOnly shiftEndTime = shift.EndTime;
            string checkoutNote = "";

            // Ngưỡng cho phép về sớm (Ví dụ: EndTime - 5 phút)
            var earlyLeaveAllowedTime = shiftEndTime.Add(TimeSpan.FromMinutes(-config.EarlyLeaveToleranceMinutes));

            // Ngưỡng tính OT (Ví dụ: EndTime + 5 phút)
            var otThresholdTime = shiftEndTime.Add(TimeSpan.FromMinutes(config.EarlyLeaveToleranceMinutes));

            if (vnNowTime < earlyLeaveAllowedTime)
            {
                // VỀ SỚM
                var earlyDuration = shiftEndTime - vnNowTime;
                var minutesEarly = Math.Round(earlyDuration.TotalMinutes);

                if (earlyDuration.TotalMinutes > config.SevereEarlyLeaveMinutes)
                    checkoutNote += $" | Về sớm {minutesEarly} phút [PHẠT NẶNG]";
                else
                    checkoutNote += $" | Về sớm {minutesEarly} phút";
            }
            else if (vnNowTime > otThresholdTime)
            {
                // TĂNG CA (OT)
                var otDuration = vnNowTime - shiftEndTime;
                var minutesOT = Math.Round(otDuration.TotalMinutes);
                checkoutNote += $" | Về trễ {minutesOT} phút";
            }

            // 4. CẬP NHẬT
            attendance.CheckOut = vnNowTime;
            if (attendance.Status == AttendanceStatus.present)
            {
                attendance.Status = AttendanceStatus.completed;
            }
            else if (attendance.Status == AttendanceStatus.leave)
            {
                attendance.Status = AttendanceStatus.leave;
            }
            else if (attendance.Status == AttendanceStatus.late)
            {
                attendance.Status = AttendanceStatus.late;
            }
            else
            {
                attendance.Status = AttendanceStatus.absent;
            }    
            attendance.Note = (attendance.Note ?? "") + checkoutNote;

            await _context.SaveChangesAsync(ct);
            return Ok(new { statusCode = 200, Success = true, Message = "Check-out thành công!" });
        }


        // ------------------- Tự động kiểm tra nhân viên có vắng mặt ko -------------------
        [HttpPost("mark-absent")]
        public async Task<IActionResult> MarkAbsent(CancellationToken ct = default)
        {
            var (today, vnNowTime) = GetVnTime();

            // Lấy giờ giới hạn từ GlobalSettings
            var config = await GetAttendanceConfig(ct);
            var absentThresholdTime = config.AbsentMarkThresholdTime;

            var weekendSetting = await _context.GlobalSettings
                 .AsNoTracking()
                 .FirstOrDefaultAsync(s => s.Key == "WEEKEND_DAYS");

            // Phân tích các ngày nghỉ (Mặc định: T7, CN nếu không tìm thấy cấu hình)
            string weekendValue = weekendSetting?.Value ?? "Saturday, Sunday";
            var weekendDays = weekendValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(s => Enum.TryParse(s, true, out DayOfWeek day) ? (DayOfWeek?)day : null)
                                          .Where(d => d.HasValue)
                                          .Select(d => d.Value)
                                          .ToList();

            // Kiểm tra nếu hôm nay là ngày cuối tuần
            if (weekendDays.Contains(today.DayOfWeek))
            {
                return Ok(new { Success = true, Message = "Hệ thống không đánh dấu vắng mặt vào ngày nghỉ cuối tuần (dựa trên cấu hình GlobalSettings)." });
            }
            var activeEmployees = await _context.Employees.Where(e => e.Status == EmployeeStatus.active).ToListAsync(ct);
            int markedCount = 0;

            foreach (var emp in activeEmployees)
            {
                // Lấy lịch làm việc
                var schedule = await _context.WorkSchedules
                    .AsNoTracking()
                    .Include(ws => ws.ShiftTemplate)
                    .FirstOrDefaultAsync(ws => ws.EmployeeId == emp.Id && ws.Date == today, ct);

                if (schedule == null) continue;

                // CHỈ XỬ LÝ nếu giờ hiện tại >= giờ ngưỡng VÀ ca làm việc bắt đầu <= giờ ngưỡng
                if (vnNowTime >= absentThresholdTime && schedule.ShiftTemplate.StartTime <= absentThresholdTime)
                {
                    // Kiểm tra chưa check-in và không có phép
                    bool isApprovedLeave = await IsDayApprovedForLeave(emp.Id, today);
                    if (!isApprovedLeave)
                    {
                        var att = await _context.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == emp.Id && a.Date == today, ct);
                        if (att == null || att.CheckIn == null)
                        {
                            if (att == null)
                            {
                                _context.Attendances.Add(new Attendance
                                {
                                    Id = Guid.NewGuid(),
                                    EmployeeId = emp.Id,
                                    Date = today,
                                    Status = AttendanceStatus.absent,
                                    Note = $"Vắng (Ca {schedule.ShiftTemplate.Code} - Auto {vnNowTime:HH:mm})"
                                });
                            }
                            else
                            {
                                att.Status = AttendanceStatus.absent;
                                att.Note = $"Vắng (Ca {schedule.ShiftTemplate.Code} - Auto {vnNowTime:HH:mm})";
                            }
                            markedCount++;
                        }
                    }
                }
            }
            await _context.SaveChangesAsync(ct);
            return Ok(new { Success = true, Message = $"Đã đánh dấu vắng cho {markedCount} nhân viên." });
        }
        [HttpGet("status/{employeeId}")]
        public async Task<IActionResult> GetAttendanceStatus(Guid employeeId)
        {
            var (today, vnNowTime) = GetVnTime();

            // 1. [MỚI] Lấy thông tin Ca làm việc dự kiến
            // (Hàm này đã được thêm vào AttendanceController ở bước trước)
            var shift = await GetEmployeeShiftForToday(employeeId, today);

            // 2. [MỚI] Nếu không có lịch, kiểm tra xem có phải ngày nghỉ cuối tuần không
            if (shift == null)
            {
                var weekendSetting = await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "WEEKEND_DAYS");
                string weekendValue = weekendSetting?.Value ?? "Saturday,Sunday";
                var weekendDays = weekendValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => Enum.TryParse(s, true, out DayOfWeek day) ? (DayOfWeek?)day : null)
                                             .Where(d => d.HasValue).Select(d => d.Value).ToList();

                if (weekendDays.Contains(today.DayOfWeek))
                {
                    return Ok(new
                    {
                        Success = true,
                        Status = "Weekend",
                        Message = $"Hôm nay là {today.DayOfWeek}, ngày nghỉ cuối tuần."
                    });
                }

                return Ok(new
                {
                    Success = true,
                    Status = "NoSchedule",
                    Message = "Không có lịch làm việc được xếp hôm nay."
                });
            }

            // Chuẩn bị thông tin ca để trả về cho Mobile App hiển thị
            var shiftInfo = new
            {
                Code = shift.Code,
                Name = shift.Name,
                StartTime = shift.StartTime.ToString("HH:mm"),
                EndTime = shift.EndTime.ToString("HH:mm")
            };

            // 3. Lấy thông tin chấm công thực tế
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today);

            // --- CÁC TRƯỜNG HỢP TRẢ VỀ ---

            if (attendance == null)
            {
                return Ok(new
                {
                    Success = true,
                    Status = "NotCheckedIn",
                    Message = $"Bạn chưa check-in. Ca {shift.Code} bắt đầu lúc {shiftInfo.StartTime}.",
                    Shift = shiftInfo // [MỚI] Trả về thông tin ca
                });
            }

            if (attendance.Status == AttendanceStatus.leave)
            {
                return Ok(new
                {
                    Success = true,
                    Status = AttendanceStatus.leave.ToString(),
                    Message = "Bạn đã được duyệt nghỉ phép hôm nay.",
                    Shift = shiftInfo
                });
            }

            if (attendance.Status == AttendanceStatus.absent)
            {
                return Ok(new
                {
                    Success = true,
                    Status = AttendanceStatus.absent.ToString(),
                    Message = "Bạn đã bị đánh dấu vắng mặt hôm nay.",
                    Shift = shiftInfo
                });
            }

            if (attendance.CheckOut == null)
            {
                return Ok(new
                {
                    Success = true,
                    Status = "CheckedIn",
                    Message = $"Đã check-in ({attendance.CheckIn:HH:mm}). Ca kết thúc lúc {shiftInfo.EndTime}.",
                    Shift = shiftInfo
                });
            }

            return Ok(new
            {
                Success = true,
                Status = attendance.Status.ToString(), // Completed/Late/Present
                Message = "Bạn đã hoàn tất chấm công hôm nay.",
                Shift = shiftInfo
            });
        }
        [HttpPost("auto-checkout")]
        public async Task<IActionResult> AutoCheckout(CancellationToken ct = default)
        {
            var (today, _) = GetVnTime();

            // Lấy giờ auto-checkout từ config
            var config = await GetAttendanceConfig(ct);
            var autoCheckoutTime = config.AutoCheckoutTime;

            var pending = await _context.Attendances
                .Where(a => a.Date == today && a.CheckOut == null && a.Status != AttendanceStatus.absent)
                .ToListAsync(ct);

            if (!pending.Any()) return Ok(new { Success = true, Message = "Không có nhân viên cần auto-checkout." });

            foreach (var att in pending)
            {
                att.CheckOut = autoCheckoutTime;
                if (att.Status == AttendanceStatus.present) att.Status = AttendanceStatus.completed;
                att.Note = (att.Note ?? "") + $" | Auto-out {autoCheckoutTime:HH:mm}";
            }

            await _context.SaveChangesAsync(ct);
            return Ok(new { Success = true, Message = $"Auto check-out cho {pending.Count} nhân viên." });
        }


        // Các phương thức Hangfire (Đã vô hiệu hóa)
        [HttpPost("update-mark-absent-time")]
        public IActionResult UpdateMarkAbsentTime([FromQuery] string cron)
        {
            if (string.IsNullOrEmpty(cron))
                return CreateErrorResponse(400, "Thiếu biểu thức cron!");

            return Ok(new
            {
                Success = true,
                Message = $"Đã cập nhật giờ chạy job MarkAbsent thành công. (Cron: {cron})",
                Cron = cron
            });
        }

        [HttpPost("update-auto-checkout-time")]
        public IActionResult UpdateAutoCheckoutTime([FromQuery] string cron)
        {
            if (string.IsNullOrEmpty(cron))
                return CreateErrorResponse(400, "Thiếu biểu thức cron!");

            return Ok(new
            {
                Success = true,
                Message = $"Đã cập nhật giờ chạy job AutoCheckout thành công. (Cron: {cron})",
                Cron = cron
            });
        }


        [HttpGet]
        public async Task<IActionResult> GetAttendances(
        [FromQuery] string? q,
        [FromQuery] int current = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "Date desc")
        {
            var initialQuery = _context.Attendances
                .Include(a => a.Employee)
                .AsQueryable();

            IQueryable<Attendance> query = initialQuery;

            // 1. Lọc theo chuỗi tìm kiếm 'q'
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(a =>
                    (a.Employee != null && a.Employee.FullName.Contains(q)) ||
                    (a.Employee != null && a.Employee.Code.Contains(q)) ||
                    (a.Note != null && a.Note.Contains(q)) ||
                    a.Status.ToString().Contains(q)
                );

                if (await initialQuery.AnyAsync() && !await query.AnyAsync())
                {
                    string supportedSearchFields = "Tên NV, Mã NV, Trạng thái (status), hoặc Ghi chú (note).";
                    return CreateErrorResponse(400, $"Không tìm thấy kết quả nào cho '{q}'. Vui lòng tìm kiếm theo: {supportedSearchFields}");
                }
            }

            // 2. Tính tổng số lượng và phân trang
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            string responseMessage = totalCount == 0 && string.IsNullOrEmpty(q)
                ? "Hệ thống chưa có bản ghi chấm công nào."
                : $"Tìm thấy {totalCount} bản ghi chấm công.";

            List<dynamic> attendanceList = new List<dynamic>();

            // 3. Sắp xếp và phân trang - BỌC TRONG KHỐI TRY-CATCH
            try
            {
                if (totalCount > 0)
                {
                    var tempAttendanceList = await query
                        .OrderBy(sort)
                        .Skip((current - 1) * pageSize)
                        .Take(pageSize)
                        .Select(a => new
                        {
                            id = a.Id,
                            employeeId = a.EmployeeId,
                            employeeName = a.Employee != null ? a.Employee.FullName : "N/A",
                            date = a.Date.ToString("yyyy-MM-dd"),
                            checkIn = a.CheckIn.HasValue ? a.CheckIn.Value.ToString("HH:mm:ss") : null,
                            checkOut = a.CheckOut.HasValue ? a.CheckOut.Value.ToString("HH:mm:ss") : null,
                            status = a.Status.ToString(),
                            note = a.Note
                        })
                        .ToListAsync();

                    attendanceList.AddRange(tempAttendanceList.Cast<dynamic>());
                }
            }
            catch (ParseException ex)
            {
                string supportedFields = "Date, Status, Note, Employee.FullName. (Thêm ' asc' hoặc ' desc')";
                return CreateErrorResponse(400, $"Lỗi sắp xếp: Tên cột '{sort}' không hợp lệ. Hỗ trợ sắp xếp theo: {supportedFields}");
            }
            catch (Exception)
            {
                throw;
            }

            // 4. Trả về Response
            return Ok(new
            {
                statusCode = 200,
                message = responseMessage,
                data = new[]
                {
                    new
                    {
                        meta = new
                        {
                            current = current,
                            pageSize = pageSize,
                            pages = totalPages,
                            total = totalCount
                        },
                        result = attendanceList
                    }
                },
                success = true
            });
        }
    }
}