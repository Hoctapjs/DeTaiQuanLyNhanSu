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

        public AttendanceController(AppDbContext context)
        {
            _context = context;
        }

        // Phương thức hỗ trợ lấy ngày và giờ hiện tại theo múi giờ Việt Nam
        private (DateOnly Date, TimeOnly Time) GetVnTime()
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
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

        // ------------------- CHECK-IN -------------------
        [HttpPost("checkin")]
        public async Task<IActionResult> Checkin([FromBody] CheckinRequest request)
        {
            if (!Guid.TryParse(request.EmployeeId, out var empId))
                return CreateErrorResponse(400, "EmployeeId không hợp lệ.");

            var (today, vnNowTime) = GetVnTime();

            // Mạng WiFi hợp lệ
            var allowedWifi = new List<(string WifiName, string Bssid)>
            {
                ("P 304", "68:9e:29:c0:89:ef"),
                ("AndroidWifi", "00:13:10:85:fe:01"),
                ("IPOSBACKUP", "A8:2B:CD:50:12:AF")
            };
            var isAllowed = allowedWifi.Any(w =>
                string.Equals(w.WifiName, request.WifiName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.Bssid, request.Bssid, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
                return CreateErrorResponse(400, "Bạn không kết nối WiFi công ty. Vui lòng kết nối để checkin.");

            // Kiểm tra check-in hôm nay (Dùng DateOnly)
            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == empId && a.Date == today);

            if (existing != null)
            {
                // Nếu đã bị đánh dấu vắng (Dùng Enum)
                if (existing.Status == AttendanceStatus.absent)
                    return CreateErrorResponse(400, "Hôm nay bạn đã bị đánh dấu vắng mặt. Nếu có lý do chính đáng, vui lòng liên hệ quản lý.");

                // Nếu đã check-in (Dùng TimeOnly?)
                if (existing.CheckIn != null)
                    return CreateErrorResponse(400, "Bạn đã check-in hôm nay rồi!");
            }

            // Giờ chuẩn (Dùng TimeOnly)
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
                var attendance = new Attendance
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = empId,
                    Date = today,
                    CheckIn = vnNowTime,
                    Status = status,
                    Note = note
                };
                _context.Attendances.Add(attendance);
            }
            else
            {
                // Cập nhật trạng thái và giờ check-in cho bản ghi đã tồn tại (ví dụ: bị 'leave' tạm thời)
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
            if (!Guid.TryParse(request.EmployeeId, out var empId))
                return CreateErrorResponse(400, "EmployeeId không hợp lệ hoặc bị thiếu.");

            var (today, vnNowTime) = GetVnTime();
            var endWork = new TimeOnly(17, 0, 0); // Giờ chuẩn: 17:00:00

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == empId && a.Date == today);

            if (attendance == null || attendance.CheckIn == null)
                return CreateErrorResponse(400, "Bạn chưa check-in hôm nay!");

            if (attendance.CheckOut != null)
                return CreateErrorResponse(400, "Bạn đã check-out rồi!");

            // 2. Kiểm tra Wi-Fi
            var allowedWifi = new List<(string WifiName, string Bssid)>
            {
                ("P 304", "68:9e:29:c0:89:ef"), ("AndroidWifi", "00:13:10:85:fe:01"), ("IPOSBACKUP", "A8:2B:CD:50:12:AF")
            };
            var isAllowed = allowedWifi.Any(w =>
                string.Equals(w.WifiName, request.WifiName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.Bssid, request.Bssid, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
                return CreateErrorResponse(400, "Bạn không kết nối WiFi công ty. Vui lòng kết nối để checkout");


            // 3. XỬ LÝ VỀ SỚM và OT
            TimeSpan workDuration = vnNowTime - endWork;

            if (workDuration.TotalMinutes > 5)
            {
                // Logic OT
                attendance.Note = (attendance.Note ?? "") + " | Ghi nhận làm thêm giờ.";
            }
            else if (vnNowTime < endWork)
            {
                // XỬ LÝ VỀ SỚM
                TimeSpan earlyLeaveDuration = endWork - vnNowTime;
                var minutesEarly = Math.Round(earlyLeaveDuration.TotalMinutes);

                // QUY TẮC PHẠT MỚI: Về sớm hơn 10 phút
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
            // Tránh chạy vào cuối tuần
            if (GetVnTime().Date.DayOfWeek == DayOfWeek.Saturday || GetVnTime().Date.DayOfWeek == DayOfWeek.Sunday)
                return Ok(new { Success = true, Message = "Hệ thống không đánh dấu vắng mặt vào cuối tuần." });

            var (today, _) = GetVnTime();

            // LẤY TẤT CẢ NHÂN VIÊN ĐANG HOẠT ĐỘNG (SỬ DỤNG ENUM CHUẨN)
            var employees = await _context.Employees
                .Where(e => e.Status == EmployeeStatus.active) // <--- Đã chỉnh sửa
                .ToListAsync();

            int markedAbsentCount = 0;

            // 2. DUYỆT TỪNG NHÂN VIÊN
            foreach (var emp in employees)
            {
                // 3. KIỂM TRA HỢP ĐỒNG CÒN HIỆU LỰC
                bool hasValidContract = await _context.Contracts
                    .AnyAsync(c => c.EmployeeId == emp.Id && c.Status == ContractStatus.active);

                if (hasValidContract)
                {
                    // 4. Kiểm tra đã có bất kỳ bản ghi chấm công nào chưa (Dùng DateOnly)
                    bool hasAttendance = await _context.Attendances
                        .AnyAsync(a => a.EmployeeId == emp.Id && a.Date == today);

                    if (!hasAttendance)
                    {
                        // 5. Thêm bản ghi vắng mặt (absent)
                        _context.Attendances.Add(new Attendance
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = emp.Id,
                            Date = today,
                            Status = AttendanceStatus.absent,
                            Note = "Nghỉ không phép"
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