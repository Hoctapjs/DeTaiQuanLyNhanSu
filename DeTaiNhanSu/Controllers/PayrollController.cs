using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Notification;
using Google.Api.Gax;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Threading.Tasks;

namespace DeTaiNhanSu.Controllers
{
    // =================================================================
    // DTOs VÀ PHƯƠNG THỨC HỖ TRỢ (GIỮ NGUYÊN)
    // =================================================================
    public class FinalizeBatchPayrollRequest
    {
        public string Month { get; set; } = string.Empty;
    }

    public class PayrollCalculationResult
    {
        public decimal GrossSalary { get; set; }
        public decimal NetSalary { get; set; }

        public decimal LuongNgayCong { get; set; }
        public decimal TongPhuCap { get; set; }
        public decimal LuongOT { get; set; }
        public decimal TongThuong { get; set; }
        public decimal TongPhat { get; set; }
        public decimal TongBaoHiem { get; set; }
        public ContractType ContractType { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal InsuranceSalary { get; set; }
        public int SoCongPhanCong { get; set; }
        public decimal SoCongThucTe { get; set; }
        public int SoLanDiMuon { get; set; }
        public int SoLanVang { get; set; }
        public int SoLanVeSom { get; set; }
        public decimal LuongMotNgayCong { get; set; }
        public decimal LuongMotGio { get; set; }
        public decimal HeSoOT { get; set; }
        public decimal TongGioOTThucTe { get; set; }
        public decimal TongGioOTDaDangKy { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class PayrollController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        public PayrollController(AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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
        // Lấy danh sách tất cả các kỳ chạy lương (PayrollRun)
        // GET /api/Salary/payrollruns
        // =================================================================
        [HttpGet("payrollruns")]
        public async Task<IActionResult> GetPayrollRuns(
            [FromQuery] string? q,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sort = "Period desc") // Mặc định sắp xếp theo kỳ giảm dần
        {
            var initialQuery = _context.PayrollRuns
                .AsNoTracking()
                .AsQueryable();

            IQueryable<PayrollRun> query = initialQuery;

            // 1. Áp dụng Tìm kiếm (q)
            if (!string.IsNullOrEmpty(q))
            {
                // Lọc theo Period (YYYY-MM) hoặc Status
                string search = q.Trim();
                query = query.Where(pr => pr.Period.Contains(search));

                if (Enum.TryParse(search, true, out PayrollRunStatus statusEnum))
                {
                    query = query.Where(pr => pr.Status == statusEnum);
                }

                // Kiểm tra xem có kết quả nào sau khi lọc không
                if (await initialQuery.AnyAsync() && !await query.AnyAsync())
                {
                    return CreateErrorResponse(400, $"Không tìm thấy kết quả nào cho '{q}'. Vui lòng tìm kiếm theo: Kỳ lương (YYYY-MM) hoặc Trạng thái.");
                }
            }

            // 2. Tính tổng số lượng
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Xử lý trường hợp không có dữ liệu
            if (totalCount == 0)
            {
                var emptyMeta = new { current, pageSize, pages = 0, total = 0 };
                return Ok(new
                {
                    statusCode = 200,
                    message = "Không tìm thấy kỳ lương nào.",
                    data = new[] { new { meta = emptyMeta, result = new List<object>() } },
                    success = true
                });
            }

            List<dynamic> payrollRunList = new List<dynamic>();

            // 3. Sắp xếp và phân trang
            try
            {
                var tempPayrollRunList = await query
                    .OrderBy(sort)
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(pr => new
                    {
                        id = pr.Id,
                        period = pr.Period,
                        status = pr.Status.ToString(), // Chuyển Enum sang string

                    })
                    .ToListAsync();

                payrollRunList.AddRange(tempPayrollRunList.Cast<dynamic>());

            }
            catch (ParseException)
            {
                string supportedFields = "Hỗ trợ sắp xếp theo: Period, Status, CreatedAt. (Thêm ' asc' hoặc ' desc')";
                return CreateErrorResponse(400, $"Lỗi sắp xếp: Tên cột '{sort}' không hợp lệ. {supportedFields}");
            }
            catch (Exception)
            {
                throw;
            }

            // 4. Trả về Response
            return Ok(new
            {
                statusCode = 200,
                message = $"Tìm thấy {totalCount} kỳ lương.",
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
                result = payrollRunList
            }
        },
                success = true
            });
        }
        // Struct cấu hình lương/phạt
        private struct PayrollAttendanceConfig
        {
            public decimal FullWorkDayValue { get; set; }
            public decimal HalfWorkDayValue { get; set; }
            public decimal LatePenaltyHours { get; set; }
            public int EarlyLeavePenaltyMinutes { get; set; }
        }

        // Đọc cấu hình lương
        private async Task<PayrollAttendanceConfig> GetPayrollAttendanceConfig(CancellationToken ct = default)
        {
            var settings = await _context.GlobalSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith("FULL_") || s.Key.StartsWith("HALF_") || s.Key.StartsWith("LATE_") || s.Key.StartsWith("SEVERE_"))
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

            decimal ParseDecimal(string key, decimal def) =>
                settings.TryGetValue(key, out var val) && decimal.TryParse(val, CultureInfo.InvariantCulture, out var res) ? res : def;
            int ParseInt(string key, int def) =>
                settings.TryGetValue(key, out var val) && int.TryParse(val, out var res) ? res : def;

            return new PayrollAttendanceConfig
            {
                FullWorkDayValue = ParseDecimal("FULL_WORK_DAY_VALUE", 1.0m),
                HalfWorkDayValue = ParseDecimal("HALF_WORK_DAY_VALUE", 0.5m),
                LatePenaltyHours = ParseDecimal("LATE_PENALTY_HOURS", 1.0m),
                EarlyLeavePenaltyMinutes = ParseInt("SEVERE_EARLY_LEAVE_MINUTES", 10)
            };
        }

        // =================================================================
        // PHƯƠNG THỨC HỖ TRỢ ĐỌC GLOBAL SETTINGS
        // =================================================================
        private async Task<decimal> GetGlobalSettingValue(string key, decimal defaultValue)
        {
            var setting = await _context.GlobalSettings.AsNoTracking()
                                       .FirstOrDefaultAsync(s => s.Key == key);

            if (setting != null && decimal.TryParse(setting.Value, CultureInfo.InvariantCulture, out decimal rate))
            {
                return rate;
            }
            return defaultValue;
        }

        private async Task<string> GetGlobalSettingString(string key, string defaultValue)
        {
            var setting = await _context.GlobalSettings.AsNoTracking()
                                       .FirstOrDefaultAsync(s => s.Key == key);

            return setting?.Value ?? defaultValue;
        }
        // =================================================================
        // PHƯƠNG THỨC TÍNH TOÁN LƯƠNG CHUNG (CORE LOGIC) - ĐÃ CẬP NHẬT
        // =================================================================
        private async Task<PayrollCalculationResult?> CalculateEmployeePayroll(Guid employeeId, DateTime startDate, DateTime endDate)
        {
            var startOnly = DateOnly.FromDateTime(startDate);
            var endOnly = DateOnly.FromDateTime(endDate);

            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.Status == ContractStatus.active);
            if (contract == null) return null;

            // Lấy dữ liệu
            var attendanceList = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.Date >= startOnly && a.Date <= endOnly).ToListAsync();
            var otList = await _context.Overtimes
                .Where(o => o.EmployeeId == employeeId && o.Date >= startOnly && o.Date <= endOnly).ToListAsync();
            var rewards = await _context.RewardPenalties.Include(r => r.Type)
                .Where(r => r.EmployeeId == employeeId && r.DecidedAt >= startOnly && r.DecidedAt <= endOnly).ToListAsync();

            // [MỚI] Lấy WorkSchedule để biết giờ ca làm việc thực tế
            var workSchedules = await _context.WorkSchedules.AsNoTracking()
                .Include(ws => ws.ShiftTemplate)
                .Where(ws => ws.EmployeeId == employeeId && ws.Date >= startOnly && ws.Date <= endOnly)
                .ToDictionaryAsync(ws => ws.Date, ws => ws);

            // [MỚI] Lấy cấu hình lương/phạt
            var payrollConfig = await GetPayrollAttendanceConfig();

            // Tính số công phân công (Trừ ngày nghỉ cuối tuần)
            string weekendDaysString = await GetGlobalSettingString("WEEKEND_DAYS", "Sunday");
            var configuredWeekendDays = weekendDaysString.Split(',')
                .Select(d => d.Trim())
                .Where(d => Enum.TryParse(d, true, out DayOfWeek _))
                .Select(d => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), d, true))
                .ToList();

            int soCongPhanCong = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(i => startDate.AddDays(i))
                .Count(d => !configuredWeekendDays.Contains(d.DayOfWeek));

            decimal tongCongThucTe = 0;
            int soLanDiMuon = 0;
            int soLanVang = 0;
            int soLanVeSom = 0;
            decimal tongGioOTThucTe = 0;
            decimal tongGioOTDaDangKy = otList.Sum(o => o.Hours);

            // Giờ chuẩn để tính lương theo giờ (Logic quy đổi lương vẫn dựa trên 8h/ngày công chuẩn)
            decimal luongMotNgayCong = soCongPhanCong == 0 ? 0 : contract.BasicSalary / soCongPhanCong;
            decimal luongMotGio = luongMotNgayCong / 8;

            foreach (var att in attendanceList)
            {
                decimal congNgay = payrollConfig.FullWorkDayValue; // Mặc định 1.0 công

                // Kiểm tra xem ngày này có Lịch làm việc không
                if (!workSchedules.TryGetValue(att.Date, out var schedule) || schedule.ShiftTemplate == null)
                {
                    // Không có lịch => Không tính công (trừ khi là Leave)
                    if (att.Status == AttendanceStatus.leave)
                    {
                        congNgay = payrollConfig.FullWorkDayValue;
                    }
                    else if (att.Status == AttendanceStatus.absent)
                    {
                        // Bị đánh vắng nhưng ko có lịch (lẽ ra ko nên xảy ra nếu logic MarkAbsent đúng)
                        congNgay = 0m;
                        soLanVang++;
                    }
                    else
                    {
                        congNgay = 0m; // Các trường hợp khác ko có lịch thì ko tính công
                    }
                }
                else
                {
                    // CÓ LỊCH LÀM VIỆC -> Dùng giờ Start/End của ca
                    TimeOnly shiftStart = schedule.ShiftTemplate.StartTime;
                    TimeOnly shiftEnd = schedule.ShiftTemplate.EndTime;

                    // 1. Xử lý VẮNG / NGHỈ
                    if (att.Status == AttendanceStatus.absent)
                    {
                        soLanVang++;
                        congNgay = 0m;
                    }
                    else if (att.Status == AttendanceStatus.leave)
                    {
                        congNgay = payrollConfig.FullWorkDayValue;
                    }

                    // 2. Xử lý ĐI MUỘN (Dựa trên Shift Start & Config)
                    else if (att.Status == AttendanceStatus.late)
                    {
                        soLanDiMuon++;
                        if (att.CheckIn.HasValue)
                        {
                            TimeSpan late = att.CheckIn.Value > shiftStart ? att.CheckIn.Value - shiftStart : TimeSpan.Zero;
                            if (late.TotalHours > (double)payrollConfig.LatePenaltyHours)
                            {
                                congNgay = payrollConfig.HalfWorkDayValue; // Trừ còn 0.5
                            }
                        }
                    }

                    // 3. Xử lý VỀ SỚM (Dựa trên Shift End & Config)
                    if (att.CheckOut.HasValue && att.CheckOut.Value < shiftEnd)
                    {
                        TimeSpan early = shiftEnd - att.CheckOut.Value;
                        if (early.TotalMinutes > payrollConfig.EarlyLeavePenaltyMinutes)
                        {
                            soLanVeSom++;
                            if (congNgay == payrollConfig.FullWorkDayValue)
                            {
                                congNgay = payrollConfig.HalfWorkDayValue; // Trừ còn 0.5
                            }
                        }
                    }

                    // 4. Tính OT Thực tế (Dựa trên Shift End)
                    var otRecord = otList.FirstOrDefault(o => o.Date == att.Date);
                    if (otRecord != null && att.CheckOut.HasValue && att.CheckOut.Value > shiftEnd)
                    {
                        decimal calculatedOt = (decimal)(att.CheckOut.Value - shiftEnd).TotalHours;
                        decimal finalOtHours = Math.Min(otRecord.Hours, calculatedOt);

                        tongGioOTThucTe += finalOtHours;
                    }
                }

                tongCongThucTe += congNgay;
            }

            // Các phần tính toán tổng hợp giữ nguyên
            decimal tongThuong = rewards.Where(r => r.Type.Type == RewardPenaltyKind.reward).Sum(r => r.AmountOverride.GetValueOrDefault(r.Type.DefaultAmount ?? 0));
            decimal tongPhat = rewards.Where(r => r.Type.Type == RewardPenaltyKind.penalty).Sum(r => r.AmountOverride.GetValueOrDefault(r.Type.DefaultAmount ?? 0));
            var salaryPreview = await _context.Salaries.Include(s => s.Items).OrderByDescending(s => s.PayrollRunId).FirstOrDefaultAsync(s => s.EmployeeId == employeeId);
            decimal tongPhuCap = salaryPreview?.Items?.Where(i => i.Type == SalaryItemType.allowance).Sum(i => i.Amount) ?? 0;

            decimal heSoOT = otList.FirstOrDefault()?.Rate ?? await GetGlobalSettingValue("DEFAULT_OT_RATE", 1.5m);
            decimal luongOTThucTe = tongGioOTThucTe * luongMotGio * heSoOT;

            decimal bhxhRate = await GetGlobalSettingValue("EMP_BHXH_RATE", 0.08m);
            decimal bhytRate = await GetGlobalSettingValue("EMP_BHYT_RATE", 0.015m);
            decimal bhtnRate = await GetGlobalSettingValue("EMP_BHTN_RATE", 0.01m);

            decimal insuranceSalary = contract.InsuranceSalary.HasValue && contract.InsuranceSalary.Value > 0 ? contract.InsuranceSalary.Value : contract.BasicSalary;
            decimal tongBaoHiem = (bhxhRate + bhytRate + bhtnRate) * insuranceSalary;

            decimal luongNgayCongThucTe = luongMotNgayCong * tongCongThucTe;
            decimal luongThucNhan = luongNgayCongThucTe + tongPhuCap + tongThuong + luongOTThucTe - tongPhat - tongBaoHiem;
            decimal luongGross = luongNgayCongThucTe + tongPhuCap + luongOTThucTe + tongThuong - tongPhat;

            return new PayrollCalculationResult
            {
                GrossSalary = luongGross,
                NetSalary = luongThucNhan,
                LuongNgayCong = luongNgayCongThucTe,
                TongPhuCap = tongPhuCap,
                LuongOT = luongOTThucTe,
                TongThuong = tongThuong,
                TongPhat = tongPhat,
                TongBaoHiem = tongBaoHiem,
                ContractType = contract.Type,
                BasicSalary = contract.BasicSalary,
                InsuranceSalary = insuranceSalary,
                SoCongPhanCong = soCongPhanCong,
                SoCongThucTe = tongCongThucTe,
                SoLanDiMuon = soLanDiMuon,
                SoLanVang = soLanVang,
                SoLanVeSom = soLanVeSom,
                LuongMotNgayCong = luongMotNgayCong,
                LuongMotGio = luongMotGio,
                HeSoOT = heSoOT,
                TongGioOTThucTe = tongGioOTThucTe,
                TongGioOTDaDangKy = tongGioOTDaDangKy
            };
        }

        // =================================================================
        // API GET PERFORMANCE (Tính lương tổng hợp)
        // =================================================================
        [HttpGet("performance/{employeeId}")]
        public async Task<IActionResult> GetPerformance(Guid employeeId, [FromQuery] string? month)
        {
            if (!await _context.Employees.AnyAsync(e => e.Id == employeeId)) return CreateErrorResponse(404, "Không tìm thấy nhân viên với ID được cung cấp.");
            if (string.IsNullOrEmpty(month)) month = DateTime.Now.ToString("yyyy-MM");

            if (!DateTime.TryParseExact($"{month}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startDate))
                return CreateErrorResponse(400, "Định dạng tháng không hợp lệ.");

            var endDate = startDate.AddMonths(1).AddDays(-1);

            var calculatedResult = await CalculateEmployeePayroll(employeeId, startDate, endDate);

            if (calculatedResult == null) return CreateErrorResponse(404, "Không tìm thấy hợp đồng đang hoạt động cho nhân viên này.");

            // Lấy Tỷ lệ BHXH, BHYT, BHTN từ GlobalSettings để trả về cho UI
            decimal bhxh = await GetGlobalSettingValue("EMP_BHXH_RATE", 0.08m);
            decimal bhyt = await GetGlobalSettingValue("EMP_BHYT_RATE", 0.015m);
            decimal bhtn = await GetGlobalSettingValue("EMP_BHTN_RATE", 0.01m);


            // CHỈNH SỬA: Chuyển Enum ContractType sang string khi trả về JSON
            var result = new
            {
                month = month.Split('-').Last(),
                thongTinNhanVien = new
                {
                    employeeId = employeeId,
                    contractType = calculatedResult.ContractType.ToString(),
                    basicSalary = Math.Round(calculatedResult.BasicSalary, 3),
                    insuranceSalary = Math.Round(calculatedResult.InsuranceSalary, 3)
                },
                chamCong = new
                {
                    soCongPhanCong = calculatedResult.SoCongPhanCong,
                    soCongThucTe = Math.Round(calculatedResult.SoCongThucTe, 3),
                    soLanDiMuon = calculatedResult.SoLanDiMuon,
                    soLanVang = calculatedResult.SoLanVang,
                    soLanVeSom = calculatedResult.SoLanVeSom,
                },
                luong = new
                {
                    tongPhuCap = Math.Round(calculatedResult.TongPhuCap, 3),
                    tongThuong = Math.Round(calculatedResult.TongThuong, 3),
                    tongPhat = Math.Round(calculatedResult.TongPhat, 3),
                    luongMotNgayCong = Math.Round(calculatedResult.LuongMotNgayCong, 3),
                    luongMotGio = Math.Round(calculatedResult.LuongMotGio, 3),
                    soGioOT = Math.Round(calculatedResult.TongGioOTDaDangKy, 3),
                    heSoOT = Math.Round(calculatedResult.HeSoOT, 3),
                    tongGioOTThucTe = Math.Round(calculatedResult.TongGioOTThucTe, 3),
                    luongOT = Math.Round(calculatedResult.LuongOT, 3),
                    bhxh = Math.Round(bhxh, 3),
                    bhyt = Math.Round(bhyt, 3),
                    bhtn = Math.Round(bhtn, 3),
                    baoHiem = Math.Round(calculatedResult.TongBaoHiem, 3),
                    luongThucNhan = Math.Round(calculatedResult.NetSalary, 3)
                }
            };

            return Ok(new
            {
                statusCode = 200,
                message = $"Lương tháng {month.Split('-').Last()}",
                data = new[] { new { result = new[] { result } } },
                success = true
            });
        }
        // =================================================================
        // API GET DAILY DETAILS (Chi tiết chấm công & Lương theo ngày)
        // =================================================================
        [HttpGet("daily/{employeeId}")]
        public async Task<IActionResult> GetDailyDetails(Guid employeeId, [FromQuery] string? month)
        {
            // 1. VALIDATION CƠ BẢN
            if (!await _context.Employees.AnyAsync(e => e.Id == employeeId))
                return CreateErrorResponse(404, "Không tìm thấy nhân viên với ID được cung cấp.");

            if (string.IsNullOrEmpty(month)) month = DateTime.Now.ToString("yyyy-MM");

            if (!DateTime.TryParseExact($"{month}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startDate))
                return CreateErrorResponse(400, "Định dạng tháng không hợp lệ (YYYY-MM).");

            var endDate = startDate.AddMonths(1).AddDays(-1);
            var startOnly = DateOnly.FromDateTime(startDate);
            var endOnly = DateOnly.FromDateTime(endDate);

            // 2. LẤY HỢP ĐỒNG (Bắt buộc phải có hợp đồng active để tính lương cơ bản)
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.Status == ContractStatus.active);
            if (contract == null)
                return CreateErrorResponse(404, "Không tìm thấy hợp đồng đang hoạt động cho nhân viên này.");

            // 3. LẤY CẤU HÌNH HỆ THỐNG (GlobalSettings)
            // Lưu ý: Cần có phương thức GetPayrollAttendanceConfig() và GetGlobalSettingString() trong class này
            var payrollConfig = await GetPayrollAttendanceConfig();
            string weekendDaysString = await GetGlobalSettingString("WEEKEND_DAYS", "Sunday");

            // Parse ngày nghỉ cuối tuần
            var configuredWeekendDays = weekendDaysString.Split(',')
                .Select(d => d.Trim())
                .Where(d => Enum.TryParse(d, true, out DayOfWeek _))
                .Select(d => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), d, true))
                .ToList();

            // 4. TÍNH SỐ CÔNG CHUẨN (Phân công)
            // Trừ đi các ngày nghỉ cuối tuần theo cấu hình
            int soCongPhanCong = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(i => startDate.AddDays(i))
                .Count(d => !configuredWeekendDays.Contains(d.DayOfWeek));

            // 5. TRUY VẤN DỮ LIỆU LIÊN QUAN (Attendance, OT, Rewards, Salary, WorkSchedule)
            var attendanceList = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.Date >= startOnly && a.Date <= endOnly)
                .ToListAsync();

            var otList = await _context.Overtimes
                .AsNoTracking()
                .Where(o => o.EmployeeId == employeeId && o.Date >= startOnly && o.Date <= endOnly)
                .ToListAsync();

            var rewards = await _context.RewardPenalties
                .AsNoTracking()
                .Include(r => r.Type)
                .Where(r => r.EmployeeId == employeeId && r.DecidedAt >= startOnly && r.DecidedAt <= endOnly)
                .ToListAsync();

            // Lấy phụ cấp từ bảng lương gần nhất (hoặc logic khác tùy nghiệp vụ)
            var salary = await _context.Salaries
                .Include(s => s.Items)
                .OrderByDescending(s => s.PayrollRunId)
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId);

            decimal tongPhuCap = salary?.Items?.Where(i => i.Type == SalaryItemType.allowance).Sum(i => i.Amount) ?? 0;

            // Lấy Lịch làm việc (Kèm mẫu ca ShiftTemplate) để biết giờ Start/End thực tế
            var workSchedules = await _context.WorkSchedules
                .AsNoTracking()
                .Include(ws => ws.ShiftTemplate)
                .Where(ws => ws.EmployeeId == employeeId && ws.Date >= startOnly && ws.Date <= endOnly)
                .ToDictionaryAsync(ws => ws.Date, ws => ws);

            // 6. TÍNH TOÁN CÁC HỆ SỐ CƠ BẢN
            decimal phuCapTheoNgay = soCongPhanCong == 0 ? 0 : tongPhuCap / soCongPhanCong;
            decimal luongMotNgayCong = soCongPhanCong == 0 ? 0 : contract.BasicSalary / soCongPhanCong;
            decimal luongMotGio = luongMotNgayCong / 8; // Giả định chuẩn 1 công là 8 tiếng để tính đơn giá giờ

            var daysInMonth = Enumerable.Range(0, (endDate - startDate).Days + 1).Select(i => startDate.AddDays(i)).ToList();
            var result = new List<object>();

            // 7. VÒNG LẶP TÍNH TOÁN TỪNG NGÀY
            foreach (var day in daysInMonth)
            {
                var currentDayOnly = DateOnly.FromDateTime(day);

                // Lấy dữ liệu của ngày hiện tại
                var att = attendanceList.FirstOrDefault(a => a.Date == currentDayOnly);
                var otRecord = otList.FirstOrDefault(o => o.Date == currentDayOnly);
                workSchedules.TryGetValue(currentDayOnly, out var schedule);

                // Xác định giờ ca làm việc (Nếu không có lịch, dùng giờ mặc định để hiển thị hoặc tính toán fallback)
                TimeOnly shiftStart = schedule?.ShiftTemplate?.StartTime ?? new TimeOnly(8, 0, 0);
                TimeOnly shiftEnd = schedule?.ShiftTemplate?.EndTime ?? new TimeOnly(17, 0, 0);
                string shiftCode = schedule?.ShiftTemplate?.Code ?? "N/A";

                // Khởi tạo biến cho ngày
                string? status = att?.Status.ToString();
                decimal soCong = 0;
                decimal gioOtDuocDuyet = otRecord?.Hours ?? 0;
                decimal gioOtThucTe = 0;
                decimal luongOtThucTe = 0;
                string ghiChuPhat = "";
                string noteDisplay = att?.Note ?? "";

                // --- LOGIC TÍNH CÔNG ---
                // Trường hợp 1: Có dữ liệu chấm công VÀ Có lịch làm việc
                if (att != null && schedule?.ShiftTemplate != null)
                {
                    soCong = payrollConfig.FullWorkDayValue; // Mặc định 1.0 công

                    // A. Kiểm tra trạng thái đặc biệt
                    if (att.Status == AttendanceStatus.absent)
                    {
                        soCong = 0m;
                    }
                    else if (att.Status == AttendanceStatus.leave)
                    {
                        soCong = payrollConfig.FullWorkDayValue; // Nghỉ phép vẫn tính công (hoặc xử lý riêng tùy loại phép)
                    }
                    // B. Kiểm tra Đi muộn (Dựa trên Shift Start & Config)
                    else if (att.Status == AttendanceStatus.late && att.CheckIn.HasValue)
                    {
                        // Tính thời gian trễ thực tế
                        TimeSpan late = att.CheckIn.Value > shiftStart ? att.CheckIn.Value - shiftStart : TimeSpan.Zero;

                        // Nếu trễ quá ngưỡng quy định (VD: > 1 giờ) -> Trừ công
                        if (late.TotalHours > (double)payrollConfig.LatePenaltyHours)
                        {
                            soCong = payrollConfig.HalfWorkDayValue; // Còn 0.5 công
                            ghiChuPhat += $" [TRỄ >{payrollConfig.LatePenaltyHours}h (-{(payrollConfig.FullWorkDayValue - payrollConfig.HalfWorkDayValue)} công)]";
                        }
                    }

                    // C. Kiểm tra Về sớm (Dựa trên Shift End & Config)
                    if (att.CheckOut.HasValue && att.CheckOut.Value < shiftEnd)
                    {
                        TimeSpan early = shiftEnd - att.CheckOut.Value;

                        // Nếu về sớm quá ngưỡng quy định (VD: > 10 phút)
                        if (early.TotalMinutes > payrollConfig.EarlyLeavePenaltyMinutes)
                        {
                            // Nếu chưa bị trừ công (vẫn đang là Full) -> Trừ
                            if (soCong == payrollConfig.FullWorkDayValue)
                            {
                                soCong = payrollConfig.HalfWorkDayValue;
                                ghiChuPhat += $" [SỚM >{payrollConfig.EarlyLeavePenaltyMinutes}p (-{(payrollConfig.FullWorkDayValue - payrollConfig.HalfWorkDayValue)} công)]";
                            }
                            // Nếu đã bị trừ do đi muộn rồi (đang là Half) -> Chỉ ghi chú thêm (hoặc trừ tiếp về 0 tùy chính sách cty)
                            else if (soCong == payrollConfig.HalfWorkDayValue)
                            {
                                ghiChuPhat += $" + [SỚM >{payrollConfig.EarlyLeavePenaltyMinutes}p]";
                            }
                        }
                    }

                    // D. Tính OT Thực tế (Chỉ tính khi CheckOut sau giờ kết thúc ca)
                    if (otRecord != null && att.CheckOut.HasValue && att.CheckOut.Value > shiftEnd)
                    {
                        decimal calculatedOt = (decimal)(att.CheckOut.Value - shiftEnd).TotalHours;
                        // OT được tính = Min(OT Đăng ký, OT Thực tế chấm công)
                        decimal finalOtHours = Math.Min(otRecord.Hours, calculatedOt);

                        gioOtThucTe = finalOtHours > 0 ? finalOtHours : 0;
                        // Tiền OT = Giờ * Hệ số * Đơn giá giờ
                        luongOtThucTe = gioOtThucTe * (otRecord.Rate) * luongMotGio;
                    }
                }
                // Trường hợp 2: Có chấm công nhưng KHÔNG có lịch (VD: Làm chủ nhật, hoặc Nghỉ phép/Vắng mặt không gắn lịch)
                else if (att != null)
                {
                    if (att.Status == AttendanceStatus.leave)
                    {
                        soCong = payrollConfig.FullWorkDayValue;
                        status = "Leave";
                    }
                    else if (att.Status == AttendanceStatus.absent)
                    {
                        soCong = 0m;
                        status = "Absent";
                    }
                    else if (schedule == null)
                    {
                        // Có check-in vào ngày không có lịch -> Có thể là OT ngày nghỉ
                        status = "Extra/NoSchedule";
                        soCong = 0m; // Mặc định không tính công ngày thường, chỉ tính OT nếu có

                        if (otRecord != null && att.CheckIn.HasValue && att.CheckOut.HasValue)
                        {
                            // Logic tính OT ngày nghỉ (đơn giản hóa: tính full giờ làm)
                            decimal hoursWorked = (decimal)(att.CheckOut.Value - att.CheckIn.Value).TotalHours;
                            gioOtThucTe = Math.Min(otRecord.Hours, hoursWorked);
                            luongOtThucTe = gioOtThucTe * otRecord.Rate * luongMotGio;
                        }
                    }
                }
                else
                {
                    // Không có dữ liệu chấm công
                    status = configuredWeekendDays.Contains(day.DayOfWeek) ? "Weekend" : "N/A";
                }

                // 8. TỔNG HỢP TIỀN TRONG NGÀY
                decimal luongNgay = luongMotNgayCong * soCong;

                // Cộng dồn thưởng/phạt quyết định trong ngày này
                decimal phatTrongNgay = rewards.Where(r => r.Type.Type == RewardPenaltyKind.penalty && r.DecidedAt == currentDayOnly)
                                               .Sum(r => r.AmountOverride.GetValueOrDefault(r.Type.DefaultAmount ?? 0));

                decimal thuongTrongNgay = rewards.Where(r => r.Type.Type == RewardPenaltyKind.reward && r.DecidedAt == currentDayOnly)
                                                 .Sum(r => r.AmountOverride.GetValueOrDefault(r.Type.DefaultAmount ?? 0));

                decimal tongLuongNgay = luongNgay + phuCapTheoNgay + thuongTrongNgay + luongOtThucTe - phatTrongNgay;

                // Ghép ghi chú
                string finalNote = noteDisplay;
                if (!string.IsNullOrEmpty(ghiChuPhat)) finalNote += " | " + ghiChuPhat;
                if (!string.IsNullOrEmpty(shiftCode) && shiftCode != "N/A") finalNote = $"[{shiftCode}] " + finalNote;

                // 9. THÊM VÀO KẾT QUẢ
                result.Add(new
                {
                    ngay = day.ToString("yyyy-MM-dd"),
                    thu = day.DayOfWeek.ToString(), // Thêm thứ để dễ nhìn (Monday, Tuesday...)
                    trangThai = status,
                    soCong = Math.Round(soCong, 2),
                    phuCap = Math.Round(phuCapTheoNgay, 0),
                    thuong = Math.Round(thuongTrongNgay, 0),
                    gioOtDuocDuyet = Math.Round(gioOtDuocDuyet, 2),
                    gioOtThucTe = Math.Round(gioOtThucTe, 2),
                    luongOt = Math.Round(luongOtThucTe, 0),
                    phat = Math.Round(phatTrongNgay, 0),
                    luongNgay = Math.Round(tongLuongNgay, 0),
                    ghiChu = finalNote
                });
            }

            return Ok(new
            {
                statusCode = 200,
                message = $"Lương chi tiết tháng {month.Split('-').Last()}",
                data = new[] { new { result = result } },
                success = true
            });
        }

        // =================================================================
        // API GET PERFORMANCE BATCH (Tính lương hàng loạt theo tháng)
        // ĐÃ TÍCH HỢP TÌM KIẾM, SẮP XẾP, PHÂN TRANG (Đã kiểm tra lại)
        // =================================================================
        [HttpGet("performance-batch")]
        public async Task<IActionResult> GetPerformanceBatch(
           [FromQuery] string? month,
           [FromQuery] string? q,             // Tham số tìm kiếm
           [FromQuery] int current = 1,     // Trang hiện tại
           [FromQuery] int pageSize = 20,   // Số lượng mỗi trang
           [FromQuery] string? sort = null,     // Tham số sắp xếp
           CancellationToken ct = default)
        {
            try
            {
                // 1. Validate tháng và Phân trang
                if (string.IsNullOrEmpty(month)) month = DateTime.Now.ToString("yyyy-MM");

                if (!DateTime.TryParseExact($"{month}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startDate))
                    return CreateErrorResponse(400, "Định dạng tháng không hợp lệ. Vui lòng sử dụng YYYY-MM.");

                var endDate = startDate.AddMonths(1).AddDays(-1);

                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                string monthOnly = month.Split('-').Last();
                string baseMessage = $"Bảng lương performance tháng {monthOnly}";

                // 2. Xây dựng Query IQueryable cho Nhân viên có Hợp đồng Active
                var activeContractEmployeeIds = _context.Contracts
                    .Where(c => c.Status == ContractStatus.active)
                    .Select(c => c.EmployeeId);

                var query = _context.Employees
                    .Where(e => e.Status == EmployeeStatus.active && activeContractEmployeeIds.Contains(e.Id))
                    .AsNoTracking();

                // Lấy tổng số lượng nhân viên có hợp đồng hợp lệ (Total Ban Đầu)
                var preFilterTotal = await query.CountAsync(ct);

                if (preFilterTotal == 0)
                {
                    var emptyMeta = new { current, pageSize, pages = 0, total = 0 };
                    return Ok(new
                    {
                        statusCode = 200,
                        message = baseMessage + " - Không tìm thấy nhân viên nào có hợp đồng đang hoạt động.",
                        data = new { meta = emptyMeta, result = new List<object>() },
                        success = true
                    });
                }

                // 3. Áp dụng Tìm kiếm (q)
                if (!string.IsNullOrWhiteSpace(q))
                {
                    string search = q.Trim();
                    // Lọc theo FullName hoặc Code
                    query = query.Where(e => e.FullName.Contains(search) || e.Code.Contains(search));
                }

                // 4. Áp dụng Sắp xếp (sort) và kiểm tra lỗi
                // Chỉ chấp nhận: "fullName", "-fullName", "code", "-code"
                string effectiveSort = sort?.Trim() ?? "fullName";
                string sortError = null;

                switch (effectiveSort.TrimStart('-', '+').ToLower())
                {
                    case "fullname":
                        query = effectiveSort.StartsWith('-')
                            ? query.OrderByDescending(e => e.FullName).ThenBy(e => e.Id)
                            : query.OrderBy(e => e.FullName).ThenBy(e => e.Id);
                        break;
                    case "code":
                        query = effectiveSort.StartsWith('-')
                           ? query.OrderByDescending(e => e.Code).ThenBy(e => e.Id)
                           : query.OrderBy(e => e.Code).ThenBy(e => e.Id);
                        break;
                    default:
                        // Lỗi: Tên cột sắp xếp không hợp lệ
                        sortError = $"Không thể sắp xếp theo '{sort}'. Vui lòng sử dụng: 'fullName' (tăng dần) hoặc '-fullName' (giảm dần).";
                        break;
                }

                if (sortError != null)
                {
                    return CreateErrorResponse(StatusCodes.Status400BadRequest, sortError);
                }

                // 5. Lấy tổng số lượng SAU KHI lọc (q)
                var total = await query.CountAsync(ct);

                // Xử lý trường hợp không tìm thấy sau khi Tìm kiếm (q)
                if (total == 0)
                {
                    var emptyMeta = new { current, pageSize, pages = 0, total = 0 };
                    return Ok(new
                    {
                        statusCode = 200,
                        message = baseMessage + $" - Không tìm thấy nhân viên nào phù hợp với từ khóa '{q?.Trim() ?? "..."}'",
                        data = new { meta = emptyMeta, result = new List<object>() },
                        success = true
                    });
                }

                // 6. Áp dụng Phân trang (Skip/Take)
                var employeesToRun = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new { e.Id, e.FullName })
                    .ToListAsync(ct);

                // 7. Lấy Global Settings (BHXH, BHYT, BHTN) MỘT LẦN
                decimal bhxh = await GetGlobalSettingValue("EMP_BHXH_RATE", 0.08m);
                decimal bhyt = await GetGlobalSettingValue("EMP_BHYT_RATE", 0.015m);
                decimal bhtn = await GetGlobalSettingValue("EMP_BHTN_RATE", 0.01m);

                // 8. Tính toán và Định dạng kết quả
                var allResults = new List<object>();
                foreach (var employee in employeesToRun)
                {
                    var calculatedResult = await CalculateEmployeePayroll(employee.Id, startDate, endDate);

                    // Giữ nguyên logic bỏ qua nếu không có hợp đồng active
                    if (calculatedResult == null) continue;

                    var result = new
                    {
                        month = monthOnly,
                        thongTinNhanVien = new
                        {
                            employeeId = employee.Id,
                            fullName = employee.FullName,
                            contractType = calculatedResult.ContractType.ToString(),
                            basicSalary = Math.Round(calculatedResult.BasicSalary, 3),
                            insuranceSalary = Math.Round(calculatedResult.InsuranceSalary, 3)
                        },
                        chamCong = new
                        {
                            soCongPhanCong = calculatedResult.SoCongPhanCong,
                            soCongThucTe = Math.Round(calculatedResult.SoCongThucTe, 3),
                            soLanDiMuon = calculatedResult.SoLanDiMuon,
                            soLanVang = calculatedResult.SoLanVang,
                            soLanVeSom = calculatedResult.SoLanVeSom,
                        },
                        luong = new
                        {
                            tongPhuCap = Math.Round(calculatedResult.TongPhuCap, 3),
                            tongThuong = Math.Round(calculatedResult.TongThuong, 3),
                            tongPhat = Math.Round(calculatedResult.TongPhat, 3),
                            luongMotNgayCong = Math.Round(calculatedResult.LuongMotNgayCong, 3),
                            luongMotGio = Math.Round(calculatedResult.LuongMotGio, 3),
                            soGioOT = Math.Round(calculatedResult.TongGioOTDaDangKy, 3),
                            heSoOT = Math.Round(calculatedResult.HeSoOT, 3),
                            tongGioOTThucTe = Math.Round(calculatedResult.TongGioOTThucTe, 3),
                            luongOT = Math.Round(calculatedResult.LuongOT, 3),
                            bhxh = Math.Round(bhxh, 3),
                            bhyt = Math.Round(bhyt, 3),
                            bhtn = Math.Round(bhtn, 3),
                            baoHiem = Math.Round(calculatedResult.TongBaoHiem, 3),
                            luongThucNhan = Math.Round(calculatedResult.NetSalary, 3)
                        }
                    };

                    allResults.Add(result);
                }

                // 9. Tạo đối tượng meta và trả về kết quả
                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                return Ok(new
                {
                    statusCode = 200,
                    message = baseMessage + $" - Đã tính lương cho {allResults.Count} nhân viên (Tổng: {total}).",
                    data = new { meta, result = allResults },
                    success = true
                });
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(500, $"Lỗi máy chủ không xác định khi lấy bảng lương: {ex.Message}");
            }
        }
        // =================================================================
        // API CHỐT LƯƠNG HÀNG LOẠT (FINALIZED)
        // =================================================================
        [HttpPost("finalize-batch")]
        public async Task<IActionResult> FinalizeBatchPayroll([FromBody] FinalizeBatchPayrollRequest request)
        {
            // ... (Phần này sử dụng CalculateEmployeePayroll nên không cần thay đổi)

            if (string.IsNullOrEmpty(request.Month))
                return CreateErrorResponse(400, "Vui lòng cung cấp Month (YYYY-MM).");

            if (!DateTime.TryParseExact(
                request.Month,
                "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var startDate))
            {
                return CreateErrorResponse(400, "Định dạng tháng không hợp lệ. Vui lòng sử dụng định dạng bắt buộc YYYY-MM (ví dụ: 2025-10).");
            }

            int minYear = 2020;
            if (startDate.Year < minYear) return CreateErrorResponse(400, $"Chỉ được phép chốt lương từ năm {minYear} trở đi.");
            if (startDate.Year > DateTime.Now.Year || (startDate.Year == DateTime.Now.Year && startDate.Month > DateTime.Now.Month)) return CreateErrorResponse(400, "Không được chốt lương cho các tháng trong tương lai.");

            var endDate = startDate.AddMonths(1).AddDays(-1);

            // 2. TẠO HOẶC TÌM KIẾM PayrollRun
            var payrollRun = await _context.PayrollRuns.FirstOrDefaultAsync(pr => pr.Period == request.Month);

            if (payrollRun != null)
            {
                if (payrollRun.Status == PayrollRunStatus.locked) return CreateErrorResponse(400, $"Kỳ lương tháng {request.Month} đã được chốt (locked) và không thể chạy lại.");
            }
            else
            {
                payrollRun = new PayrollRun { Id = Guid.NewGuid(), Period = request.Month, Status = PayrollRunStatus.draft };
                _context.PayrollRuns.Add(payrollRun);
            }

            await _context.SaveChangesAsync();
            Guid actualPayrollRunId = payrollRun.Id;

            // 3. Lấy danh sách nhân viên đang hoạt động
            var employeesToRun = await _context.Employees.Where(e => e.Status == EmployeeStatus.active).ToListAsync();
            if (!employeesToRun.Any()) return NotFound(new { success = false, message = "Không tìm thấy nhân viên nào đang hoạt động." });

            var processedCount = 0;
            var processedSalaries = new List<object>();

            // 4. XỬ LÝ LƯƠNG TRONG VÒNG LẶP
            foreach (var employee in employeesToRun)
            {
                var employeeId = employee.Id;
                var result = await CalculateEmployeePayroll(employeeId, startDate, endDate);

                if (result == null) continue;

                // --- XÓA DỮ LIỆU CŨ ---
                var existingSalary = await _context.Salaries
                    .Where(s => s.EmployeeId == employeeId && s.PayrollRunId == actualPayrollRunId)
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync();

                if (existingSalary != null)
                {
                    _context.SalaryItems.RemoveRange(existingSalary.Items);
                    _context.Salaries.Remove(existingSalary);
                }

                // --- INSERT VÀO BẢNG SALARIES ---
                var newSalary = new Salary
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    PayrollRunId = actualPayrollRunId,
                    Gross = Math.Round(result.GrossSalary, 3),
                    Net = Math.Round(result.NetSalary, 3),
                    Details = $"Công thực tế: {result.SoCongThucTe} | Lần đi muộn: {result.SoLanDiMuon} | Lần vắng: {result.SoLanVang} | OT Thực tế: {result.TongGioOTThucTe}"
                };
                _context.Salaries.Add(newSalary);

                // --- INSERT VÀO BẢNG SALARYITEMS ---
                _context.SalaryItems.Add(new SalaryItem { Id = Guid.NewGuid(), SalaryId = newSalary.Id, Type = SalaryItemType.basic, Amount = result.LuongNgayCong, Note = $"Lương theo {result.SoCongThucTe} công" });
                if (result.TongPhuCap > 0) _context.SalaryItems.Add(new SalaryItem { Id = Guid.NewGuid(), SalaryId = newSalary.Id, Type = SalaryItemType.allowance, Amount = result.TongPhuCap, Note = "Tổng phụ cấp" });
                if (result.LuongOT > 0) _context.SalaryItems.Add(new SalaryItem { Id = Guid.NewGuid(), SalaryId = newSalary.Id, Type = SalaryItemType.ot, Amount = result.LuongOT, Note = $"Lương làm thêm giờ (Hệ số {result.HeSoOT})" });
                if (result.TongThuong > 0) _context.SalaryItems.Add(new SalaryItem { Id = Guid.NewGuid(), SalaryId = newSalary.Id, Type = SalaryItemType.bonus, Amount = result.TongThuong, Note = "Tổng tiền thưởng" });
                if (result.TongPhat > 0) _context.SalaryItems.Add(new SalaryItem { Id = Guid.NewGuid(), SalaryId = newSalary.Id, Type = SalaryItemType.deduction, Amount = -result.TongPhat, Note = "Tổng tiền phạt" });
                if (result.TongBaoHiem > 0) _context.SalaryItems.Add(new SalaryItem { Id = Guid.NewGuid(), SalaryId = newSalary.Id, Type = SalaryItemType.insurance, Amount = -result.TongBaoHiem, Note = "Khấu trừ bảo hiểm (BHXH, BHYT, BHTN)" });

                processedCount++;
                processedSalaries.Add(new { employeeId = employeeId, netSalary = newSalary.Net });
            }

            // 5. Cập nhật trạng thái và LƯU TẤT CẢ THAY ĐỔI
            if (processedCount > 0)
            {
                payrollRun.Status = PayrollRunStatus.processed;
            }
            await _context.SaveChangesAsync();


            // 6. Gửi thông báo
            try
            {
                if (processedCount > 0)
                {
                    // A. Lấy danh sách EmployeeId vừa được tính lương
                    // (employeesToRun là danh sách nhân viên active đã lấy ở Step 3)
                    var processedEmployeeIds = employeesToRun.Select(e => e.Id).ToList();

                    // B. Tìm UserID tương ứng với các EmployeeId này
                    // (Vì bảng Users có cột EmployeeId, ta cần map qua để biết gửi cho tài khoản nào)
                    var targetUserIds = await _context.Users
                        .Where(u => u.EmployeeId != null && processedEmployeeIds.Contains(u.EmployeeId))
                        .Select(u => u.Id)
                        .ToListAsync();

                    if (targetUserIds.Any())
                    {
                        // Định dạng lại tháng cho đẹp (Ví dụ: 2025-10 -> 10/2025)
                        string formattedMonth = startDate.ToString("MM/yyyy");

                        string title = $"Phiếu lương tháng {formattedMonth}";
                        string content = $"Lương tháng {formattedMonth} của bạn đã được chốt. Vui lòng kiểm tra chi tiết trong ứng dụng.";

                        // Gọi Service bắn thông báo hàng loạt (SignalR + Firebase)
                        await _notificationService.SendPayrollNotificationAsync(title, content, targetUserIds);

                        Console.WriteLine($"✅ Đã gửi thông báo lương cho {targetUserIds.Count} nhân viên.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log lỗi thông báo nhưng KHÔNG làm fail API chính (vì lương đã lưu rồi)
                Console.WriteLine($"⚠️ Lỗi gửi thông báo lương: {ex.Message}");
            }
            // ========================================================================

            return Ok(new
            {
                statusCode = 200,
                success = true,
                message = $"Chốt lương thành công cho {processedCount} nhân viên tháng {request.Month}.",
                data = processedSalaries
            });
        }
    }
}