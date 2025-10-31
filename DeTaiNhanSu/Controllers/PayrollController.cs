using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Enums;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core.Exceptions;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;

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
        public PayrollController(AppDbContext context) => _context = context;

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
        // PHƯƠNG THỨC TÍNH TOÁN LƯƠNG CHUNG (CORE LOGIC)
        // =================================================================
        private async Task<PayrollCalculationResult?> CalculateEmployeePayroll(Guid employeeId, DateTime startDate, DateTime endDate)
        {
            var startOnly = DateOnly.FromDateTime(startDate);
            var endOnly = DateOnly.FromDateTime(endDate);

            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.Status == ContractStatus.active);
            if (contract == null) return null;

            var attendanceList = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.Date >= startOnly && a.Date <= endOnly).ToListAsync();
            var otList = await _context.Overtimes
                .Where(o => o.EmployeeId == employeeId && o.Date >= startOnly && o.Date <= endOnly).ToListAsync();

            var rewards = await _context.RewardPenalties.Include(r => r.Type)
                .Where(r => r.EmployeeId == employeeId && r.DecidedAt >= startOnly && r.DecidedAt <= endOnly)
                .ToListAsync();

            var salaryPreview = await _context.Salaries.Include(s => s.Items).OrderByDescending(s => s.PayrollRunId).FirstOrDefaultAsync(s => s.EmployeeId == employeeId);

            // =================================================================
            // LOGIC MỚI: TÍNH SỐ CÔNG PHÂN CÔNG THEO GLOBAL SETTINGS
            // =================================================================
            string weekendDaysString = await GetGlobalSettingString("WEEKEND_DAYS", "Sunday");
            var configuredWeekendDays = weekendDaysString.Split(',')
                .Select(d => d.Trim())
                .Where(d => Enum.TryParse(d, true, out DayOfWeek _))
                .Select(d => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), d, true))
                .ToList();

            int soCongPhanCong = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(i => startDate.AddDays(i))
                .Count(d => !configuredWeekendDays.Contains(d.DayOfWeek));
            // =================================================================

            decimal tongCongThucTe = 0;
            int soLanDiMuon = 0;
            int soLanVang = 0;
            int soLanVeSom = 0;
            decimal tongGioOTThucTe = 0;
            decimal tongGioOTDaDangKy = otList.Sum(o => o.Hours);

            TimeOnly standardStartTime = new TimeOnly(8, 0, 0);
            TimeOnly standardEndTime = new TimeOnly(17, 0, 0);

            decimal luongMotNgayCong = soCongPhanCong == 0 ? 0 : contract.BasicSalary / soCongPhanCong;
            decimal luongMotGio = luongMotNgayCong / 8;


            foreach (var att in attendanceList)
            {
                decimal congNgay = 1m;

                // 1. Xử lý VẮNG MẶT, NGHỈ PHÉP (Dùng Enum)
                if (att.Status == AttendanceStatus.absent)
                {
                    soLanVang++;
                    congNgay = 0m;
                }
                else if (att.Status == AttendanceStatus.leave)
                {
                    congNgay = 1m;
                }

                // 2. Xử lý LATE VÀ PHẠT (Dùng Enum)
                else if (att.Status == AttendanceStatus.late)
                {
                    soLanDiMuon++;

                    if (att.CheckIn.HasValue)
                    {
                        TimeSpan gioTrễ = att.CheckIn.Value - standardStartTime;

                        // Phạt đi trễ quá 1 giờ
                        if (gioTrễ.TotalHours > 1)
                        {
                            congNgay = 0.5m;
                        }
                    }
                }

                // 3. XỬ LÝ VỀ SỚM VÀ PHẠT
                if (att.CheckOut.HasValue && att.CheckOut.Value < standardEndTime)
                {
                    TimeSpan earlyLeaveDuration = standardEndTime - att.CheckOut.Value;

                    if (earlyLeaveDuration.TotalMinutes > 10) // Về sớm quá 10 phút
                    {
                        soLanVeSom++;
                        if (congNgay == 1m)
                        {
                            congNgay = 0.5m;
                        }
                    }
                }

                tongCongThucTe += congNgay;


                // 4. TÍNH VÀ GIỚI HẠN GIỜ OT THỰC TẾ
                var otRecord = otList.FirstOrDefault(o => o.Date == att.Date);
                if (otRecord != null && att.CheckOut.HasValue)
                {
                    TimeOnly actualCheckOut = att.CheckOut.Value;
                    if (actualCheckOut > standardEndTime)
                    {
                        decimal calculatedOt = (decimal)(actualCheckOut - standardEndTime).TotalHours;
                        decimal finalOtHours = Math.Min(otRecord.Hours, calculatedOt);

                        tongGioOTThucTe += finalOtHours;
                    }
                }
            }

            // Thưởng / phạt, Phụ cấp
            decimal tongThuong = rewards.Where(r => r.Type.Type == RewardPenaltyKind.reward).Sum(r => r.AmountOverride.GetValueOrDefault(r.Type.DefaultAmount ?? 0));
            decimal tongPhat = rewards.Where(r => r.Type.Type == RewardPenaltyKind.penalty).Sum(r => r.AmountOverride.GetValueOrDefault(r.Type.DefaultAmount ?? 0));

            decimal tongPhuCap = salaryPreview?.Items?.Where(i => i.Type == SalaryItemType.allowance).Sum(i => i.Amount) ?? 0;

            // Tính lương OT
            decimal heSoOT = otList.FirstOrDefault()?.Rate ?? await GetGlobalSettingValue("DEFAULT_OT_RATE", 1.5m);
            decimal luongOTThucTe = tongGioOTThucTe * luongMotGio * heSoOT;

            // BẢO HIỂM (ĐỌC TỪ GLOBAL SETTINGS)
            decimal bhxhRate = await GetGlobalSettingValue("EMP_BHXH_RATE", 0.08m);
            decimal bhytRate = await GetGlobalSettingValue("EMP_BHYT_RATE", 0.015m);
            decimal bhtnRate = await GetGlobalSettingValue("EMP_BHTN_RATE", 0.01m);

            decimal insuranceSalary = contract.InsuranceSalary.HasValue && contract.InsuranceSalary.Value > 0
                ? contract.InsuranceSalary.Value
                : contract.BasicSalary;

            decimal tongBaoHiem = (bhxhRate + bhytRate + bhtnRate) * insuranceSalary;

            // Lương Net/Gross
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
        // API GET DAILY DETAILS (Chi tiết theo ngày)
        // =================================================================
        [HttpGet("daily/{employeeId}")]
        public async Task<IActionResult> GetDailyDetails(Guid employeeId, [FromQuery] string? month)
        {
            if (!await _context.Employees.AnyAsync(e => e.Id == employeeId)) return CreateErrorResponse(404, "Không tìm thấy nhân viên với ID được cung cấp.");

            if (string.IsNullOrEmpty(month)) month = DateTime.Now.ToString("yyyy-MM");
            if (!DateTime.TryParseExact($"{month}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startDate))
                return CreateErrorResponse(400, "Định dạng tháng không hợp lệ.");

            var endDate = startDate.AddMonths(1).AddDays(-1);

            var startOnly = DateOnly.FromDateTime(startDate);
            var endOnly = DateOnly.FromDateTime(endDate);


            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.Status == ContractStatus.active);
            if (contract == null) return CreateErrorResponse(404, "Không tìm thấy hợp đồng đang hoạt động cho nhân viên này.");

            // =================================================================
            // LOGIC MỚI: TÍNH SỐ CÔNG PHÂN CÔNG THEO GLOBAL SETTINGS
            // =================================================================
            string weekendDaysString = await GetGlobalSettingString("WEEKEND_DAYS", "Sunday");
            var configuredWeekendDays = weekendDaysString.Split(',')
                .Select(d => d.Trim())
                .Where(d => Enum.TryParse(d, true, out DayOfWeek _))
                .Select(d => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), d, true))
                .ToList();

            int soCongPhanCong = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(i => startDate.AddDays(i))
                .Count(d => !configuredWeekendDays.Contains(d.DayOfWeek));
            // =================================================================


            var attendanceList = await _context.Attendances.Where(a => a.EmployeeId == employeeId && a.Date >= startOnly && a.Date <= endOnly).ToListAsync();
            var otList = await _context.Overtimes.Where(o => o.EmployeeId == employeeId && o.Date >= startOnly && o.Date <= endOnly).ToListAsync();

            var rewards = await _context.RewardPenalties.Include(r => r.Type)
                .Where(r => r.EmployeeId == employeeId && r.DecidedAt >= startOnly && r.DecidedAt <= endOnly).ToListAsync();


            var salary = await _context.Salaries.Include(s => s.Items).OrderByDescending(s => s.PayrollRunId).FirstOrDefaultAsync(s => s.EmployeeId == employeeId);
            decimal tongPhuCap = salary?.Items?.Where(i => i.Type == SalaryItemType.allowance).Sum(i => i.Amount) ?? 0;

            decimal phuCapTheoNgay = soCongPhanCong == 0 ? 0 : tongPhuCap / soCongPhanCong;
            decimal luongMotNgayCong = soCongPhanCong == 0 ? 0 : contract.BasicSalary / soCongPhanCong;
            decimal luongMotGio = luongMotNgayCong / 8;
            TimeOnly standardEndTime = new TimeOnly(17, 0, 0);
            TimeOnly standardStartTime = new TimeOnly(8, 0, 0);

            var daysInMonth = Enumerable.Range(0, (endDate - startDate).Days + 1).Select(i => startDate.AddDays(i)).ToList();
            var result = new List<object>();

            foreach (var day in daysInMonth)
            {
                var currentDayOnly = DateOnly.FromDateTime(day);

                var att = attendanceList.FirstOrDefault(a => a.Date == currentDayOnly);
                var otRecord = otList.FirstOrDefault(o => o.Date == currentDayOnly);

                string? status = att?.Status.ToString();
                decimal soCong = 0;
                decimal otTrongNgay = 0;

                decimal gioOtDuocDuyet = otRecord?.Hours ?? 0;
                decimal gioOtThucTe = 0;
                decimal luongOtThucTe = 0;
                string ghiChuPhat = "";

                // TÍNH SỐ CÔNG VÀ GIỜ OT
                if (att != null)
                {
                    soCong = 1m;

                    // 1. XỬ LÝ ABSENT/LEAVE (Dùng Enum)
                    if (att.Status == AttendanceStatus.absent)
                    {
                        soCong = 0m;
                    }
                    else if (att.Status == AttendanceStatus.leave)
                    {
                        soCong = 1m;
                    }

                    // 2. XỬ LÝ LATE VÀ PHẠT (Dùng Enum)
                    else if (att.Status == AttendanceStatus.late)
                    {
                        if (att.CheckIn.HasValue)
                        {
                            TimeSpan gioTre = att.CheckIn.Value - standardStartTime;
                            double soGioTre = gioTre.TotalHours;

                            if (soGioTre > 1)
                            {
                                soCong = 0.5m;
                                ghiChuPhat += " [PHẠT TRỄ - TRỪ 0.5 CÔNG]";
                            }
                        }
                    }

                    // 3. XỬ LÝ VỀ SỚM VÀ PHẠT
                    if (att.CheckOut.HasValue && att.CheckOut.Value < standardEndTime)
                    {
                        TimeSpan earlyLeaveDuration = standardEndTime - att.CheckOut.Value;

                        if (earlyLeaveDuration.TotalMinutes > 10)
                        {
                            if (soCong == 1m)
                            {
                                soCong = 0.5m;
                                ghiChuPhat += " [PHẠT SỚM - TRỪ 0.5 CÔNG]";
                            }
                            else if (soCong == 0.5m && ghiChuPhat.Contains("TRỄ"))
                            {
                                ghiChuPhat += " [Kèm Về Sớm]";
                            }
                            else if (soCong == 0.5m)
                            {
                                ghiChuPhat += " [PHẠT SỚM]";
                            }
                        }
                    }


                    // 4. Tính OT thực tế trong ngày
                    if (otRecord != null && att.CheckOut.HasValue && att.CheckOut.Value > standardEndTime)
                    {
                        decimal calculatedOt = (decimal)(att.CheckOut.Value - standardEndTime).TotalHours;
                        decimal finalOtHours = Math.Min(otRecord.Hours, calculatedOt);

                        gioOtThucTe = finalOtHours;
                        luongOtThucTe = finalOtHours * (otRecord.Rate) * luongMotGio;
                    }
                }

                decimal luongNgay = luongMotNgayCong * soCong;

                decimal phatTrongNgay = rewards.Where(r => r.Type.Type == RewardPenaltyKind.penalty && r.DecidedAt == currentDayOnly).Sum(r => r.AmountOverride.GetValueOrDefault(0));
                decimal thuongTrongNgay = rewards.Where(r => r.Type.Type == RewardPenaltyKind.reward && r.DecidedAt == currentDayOnly).Sum(r => r.AmountOverride.GetValueOrDefault(0));


                decimal tongLuongNgay = luongNgay + phuCapTheoNgay + thuongTrongNgay + luongOtThucTe - phatTrongNgay;

                string finalNote = (att?.Note ?? "") + (ghiChuPhat != "" ? " | Phạt: " + ghiChuPhat : "");

                result.Add(new
                {
                    ngay = day.ToString("yyyy-MM-dd"),
                    trangThai = status,
                    soCong = Math.Round(soCong, 3),
                    phuCap = Math.Round(phuCapTheoNgay, 3),
                    thuong = Math.Round(thuongTrongNgay, 3),
                    gioOtDuocDuyet = Math.Round(gioOtDuocDuyet, 3),
                    gioOtThucTe = Math.Round(gioOtThucTe, 3),
                    luongOt = Math.Round(luongOtThucTe, 3),
                    phat = Math.Round(phatTrongNgay, 3),
                    luongNgay = Math.Round(tongLuongNgay, 3),
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