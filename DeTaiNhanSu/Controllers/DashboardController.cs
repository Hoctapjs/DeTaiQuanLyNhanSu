using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/dashboard
        // Trả về: summary, charts (hires/quits 12 tháng), employeesByDepartment, expiringContracts (withinDays)
        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetDashboard([FromQuery] int expiringWithinDays = 30, CancellationToken ct = default)
        {
            try
            {
                // Sử dụng DateOnly cho các so sánh ngày (như trong logic expiringQuery của bạn)
                var todayDO = DateOnly.FromDateTime(DateTime.UtcNow.Date);

                // --- Định nghĩa các nhóm nhân viên dựa trên Contract ---

                // 1. ID của những nhân viên "ĐANG LÀM VIỆC" (active right now)
                // Logic: Có ít nhất 1 HĐ có Status != terminated VÀ StartDate <= today VÀ (EndDate >= today HOẶC EndDate == null)
                var workingEmployeeIds = _db.Contracts.AsNoTracking()
                    .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated &&
                                c.StartDate <= todayDO &&
                                (c.EndDate == null || c.EndDate >= todayDO))
                    .Select(c => c.EmployeeId)
                    .Distinct();

                // 2. ID của "TỔNG NHÂN VIÊN" (tổng số người còn ràng buộc HĐ)
                // Logic: Có ít nhất 1 HĐ có Status != terminated (bao gồm cả HĐ tương lai, HĐ đang nghỉ phép, v.v.)
                var totalEmployeeIdsWithContracts = _db.Contracts.AsNoTracking()
                    .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated)
                    .Select(c => c.EmployeeId)
                    .Distinct();


                // --- Summary ---

                // Tổng nhân viên (ĐÃ SỬA)
                var totalEmployees = await totalEmployeeIdsWithContracts.CountAsync(ct);

                // Tổng phòng ban
                var totalDepartments = await _db.Departments.AsNoTracking().CountAsync(ct);

                // Đang làm việc (ĐÃ SỬA)
                var workingCount = await workingEmployeeIds.CountAsync(ct);

                // Hợp đồng sắp hết hạn
                if (expiringWithinDays < 1) expiringWithinDays = 30;
                var untilDO = todayDO.AddDays(expiringWithinDays);

                var expiringQuery = _db.Contracts.AsNoTracking()
                    .Include(c => c.Employee)
                    .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated &&
                                c.EndDate != null &&
                                c.EndDate >= todayDO &&
                                c.EndDate <= untilDO);

                var expiringCount = await expiringQuery.CountAsync(ct);

                // thống kê chấm công theo ngày
                // =========================================================
                // === THỐNG KÊ CHẤM CÔNG HÔM NAY (PHẦN MỚI) ===
                // =========================================================

                    // 1. Lấy số lượng nhân viên "ĐANG LÀM VIỆC" nhưng "ĐANG NGHỈ PHÉP" (đã duyệt)
                    // (Dùng logic tương tự IsDayApprovedForLeave)
                    var onLeaveWorkingIds = await workingEmployeeIds
                        .Intersect(_db.Requests.AsNoTracking()
                            .Where(r => r.Status == RequestStatus.approved &&
                                        r.Category == RequestCategory.leave &&
                                        r.FromDate.HasValue && r.FromDate.Value <= todayDO &&
                                        r.ToDate.HasValue && r.ToDate.Value >= todayDO)
                            .Select(r => r.EmployeeId))
                        .ToListAsync(ct);

                    int onLeaveCount = onLeaveWorkingIds.Count;

                    // 2. Lấy tất cả trạng thái chấm công hôm nay (chỉ 1 query)
                    var todayAttendanceStats = await _db.Attendances.AsNoTracking()
                        .Where(a => a.Date == todayDO)
                        .GroupBy(a => a.Status)
                        .Select(g => new { Status = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

                    // 3. Tính toán các số liệu
                    int lateCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.late, 0);
                    int onTimeCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.present, 0);
                    int completedCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.completed, 0);
                    int absentCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.absent, 0);

                    // Tổng đã check-in = đúng giờ + trễ + đã check-out
                    int checkedInCount = onTimeCount + lateCount + completedCount;

                    // Tổng số người *nên* làm việc hôm nay
                    int totalWorkingToday = workingCount;

                    // Số người chưa check-in = Tổng NV - (Đã check-in) - (Vắng) - (Nghỉ phép)
                    int notCheckedInYetCount = totalWorkingToday - checkedInCount - absentCount - onLeaveCount;

                // thống kê nghỉ phép
                    // 1. Đang chờ duyệt (chỉ category nghỉ phép)
                    int pendingLeaveRequests = await _db.Requests.AsNoTracking()
                        .CountAsync(r => r.Category == RequestCategory.leave &&
                                         r.Status == RequestStatus.pending, ct); // Giả sử có .pending

                    // 2. Đã duyệt trong tháng này (tính theo ngày bắt đầu)
                    var monthStart = new DateOnly(todayDO.Year, todayDO.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                    int approvedLeaveThisMonth = await _db.Requests.AsNoTracking()
                        .CountAsync(r => r.Category == RequestCategory.leave &&
                                         r.Status == RequestStatus.approved &&
                                         r.FromDate.HasValue &&
                                         r.FromDate.Value >= monthStart &&
                                         r.FromDate.Value <= monthEnd, ct);

                // (biến onLeaveCount đã được tính ở THỐNG KÊ CHẤM CÔNG)

                // thống kê khóa học
                    // Tổng số khóa học
                    int totalCourses = await _db.Courses.AsNoTracking().CountAsync(ct);

                    // Khóa học mới trong tháng này (dựa trên CreateAt là DateTime.UtcNow)
                    // Chúng ta dùng DateTime.UtcNow vì CreateAt trong CourseController dùng nó
                    var utcNowForCourses = DateTime.UtcNow;
                    int newCoursesThisMonth = await _db.Courses
                        .AsNoTracking()
                        .CountAsync(c => c.CreatedAt.Year == utcNowForCourses.Year &&
                                         c.CreatedAt.Month == utcNowForCourses.Month, ct);

                // thống kê vi phạm kỷ luật
                // Lấy thống kê vi phạm trong tháng này (từ đầu tháng đến hôm nay)
                var penaltyStats = await _db.RewardPenalties
                    .AsNoTracking()
                    .Where(x => x.Type.Type == RewardPenaltyKind.penalty && // Chỉ lọc 'Kỷ luật'
                                x.DecidedAt >= monthStart &&
                                x.DecidedAt <= todayDO)
                    .GroupBy(x => 1) // Nhóm tất cả lại thành 1
                    .Select(g => new {
                        ThisMonth = g.Count(), // Đếm tổng số vi phạm trong tháng
                        Today = g.Count(x => x.DecidedAt == todayDO) // Đếm số vi phạm hôm nay
                    })
                    .FirstOrDefaultAsync(ct);

                int penaltiesThisMonth = penaltyStats?.ThisMonth ?? 0;
                int penaltiesToday = penaltyStats?.Today ?? 0;

                // --- Charts: last 12 months hires & quits ---
                // (Giữ nguyên logic này vì nó dựa trên Employee.HireDate và Employee.TerminationDate,
                // vốn là các sự kiện "vào" và "ra", không phải trạng thái "đang làm việc")
                var utcNow = DateTime.UtcNow;
                var startMonth = new DateTime(utcNow.Year, utcNow.Month, 1).AddMonths(-11);
                var months = Enumerable.Range(0, 12)
                    .Select(i => startMonth.AddMonths(i))
                    .ToList();

                var hiresRaw = await _db.Employees
                    .AsNoTracking()
                    .Where(e => e.HireDate != null)
                    .Select(e => new { e.HireDate })
                    .ToListAsync(ct);

                var quitsRaw = await _db.Contracts
                    .AsNoTracking()
                    // Giả sử 'terminated' là trạng thái cho biết nhân viên đã nghỉ
                    .Where(c => c.Status == DeTaiNhanSu.Enums.ContractStatus.terminated && c.EndDate != null)
                    .Select(c => new { TerminationDate = c.EndDate }) // Dùng EndDate làm ngày nghỉ
                    .ToListAsync(ct);

                var labels = months.Select(m => m.ToString("MMM yyyy", CultureInfo.InvariantCulture)).ToList();
                var hires = new List<int>();
                var quits = new List<int>();

                foreach (var m in months)
                {
                    var year = m.Year;
                    var month = m.Month;

                    var hireCount = hiresRaw.Count(x =>
                    {
                        if (x.HireDate is DateOnly d) return d.Year == year && d.Month == month;
                        return false;
                    });

                    var quitCount = quitsRaw.Count(x =>
                    {
                        // 'TerminationDate' ở đây chính là 'c.EndDate' từ truy vấn Contracts ở trên
                        if (x.TerminationDate is DateOnly d) return d.Year == year && d.Month == month;
                        return false;
                    });

                    hires.Add(hireCount);
                    quits.Add(quitCount);
                }

                // --- Employees by department --- (ĐÃ SỬA)
                // Sửa lại để chỉ đếm nhân viên "đang làm việc" VÀ khắc phục lỗi N+1

                //// 1. Lấy số lượng NV đang làm việc theo từng DepartmentId
                //var workingCountsByDept = await _db.Employees
                //    .AsNoTracking()
                //    .Where(e => workingEmployeeIds.Contains(e.Id)) // Chỉ lọc nhân viên "đang làm việc"
                //    .GroupBy(e => e.DepartmentId)
                //    .Select(g => new
                //    {
                //        DepartmentId = g.Key,
                //        Count = g.Count()
                //    })
                //    .ToDictionaryAsync(k => k.DepartmentId, v => v.Count, ct);

                var workingCountsByDept = await _db.Employees
                    .AsNoTracking()
                    .Where(e => workingEmployeeIds.Contains(e.Id))
                    .GroupBy(e => e.DepartmentId)
                    .Where(g => g.Key != null)
                    .Select(g => new
                    {
                        DepartmentId = g.Key.Value,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(k => k.DepartmentId, v => v.Count, ct);

                var allDepartments = await _db.Departments
                    .AsNoTracking()
                    .Select(d => new
                    {
                        departmentId = d.Id,
                        departmentName = d.Name,
                    })
                    .ToListAsync(ct);

                // 2. Lấy tất cả phòng ban và map với số lượng đã đếm
                var employeesByDept = allDepartments
                    .Select(d => new
                    {
                        d.departmentId,
                        d.departmentName,
                        // Dùng dictionary (workingCountsByDept) trong bộ nhớ
                        count = workingCountsByDept.GetValueOrDefault(d.departmentId, 0)
                    })
                    .OrderByDescending(d => d.count) // Sắp xếp trong bộ nhớ (OK)
                    .ToList();


                // --- Expiring contracts list (top 10 ordered by EndDate) ---
                var expiringList = await expiringQuery
                    .OrderBy(c => c.EndDate)
                    .Take(10)
                    .Select(c => new
                    {
                        id = c.Id,
                        employeeId = c.EmployeeId,
                        employeeName = c.Employee.FullName,
                        contractNumber = c.ContractNumber,
                        endDate = c.EndDate,
                        status = c.Status.ToString().ToLower()
                    })
                    .ToListAsync(ct);

                // --- Build payload ---
                var payload = new
                {
                    summary = new
                    {
                        totalEmployees,
                        totalDepartments,
                        workingCount,
                        contractsExpiring = expiringCount,
                        contractsExpiringWithinDays = expiringWithinDays
                    },
                    charts = new
                    {
                        hiresQuits = new
                        {
                            labels,
                            hires,
                            quits
                        }
                    },
                    employeesByDepartment = employeesByDept,
                    expiringContracts = new
                    {
                        meta = new { count = expiringCount },
                        items = expiringList
                    },
                    attendanceToday = new
                    {
                        totalWorkingToday = totalWorkingToday, // Tổng NV có HĐ
                        checkedIn = checkedInCount,            // Đã check-in (đúng giờ + trễ)
                        onTime = onTimeCount + completedCount, // Đúng giờ (bao gồm cả đã checkout)
                        late = lateCount,                      // Đi trễ
                        onLeave = onLeaveCount,                // Nghỉ phép (đã duyệt)
                        absent = absentCount,                  // Vắng (đã bị đánh dấu)
                        notCheckedInYet = Math.Max(0, notCheckedInYetCount) // Chưa check-in
                    },
                    leaveStats = new
                    {
                        onLeaveToday = onLeaveCount, // (Dùng chung)
                        pendingApproval = pendingLeaveRequests,
                        approvedThisMonth = approvedLeaveThisMonth
                    },
                    disciplineStats = new
                    {
                        penaltiesThisMonth = penaltiesThisMonth,
                        penaltiesToday = penaltiesToday
                    },
                    courseStats = new
                    {
                        total = totalCourses,
                        newThisMonth = newCoursesThisMonth
                    }
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Lấy dữ liệu dashboard thành công.",
                    data = new { result = payload },
                    success = true
                });
            }
            catch (Exception ex) // Bắt lỗi cụ thể để debug
            {
                // Bạn nên log lỗi 'ex' ở đây
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    statusCode = StatusCodes.Status500InternalServerError,
                    message = $"Đã xảy ra lỗi khi lấy dashboard: {ex.Message}", // Thêm ex.Message để biết lỗi
                    data = new { result = (object?)null },
                    success = false
                });
            }
        }

        // tình hình chấm công

        // thống kê nghỉ phép

        // thống kê lương

        // thống kê vi phạm kỷ luật

        // thống kê số khóa học

    }
}