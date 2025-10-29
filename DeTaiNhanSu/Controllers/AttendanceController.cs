using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Collections.Generic;
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
        public async Task<IActionResult> Checkin([FromBody] CheckinRequest request)
        {
            if (!Guid.TryParse(request.EmployeeId, out var empId)) return CreateErrorResponse(400, "EmployeeId không hợp lệ.");
            var (today, vnNowTime) = GetVnTime();

            // 1. KIỂM TRA NGÀY NGHỈ PHÉP ĐÃ DUYỆT TRƯỚC HẾT
            if (await IsDayApprovedForLeave(empId, today))
            {
                return CreateErrorResponse(400, "Bạn đã được duyệt đơn nghỉ phép hôm nay nên không cần checkin.");
            }

            // 2. KIỂM TRA WIFI TỪ DB
            var allowedWifis = await GetAllowedWifisFromDb();
            if (!allowedWifis.Any()) return CreateErrorResponse(500, "Lỗi cấu hình: Danh sách WiFi công ty chưa được thiết lập.");

            var isAllowed = allowedWifis.Any(w => string.Equals(w.WifiName, request.WifiName, StringComparison.OrdinalIgnoreCase) && string.Equals(w.Bssid, request.Bssid, StringComparison.OrdinalIgnoreCase));
            if (!isAllowed) return CreateErrorResponse(400, "Bạn không kết nối WiFi công ty. Vui lòng kết nối để checkin.");

            // 3. Kiểm tra check-in hôm nay
            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == empId && a.Date == today);

            if (existing != null)
            {
                if (existing.Status == AttendanceStatus.absent)
                    return CreateErrorResponse(400, "Hôm nay bạn đã bị đánh dấu vắng mặt. Nếu có lý do chính đáng, vui lòng liên hệ quản lý.");
                if (existing.CheckIn != null)
                    return CreateErrorResponse(400, "Bạn đã check-in hôm nay rồi!");
            }

            // 4. Xử lý Trạng thái
            var start = new TimeOnly(8, 0, 0);
            var lateThreshold = start.Add(TimeSpan.FromMinutes(5));

            AttendanceStatus status;
            string note;

            if (vnNowTime <= lateThreshold)
            {
                status = AttendanceStatus.present;
                note = $"Đúng giờ";
            }
            else
            {
                status = AttendanceStatus.late;
                var lateMinutes = (int)(vnNowTime - start).TotalMinutes;
                note = $"Đi muộn {lateMinutes} phút";
            }

            // Cập nhật hoặc thêm mới bản ghi
            if (existing == null)
            {
                var attendance = new Attendance { Id = Guid.NewGuid(), EmployeeId = empId, Date = today, CheckIn = vnNowTime, Status = status, Note = note };
                _context.Attendances.Add(attendance);
            }
            else
            {
                existing.CheckIn = vnNowTime;
                existing.Status = status;
                existing.Note = note;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"Check-in thành công. Trạng thái: {status}" });
        }


        // ------------------- CHECK-OUT -------------------
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            // 1. Khai báo và Kiểm tra cơ bản
            if (!Guid.TryParse(request.EmployeeId, out var empId)) return CreateErrorResponse(400, "EmployeeId không hợp lệ hoặc bị thiếu.");

            var (today, vnNowTime) = GetVnTime();
            var endWork = new TimeOnly(17, 0, 0); // Giờ chuẩn: 17:00:00

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == empId && a.Date == today);

            if (attendance == null || attendance.CheckIn == null) return CreateErrorResponse(400, "Bạn chưa check-in hôm nay!");
            if (attendance.CheckOut != null) return CreateErrorResponse(400, "Bạn đã check-out rồi!");

            // 2. Kiểm tra Wi-Fi TỪ DB
            var allowedWifis = await GetAllowedWifisFromDb();
            if (!allowedWifis.Any()) return CreateErrorResponse(500, "Lỗi cấu hình: Danh sách WiFi công ty chưa được thiết lập.");

            var isAllowed = allowedWifis.Any(w => string.Equals(w.WifiName, request.WifiName, StringComparison.OrdinalIgnoreCase) && string.Equals(w.Bssid, request.Bssid, StringComparison.OrdinalIgnoreCase));
            if (!isAllowed) return CreateErrorResponse(400, "Bạn không kết nối WiFi công ty. Vui lòng kết nối để checkout");


            // 3. XỬ LÝ VỀ SỚM (Chỉ ghi chú phạt) và OT
            TimeSpan overTimeDuration = vnNowTime - endWork;

            if (overTimeDuration.TotalMinutes > 5)
            {
                // Logic OT
            }
            else if (vnNowTime < endWork)
            {
                // XỬ LÝ VỀ SỚM
                var earlyLeaveDuration = endWork - vnNowTime;
                var minutesEarly = Math.Round(earlyLeaveDuration.TotalMinutes);

                // QUY TẮC PHẠT: Về sớm hơn 10 phút là PHẠT NẶNG (Trừ 0.5 công)
                if (earlyLeaveDuration.TotalMinutes > 10)
                {
                    attendance.Note = (attendance.Note ?? "") + $" | Về sớm {minutesEarly} phút. [PHẠT NẶNG - TRỪ 0.5 CÔNG]";
                }
            }


            // 4. Cập nhật CheckOut và Trạng thái cuối cùng
            attendance.CheckOut = vnNowTime;

            // Nếu status là 'present' (đi đúng giờ), chuyển thành 'completed').
            if (attendance.Status == AttendanceStatus.present)
            {
                attendance.Status = AttendanceStatus.completed;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Check-out thành công!" });
        }


        // ------------------- Tự động kiểm tra nhân viên có vắng mặt ko -------------------
        [HttpPost("mark-absent")]
        public async Task<IActionResult> MarkAbsent()
        {

            var (today, _) = GetVnTime();
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
            var employees = await _context.Employees.Where(e => e.Status == EmployeeStatus.active).ToListAsync();
            int markedAbsentCount = 0;

            foreach (var emp in employees)
            {
                // 1. KIỂM TRA HỢP ĐỒNG & NGHỈ PHÉP ĐÃ DUYỆT
                bool hasValidContract = await _context.Contracts.AnyAsync(c => c.EmployeeId == emp.Id && c.Status == ContractStatus.active);
                bool isApprovedLeaveDay = await IsDayApprovedForLeave(emp.Id, today);


                if (hasValidContract && !isApprovedLeaveDay) // CHỈ ĐÁNH DẤU NẾU LÀ NGÀY LÀM VIỆC BÌNH THƯỜNG
                {
                    // 2. Kiểm tra đã có bất kỳ bản ghi chấm công nào chưa (Không cần kiểm tra Leave ở đây nữa)
                    bool hasAttendance = await _context.Attendances.AnyAsync(a => a.EmployeeId == emp.Id && a.Date == today);

                    if (!hasAttendance)
                    {
                        // 3. Thêm bản ghi vắng mặt (absent)
                        _context.Attendances.Add(new Attendance
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = emp.Id,
                            Date = today,
                            Status = AttendanceStatus.absent,
                            Note = "Nghỉ không phép (Tự động đánh dấu cuối ngày)"
                        });
                        markedAbsentCount++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"Đã đánh dấu vắng mặt cho {markedAbsentCount} nhân viên chưa check-in và có hợp đồng còn hiệu lực."
            });
        }
        [HttpGet("status/{employeeId}")]
        public async Task<IActionResult> GetAttendanceStatus(Guid employeeId)
        {
            var (today, _) = GetVnTime();

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today);

            if (attendance == null)
            {
                return Ok(new
                {
                    Success = true,
                    Status = "NotCheckedIn",
                    Message = "Bạn chưa check-in hôm nay."
                });
            }

            if (attendance.Status == AttendanceStatus.leave)
            {
                return Ok(new
                {
                    Success = true,
                    Status = AttendanceStatus.leave.ToString(),
                    Message = "Bạn đã được duyệt nghỉ phép hôm nay nên không cần checkin."
                });
            }


            if (attendance.Status == AttendanceStatus.absent)
            {
                return Ok(new
                {
                    Success = true,
                    Status = AttendanceStatus.absent.ToString(),
                    Message = "Bạn đã bị đánh dấu vắng mặt hôm nay."
                });
            }

            if (attendance.CheckOut == null)
            {
                return Ok(new
                {
                    Success = true,
                    Status = "CheckedIn",
                    Message = "Bạn đã check-in nhưng chưa check-out."
                });
            }

            return Ok(new
            {
                Success = true,
                Status = attendance.Status.ToString(),
                Message = "Bạn đã hoàn tất check-in & check-out hôm nay."
            });
        }

        [HttpPost("auto-checkout")]
        public async Task<IActionResult> AutoCheckout()
        {
            var (today, _) = GetVnTime();
            var autoCheckoutTime = new TimeOnly(23, 59, 0);

            var pendingAttendances = await _context.Attendances
                .Where(a => a.Date == today && a.CheckOut == null)
                .ToListAsync();

            if (!pendingAttendances.Any())
                return Ok(new { Success = true, Message = "Không có nhân viên nào cần auto-checkout." });

            foreach (var attendance in pendingAttendances)
            {
                if (attendance.Status != AttendanceStatus.absent)
                {
                    attendance.CheckOut = autoCheckoutTime;

                    if (attendance.Status == AttendanceStatus.present)
                        attendance.Status = AttendanceStatus.completed;

                    attendance.Note = (attendance.Note ?? "") + " | Tự động check-out 23:59";
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"Đã auto check-out {pendingAttendances.Count} nhân viên." });
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