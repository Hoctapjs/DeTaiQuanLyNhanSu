using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using DeTaiNhanSu.Dtos.DashboardDtoFol;
using DeTaiNhanSu.Models;
using System.Linq.Dynamic.Core;
using DocumentFormat.OpenXml;

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
        //[HttpGet]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> GetDashboard([FromQuery] int expiringWithinDays = 30, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        // Sử dụng DateOnly cho các so sánh ngày (như trong logic expiringQuery của bạn)
        //        var todayDO = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        //        // --- Định nghĩa các nhóm nhân viên dựa trên Contract ---

        //        // 1. ID của những nhân viên "ĐANG LÀM VIỆC" (active right now)
        //        // Logic: Có ít nhất 1 HĐ có Status != terminated VÀ StartDate <= today VÀ (EndDate >= today HOẶC EndDate == null)
        //        var workingEmployeeIds = _db.Contracts.AsNoTracking()
        //            .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated &&
        //                        c.StartDate <= todayDO &&
        //                        (c.EndDate == null || c.EndDate >= todayDO))
        //            .Select(c => c.EmployeeId)
        //            .Distinct();

        //        // 2. ID của "TỔNG NHÂN VIÊN" (tổng số người còn ràng buộc HĐ)
        //        // Logic: Có ít nhất 1 HĐ có Status != terminated (bao gồm cả HĐ tương lai, HĐ đang nghỉ phép, v.v.)
        //        var totalEmployeeIdsWithContracts = _db.Contracts.AsNoTracking()
        //            .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated)
        //            .Select(c => c.EmployeeId)
        //            .Distinct();


        //        // --- Summary ---

        //        // Tổng nhân viên (ĐÃ SỬA)
        //        var totalEmployees = await totalEmployeeIdsWithContracts.CountAsync(ct);

        //        // Tổng phòng ban
        //        var totalDepartments = await _db.Departments.AsNoTracking().CountAsync(ct);

        //        // Đang làm việc (ĐÃ SỬA)
        //        var workingCount = await workingEmployeeIds.CountAsync(ct);

        //        // Hợp đồng sắp hết hạn
        //        if (expiringWithinDays < 1) expiringWithinDays = 30;
        //        var untilDO = todayDO.AddDays(expiringWithinDays);

        //        var expiringQuery = _db.Contracts.AsNoTracking()
        //            .Include(c => c.Employee)
        //            .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated &&
        //                        c.EndDate != null &&
        //                        c.EndDate >= todayDO &&
        //                        c.EndDate <= untilDO);

        //        var expiringCount = await expiringQuery.CountAsync(ct);

        //        // thống kê chấm công theo ngày hiện tại
        //            // 1. Lấy số lượng nhân viên "ĐANG LÀM VIỆC" nhưng "ĐANG NGHỈ PHÉP" (đã duyệt)
        //            // (Dùng logic tương tự IsDayApprovedForLeave)
        //            var onLeaveWorkingIds = await workingEmployeeIds
        //                .Intersect(_db.Requests.AsNoTracking()
        //                    .Where(r => r.Status == RequestStatus.approved &&
        //                                r.Category == RequestCategory.leave &&
        //                                r.FromDate.HasValue && r.FromDate.Value <= todayDO &&
        //                                r.ToDate.HasValue && r.ToDate.Value >= todayDO)
        //                    .Select(r => r.EmployeeId))
        //                .ToListAsync(ct);

        //            int onLeaveCount = onLeaveWorkingIds.Count;

        //            // 2. Lấy tất cả trạng thái chấm công hôm nay (chỉ 1 query)
        //            var todayAttendanceStats = await _db.Attendances.AsNoTracking()
        //                .Where(a => a.Date == todayDO)
        //                .GroupBy(a => a.Status)
        //                .Select(g => new { Status = g.Key, Count = g.Count() })
        //                .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

        //            // 3. Tính toán các số liệu
        //            int lateCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.late, 0);
        //            int onTimeCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.present, 0);
        //            int completedCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.completed, 0);
        //            int absentCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.absent, 0);

        //            // Tổng đã check-in = đúng giờ + trễ + đã check-out
        //            int checkedInCount = onTimeCount + lateCount + completedCount;

        //            // Tổng số người *nên* làm việc hôm nay
        //            int totalWorkingToday = workingCount;

        //            // Số người chưa check-in = Tổng NV - (Đã check-in) - (Vắng) - (Nghỉ phép)
        //            int notCheckedInYetCount = totalWorkingToday - checkedInCount - absentCount - onLeaveCount;

        //        // thống kê nghỉ phép
        //            // 1. Đang chờ duyệt (chỉ category nghỉ phép)
        //            int pendingLeaveRequests = await _db.Requests.AsNoTracking()
        //                .CountAsync(r => r.Category == RequestCategory.leave &&
        //                                 r.Status == RequestStatus.pending, ct); // Giả sử có .pending

        //            // 2. Đã duyệt trong tháng này (tính theo ngày bắt đầu)
        //            var monthStart = new DateOnly(todayDO.Year, todayDO.Month, 1);
        //            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        //            int approvedLeaveThisMonth = await _db.Requests.AsNoTracking()
        //                .CountAsync(r => r.Category == RequestCategory.leave &&
        //                                 r.Status == RequestStatus.approved &&
        //                                 r.FromDate.HasValue &&
        //                                 r.FromDate.Value >= monthStart &&
        //                                 r.FromDate.Value <= monthEnd, ct);

        //        // (biến onLeaveCount đã được tính ở THỐNG KÊ CHẤM CÔNG)

        //        // thống kê khóa học
        //            // Tổng số khóa học
        //            int totalCourses = await _db.Courses.AsNoTracking().CountAsync(ct);

        //            // Khóa học mới trong tháng này (dựa trên CreateAt là DateTime.UtcNow)
        //            // Chúng ta dùng DateTime.UtcNow vì CreateAt trong CourseController dùng nó
        //            var utcNowForCourses = DateTime.UtcNow;
        //            int newCoursesThisMonth = await _db.Courses
        //                .AsNoTracking()
        //                .CountAsync(c => c.CreatedAt.Year == utcNowForCourses.Year &&
        //                                 c.CreatedAt.Month == utcNowForCourses.Month, ct);

        //        // thống kê vi phạm kỷ luật
        //            // Lấy thống kê vi phạm trong tháng này (từ đầu tháng đến hôm nay)
        //            var penaltyStats = await _db.RewardPenalties
        //                .AsNoTracking()
        //                .Where(x => x.Type.Type == RewardPenaltyKind.penalty && // Chỉ lọc 'Kỷ luật'
        //                            x.DecidedAt >= monthStart &&
        //                            x.DecidedAt <= todayDO)
        //                .GroupBy(x => 1) // Nhóm tất cả lại thành 1
        //                .Select(g => new {
        //                    ThisMonth = g.Count(), // Đếm tổng số vi phạm trong tháng
        //                    Today = g.Count(x => x.DecidedAt == todayDO) // Đếm số vi phạm hôm nay
        //                })
        //                .FirstOrDefaultAsync(ct);

        //            int penaltiesThisMonth = penaltyStats?.ThisMonth ?? 0;
        //            int penaltiesToday = penaltyStats?.Today ?? 0;

        //        // thống kê lương
        //            // 1. Tìm kỳ lương (PayrollRun) đã chốt (processed/locked) gần đây nhất
        //            var lastFinalizedRun = await _db.PayrollRuns
        //                .AsNoTracking()
        //                .Where(pr => pr.Status == PayrollRunStatus.processed || pr.Status == PayrollRunStatus.locked)
        //                .OrderByDescending(pr => pr.Period) // Sắp xếp theo "YYYY-MM" (ví dụ)
        //                .FirstOrDefaultAsync(ct);

        //            string? lastSalaryPeriod = null;
        //            decimal totalGrossLastMonth = 0;
        //            decimal totalNetLastMonth = 0;

        //            if (lastFinalizedRun != null)
        //            {
        //                // 2. Nếu tìm thấy, lấy tên kỳ và tổng lương của kỳ đó
        //                lastSalaryPeriod = lastFinalizedRun.Period;

        //                var salaryStats = await _db.Salaries
        //                    .AsNoTracking()
        //                    .Where(s => s.PayrollRunId == lastFinalizedRun.Id)
        //                    .GroupBy(s => 1)
        //                    .Select(g => new {
        //                        TotalGross = g.Sum(s => s.Gross),
        //                        TotalNet = g.Sum(s => s.Net)
        //                    })
        //                    .FirstOrDefaultAsync(ct);

        //                if (salaryStats != null)
        //                {
        //                    totalGrossLastMonth = salaryStats.TotalGross;
        //                    totalNetLastMonth = salaryStats.TotalNet;
        //                }
        //            }

        //        // thống kê hiệu suất làm việc
        //            // hiệu suất chuyên cần
        //            // hiệu suất đào tạo
        //                // 1. Thống kê chuyên cần (chấm công) trong tháng này
        //                var monthAttendanceStats = await _db.Attendances
        //                    .AsNoTracking()
        //                    .Where(a => a.Date >= monthStart && a.Date <= todayDO)
        //                    .GroupBy(a => a.Status)
        //                    .Select(g => new { Status = g.Key, Count = g.Count() })
        //                    .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

        //                int perf_totalLate = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.late, 0);
        //                int perf_totalAbsent = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.absent, 0);
        //                int perf_totalOnTime = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.present, 0) +
        //                                       monthAttendanceStats.GetValueOrDefault(AttendanceStatus.completed, 0);

        //                // 2. Thống kê kết quả đào tạo (tổng quan)
        //                var allTrainingStats = await _db.TrainingRecords
        //                    .AsNoTracking()
        //                    .GroupBy(r => r.Status) // Nhóm theo status
        //                    .Select(g => new { Status = g.Key, Count = g.Count() })
        //                    .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

        //                int perf_trainCompleted = allTrainingStats.GetValueOrDefault(TrainingStatus.completed, 0);
        //                int perf_trainFailed = allTrainingStats.GetValueOrDefault(TrainingStatus.failed, 0);
        //                int perf_trainInProgress = allTrainingStats.GetValueOrDefault(TrainingStatus.in_progress, 0) +
        //                                           allTrainingStats.GetValueOrDefault(TrainingStatus.not_completed, 0);

        //                // 3. Lấy điểm trung bình của các khóa đã hoàn thành (đạt hoặc trượt)
        //                var avgScore = await _db.TrainingRecords
        //                    .AsNoTracking()
        //                    .Where(r => r.Status == TrainingStatus.completed || r.Status == TrainingStatus.failed)
        //                    .AverageAsync(r => (decimal?)r.Score, ct); // Dùng decimal? để xử lý nếu không có bản ghi nào

        //                decimal perf_trainAvgScore = Math.Round(avgScore ?? 0, 2);


        //        // --- Charts: last 12 months hires & quits ---
        //        // (Giữ nguyên logic này vì nó dựa trên Employee.HireDate và Employee.TerminationDate,
        //        // vốn là các sự kiện "vào" và "ra", không phải trạng thái "đang làm việc")
        //        var utcNow = DateTime.UtcNow;
        //        var startMonth = new DateTime(utcNow.Year, utcNow.Month, 1).AddMonths(-11);
        //        var months = Enumerable.Range(0, 12)
        //            .Select(i => startMonth.AddMonths(i))
        //            .ToList();

        //        var hiresRaw = await _db.Employees
        //            .AsNoTracking()
        //            .Where(e => e.HireDate != null)
        //            .Select(e => new { e.HireDate })
        //            .ToListAsync(ct);

        //        var quitsRaw = await _db.Contracts
        //            .AsNoTracking()
        //            // Giả sử 'terminated' là trạng thái cho biết nhân viên đã nghỉ
        //            .Where(c => c.Status == DeTaiNhanSu.Enums.ContractStatus.terminated && c.EndDate != null)
        //            .Select(c => new { TerminationDate = c.EndDate }) // Dùng EndDate làm ngày nghỉ
        //            .ToListAsync(ct);

        //        var labels = months.Select(m => m.ToString("MMM yyyy", CultureInfo.InvariantCulture)).ToList();
        //        var hires = new List<int>();
        //        var quits = new List<int>();

        //        foreach (var m in months)
        //        {
        //            var year = m.Year;
        //            var month = m.Month;

        //            var hireCount = hiresRaw.Count(x =>
        //            {
        //                if (x.HireDate is DateOnly d) return d.Year == year && d.Month == month;
        //                return false;
        //            });

        //            var quitCount = quitsRaw.Count(x =>
        //            {
        //                // 'TerminationDate' ở đây chính là 'c.EndDate' từ truy vấn Contracts ở trên
        //                if (x.TerminationDate is DateOnly d) return d.Year == year && d.Month == month;
        //                return false;
        //            });

        //            hires.Add(hireCount);
        //            quits.Add(quitCount);
        //        }

        //        // --- Employees by department --- (ĐÃ SỬA)
        //        // Sửa lại để chỉ đếm nhân viên "đang làm việc" VÀ khắc phục lỗi N+1

        //        //// 1. Lấy số lượng NV đang làm việc theo từng DepartmentId
        //        //var workingCountsByDept = await _db.Employees
        //        //    .AsNoTracking()
        //        //    .Where(e => workingEmployeeIds.Contains(e.Id)) // Chỉ lọc nhân viên "đang làm việc"
        //        //    .GroupBy(e => e.DepartmentId)
        //        //    .Select(g => new
        //        //    {
        //        //        DepartmentId = g.Key,
        //        //        Count = g.Count()
        //        //    })
        //        //    .ToDictionaryAsync(k => k.DepartmentId, v => v.Count, ct);

        //        var workingCountsByDept = await _db.Employees
        //            .AsNoTracking()
        //            .Where(e => workingEmployeeIds.Contains(e.Id))
        //            .GroupBy(e => e.DepartmentId)
        //            .Where(g => g.Key != null)
        //            .Select(g => new
        //            {
        //                DepartmentId = g.Key.Value,
        //                Count = g.Count()
        //            })
        //            .ToDictionaryAsync(k => k.DepartmentId, v => v.Count, ct);

        //        var allDepartments = await _db.Departments
        //            .AsNoTracking()
        //            .Select(d => new
        //            {
        //                departmentId = d.Id,
        //                departmentName = d.Name,
        //            })
        //            .ToListAsync(ct);

        //        // 2. Lấy tất cả phòng ban và map với số lượng đã đếm
        //        var employeesByDept = allDepartments
        //            .Select(d => new
        //            {
        //                d.departmentId,
        //                d.departmentName,
        //                // Dùng dictionary (workingCountsByDept) trong bộ nhớ
        //                count = workingCountsByDept.GetValueOrDefault(d.departmentId, 0)
        //            })
        //            .OrderByDescending(d => d.count) // Sắp xếp trong bộ nhớ (OK)
        //            .ToList();


        //        // --- Expiring contracts list (top 10 ordered by EndDate) ---
        //        var expiringList = await expiringQuery
        //            .OrderBy(c => c.EndDate)
        //            .Take(10)
        //            .Select(c => new
        //            {
        //                id = c.Id,
        //                employeeId = c.EmployeeId,
        //                employeeName = c.Employee.FullName,
        //                contractNumber = c.ContractNumber,
        //                endDate = c.EndDate,
        //                status = c.Status.ToString().ToLower()
        //            })
        //            .ToListAsync(ct);

        //        // --- Build payload ---
        //        var payload = new
        //        {
        //            summary = new
        //            {
        //                totalEmployees,
        //                totalDepartments,
        //                workingCount,
        //                contractsExpiring = expiringCount,
        //                contractsExpiringWithinDays = expiringWithinDays
        //            },
        //            charts = new
        //            {
        //                hiresQuits = new
        //                {
        //                    labels,
        //                    hires,
        //                    quits
        //                }
        //            },
        //            employeesByDepartment = employeesByDept,
        //            expiringContracts = new
        //            {
        //                meta = new { count = expiringCount },
        //                items = expiringList
        //            },
        //            attendanceToday = new
        //            {
        //                totalWorkingToday = totalWorkingToday, // Tổng NV có HĐ
        //                checkedIn = checkedInCount,            // Đã check-in (đúng giờ + trễ)
        //                onTime = onTimeCount + completedCount, // Đúng giờ (bao gồm cả đã checkout)
        //                late = lateCount,                      // Đi trễ
        //                onLeave = onLeaveCount,                // Nghỉ phép (đã duyệt)
        //                absent = absentCount,                  // Vắng (đã bị đánh dấu)
        //                notCheckedInYet = Math.Max(0, notCheckedInYetCount) // Chưa check-in
        //            },
        //            leaveStats = new
        //            {
        //                onLeaveToday = onLeaveCount, // (Dùng chung)
        //                pendingApproval = pendingLeaveRequests,
        //                approvedThisMonth = approvedLeaveThisMonth
        //            },
        //            disciplineStats = new
        //            {
        //                penaltiesThisMonth = penaltiesThisMonth,
        //                penaltiesToday = penaltiesToday
        //            },
        //            courseStats = new
        //            {
        //                total = totalCourses,
        //                newThisMonth = newCoursesThisMonth
        //            },
        //            salaryStats = new
        //            {
        //                lastFinalizedPeriod = lastSalaryPeriod, // (ví dụ: "2025-10")
        //                totalGross = totalGrossLastMonth,
        //                totalNet = totalNetLastMonth
        //            },
        //            performanceStats = new
        //            {
        //                attendanceThisMonth = new
        //                {
        //                    totalLate = perf_totalLate,
        //                    totalAbsent = perf_totalAbsent,
        //                    totalOnTime = perf_totalOnTime
        //                },
        //                trainingAllTime = new
        //                {
        //                    completed = perf_trainCompleted,
        //                    failed = perf_trainFailed,
        //                    inProgress = perf_trainInProgress,
        //                    averageScore = perf_trainAvgScore
        //                }
        //            }
        //        };

        //        return StatusCode(StatusCodes.Status200OK, new
        //        {
        //            statusCode = StatusCodes.Status200OK,
        //            message = "Lấy dữ liệu dashboard thành công.",
        //            data = new { result = payload },
        //            success = true
        //        });
        //    }
        //    catch (Exception ex) // Bắt lỗi cụ thể để debug
        //    {
        //        // Bạn nên log lỗi 'ex' ở đây
        //        return StatusCode(StatusCodes.Status500InternalServerError, new
        //        {
        //            statusCode = StatusCodes.Status500InternalServerError,
        //            message = $"Đã xảy ra lỗi khi lấy dashboard: {ex.Message}", // Thêm ex.Message để biết lỗi
        //            data = new { result = (object?)null },
        //            success = false
        //        });
        //    }
        //}

        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetDashboard([FromQuery] int expiringWithinDays = 30, CancellationToken ct = default)
        {
            try
            {
                // === 1. Chuẩn bị các biến dùng chung ===
                var todayDO = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                var utcNow = DateTime.UtcNow;
                var monthStart = new DateOnly(todayDO.Year, todayDO.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                if (expiringWithinDays < 1) expiringWithinDays = 30;

                // === 2. Lấy các dữ liệu gốc (dependencies) ===
                // Đây là các truy vấn quan trọng nhất, cần được thực thi trước và
                // truyền kết quả (List) vào các method con để tránh lỗi query.

                // 2.1. Lấy List ID của nhân viên "ĐANG LÀM VIỆC"
                var workingEmployeeIds = await GetWorkingEmployeeIdsAsync(todayDO, ct);
                int workingCount = workingEmployeeIds.Count;

                // 2.2. Lấy List ID của "TỔNG NHÂN VIÊN" (còn HĐ)
                var totalEmployeeIdsWithContracts = await GetTotalEmployeeIdsAsync(ct);
                int totalEmployees = totalEmployeeIdsWithContracts.Count;

                // 2.3. Định nghĩa IQueryable cho HĐ sắp hết hạn (chưa thực thi)
                var expiringQuery = BuildExpiringContractsQuery(todayDO, expiringWithinDays);

                // 3 + 4 thực thi các tác vụ thống kê và lấy kết quả
                int totalDepartments = await _db.Departments.AsNoTracking().CountAsync(ct);

                // tổng số hợp đồng sắp hết hạn
                int expiringCount = await expiringQuery.CountAsync(ct);

                // chấm công hôm nay
                var attendanceStats = await GetAttendanceTodayAsync(workingEmployeeIds, workingCount, todayDO, ct);

                // nghỉ phép trong tháng
                var leaveStats = await GetLeaveStatsAsync(monthStart, monthEnd, ct);

                // kỷ luật trong tháng
                var disciplineStats = await GetDisciplineStatsAsync(monthStart, todayDO, ct);

                // số khóa học
                var courseStats = await GetCourseStatsAsync(utcNow, ct);

                // lương phải trả cho kỳ gần nhất đang process hoặc locked
                var salaryStats = await GetSalaryStatsAsync(ct);

                // biểu đồ vuông thống kê chuyên cần theo trạng thái làm việc và điểm trung bình đào tạo tổng
                var performanceStats = await GetPerformanceStatsAsync(monthStart, todayDO, ct);

                // biến động nhân sự
                var hiresQuitsChart = await GetHiresQuitsChartAsync(utcNow, ct);

                // số nhân viên theo phòng ban
                var employeesByDept = await GetDepartmentChartAsync(workingEmployeeIds, ct);

                // hợp đồng sắp hết hạn
                var expiringList = await GetExpiringContractsListAsync(expiringQuery, ct);

                // hợp đồng mới hôm nay
                var newContractsQuery = _db.Contracts.AsNoTracking()
                    .Where(x => x.Status != ContractStatus.terminated);

                var newContractsList = await GetNewContractsListAsync(newContractsQuery, ct);

                // nhân viên chưa có hợp đồng
                var noContractQuery = _db.Employees.AsNoTracking()
                    .Where(e => e.Status == EmployeeStatus.active)
                    .Where(e => !_db.Contracts.Any(c => c.EmployeeId == e.Id));

                var noContractList = await GetNoContractEmployeesListAsync(noContractQuery, ct);


                // === 5. Xây dựng Payload (DTO) ===
                // Logic tính toán `notCheckedInYetCount` được đưa về đây
                int notCheckedInYetCount = workingCount -
                                           attendanceStats.CheckedInCount -
                                           attendanceStats.AbsentCount -
                                           attendanceStats.OnLeaveCount;

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
                        hiresQuits = hiresQuitsChart // Đã là DTO
                    },
                    employeesByDepartment = employeesByDept, // Đã là List DTO
                    expiringContracts = new
                    {
                        meta = new { count = expiringCount },
                        items = expiringList // Đã là List DTO
                    },
                    attendanceToday = new
                    {
                        totalWorkingToday = workingCount, // Sửa: Dùng workingCount đã tính
                        checkedIn = attendanceStats.CheckedInCount,
                        onTime = attendanceStats.OnTimeCount + attendanceStats.CompletedCount,
                        late = attendanceStats.LateCount,
                        onLeave = attendanceStats.OnLeaveCount,
                        absent = attendanceStats.AbsentCount,
                        notCheckedInYet = Math.Max(0, notCheckedInYetCount)
                    },
                    leaveStats = new
                    {
                        onLeaveToday = attendanceStats.OnLeaveCount, // Dùng chung kết quả
                        pendingApproval = leaveStats.PendingApproval,
                        approvedThisMonth = leaveStats.ApprovedThisMonth
                    },
                    disciplineStats = disciplineStats, // Đã là DTO
                    courseStats = courseStats,         // Đã là DTO
                    salaryStats = salaryStats,         // Đã là DTO
                    performanceStats = performanceStats, // Đã là DTO
                    newContractsList = newContractsList,
                    noContractList = noContractList
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Lấy dữ liệu dashboard thành công.",
                    data = new { result = payload },
                    success = true
                });
            }
            catch (Exception ex)
            {
                // Log lỗi 'ex' ở đây (ví dụ: _logger.LogError(ex, "Lỗi GetDashboard"))
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    statusCode = StatusCodes.Status500InternalServerError,
                    message = $"Đã xảy ra lỗi khi lấy dashboard: {ex.Message}",
                    data = new { result = (object?)null },
                    success = false
                });
            }
        }

        private record InternalAttendanceDto(int OnLeaveCount, int LateCount, int OnTimeCount, int CompletedCount, int AbsentCount, int CheckedInCount);
        private record InternalLeaveStatsDto(int PendingApproval, int ApprovedThisMonth);

        // lấy danh sách nhân viên có hợp đồng đang làm việc, thời gian trong hợp đồng còn hiệu lực
        private async Task<List<Guid>> GetWorkingEmployeeIdsAsync(DateOnly todayDO, CancellationToken ct)
        {
            // Thực thi ngay (ToListAsync) để trả về List ID
            return await _db.Contracts.AsNoTracking()
                .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated &&
                            c.StartDate <= todayDO &&
                            (c.EndDate == null || c.EndDate >= todayDO))
                .Select(c => c.EmployeeId)
                .Distinct()
                .ToListAsync(ct);
        }

        // lấy danh sách tổng tất cả nhân viên trừ các nhân viên đã chấm dứt terminated
        private async Task<List<Guid>> GetTotalEmployeeIdsAsync(CancellationToken ct)
        {
            // Thực thi ngay (ToListAsync) để trả về List ID
            return await _db.Contracts.AsNoTracking()
                .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated)
                .Select(c => c.EmployeeId)
                .Distinct()
                .ToListAsync(ct);
        }

        // lấy danh sách hợp đồng sắp hết hạn
        private IQueryable<Contract> BuildExpiringContractsQuery(DateOnly todayDO, int expiringWithinDays)
        {
            var untilDO = todayDO.AddDays(expiringWithinDays);

            // Trả về IQueryable, chưa thực thi
            return _db.Contracts.AsNoTracking()
                .Include(c => c.Employee)
                .Where(c => c.Status != DeTaiNhanSu.Enums.ContractStatus.terminated &&
                            c.EndDate != null &&
                            c.EndDate >= todayDO &&
                            c.EndDate <= untilDO);
        }

        // --- Các method thống kê (chạy song song) ---

        // lấy danh sách chấm công hôm nay
        private async Task<AttendanceTodayDto> GetAttendanceTodayAsync(List<Guid> workingEmployeeIds, int workingCount, DateOnly todayDO, CancellationToken ct)
        {
            // 1. Đang nghỉ phép (dùng list ID đã lấy)
            // Dùng Intersect với list ID trong bộ nhớ (hiệu quả)
            var onLeaveWorkingIds = await _db.Requests.AsNoTracking()
                .Where(r => r.Status == RequestStatus.approved &&
                            r.Category == RequestCategory.leave &&
                            r.FromDate.HasValue && r.FromDate.Value <= todayDO &&
                            r.ToDate.HasValue && r.ToDate.Value >= todayDO)
                .Select(r => r.EmployeeId)
                .Intersect(workingEmployeeIds) // Giao với list NV đang làm việc
                .ToListAsync(ct);

            int onLeaveCount = onLeaveWorkingIds.Count;

            // 2. Trạng thái chấm công
            var todayAttendanceStats = await _db.Attendances.AsNoTracking()
                .Where(a => a.Date == todayDO)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

            // 3. Tính toán
            int lateCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.late, 0);
            int onTimeCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.present, 0);
            int completedCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.completed, 0);
            int absentCount = todayAttendanceStats.GetValueOrDefault(AttendanceStatus.absent, 0);
            int checkedInCount = onTimeCount + lateCount + completedCount;

            return new AttendanceTodayDto(onLeaveCount, lateCount, onTimeCount, completedCount, absentCount, checkedInCount);
        }

        private async Task<LeaveStatsDto> GetLeaveStatsAsync(DateOnly monthStart, DateOnly monthEnd, CancellationToken ct)
        {
            var pendingCount = await _db.Requests.AsNoTracking()
                .CountAsync(r => r.Category == RequestCategory.leave &&
                         r.Status == RequestStatus.pending, ct);

            var approvedCount = await _db.Requests.AsNoTracking()
                .CountAsync(r => r.Category == RequestCategory.leave &&
                                 r.Status == RequestStatus.approved &&
                                 r.FromDate.HasValue &&
                                 r.FromDate.Value >= monthStart &&
                                 r.FromDate.Value <= monthEnd, ct);

            return new LeaveStatsDto(pendingCount, approvedCount);
        }

        private async Task<DisciplineStatsDto> GetDisciplineStatsAsync(DateOnly monthStart, DateOnly todayDO, CancellationToken ct)
        {
            var penaltyStats = await _db.RewardPenalties
                .AsNoTracking()
                .Where(x => x.Type.Type == RewardPenaltyKind.penalty &&
                            x.DecidedAt >= monthStart &&
                            x.DecidedAt <= todayDO)
                .GroupBy(x => 1)
                .Select(g => new
                {
                    ThisMonth = g.Count(),
                    Today = g.Count(x => x.DecidedAt == todayDO)
                })
                .FirstOrDefaultAsync(ct);

            return new DisciplineStatsDto(penaltyStats?.ThisMonth ?? 0, penaltyStats?.Today ?? 0);
        }

        private async Task<CourseStatsDto> GetCourseStatsAsync(DateTime utcNowForCourses, CancellationToken ct)
        {
            var total = await _db.Courses.AsNoTracking().CountAsync(ct);

            var newThisMonth = await _db.Courses.AsNoTracking()
                .CountAsync(c => c.CreatedAt.Year == utcNowForCourses.Year &&
                         c.CreatedAt.Month == utcNowForCourses.Month, ct);

            return new CourseStatsDto(total, newThisMonth);
        }

        private async Task<SalaryStatsDto> GetSalaryStatsAsync(CancellationToken ct)
        {
            var lastFinalizedRun = await _db.PayrollRuns
                .AsNoTracking()
                .Where(pr => pr.Status == PayrollRunStatus.processed || pr.Status == PayrollRunStatus.locked)
                .OrderByDescending(pr => pr.Period)
                .FirstOrDefaultAsync(ct);

            if (lastFinalizedRun == null)
            {
                return new SalaryStatsDto(null, 0, 0);
            }

            var salaryStats = await _db.Salaries
                .AsNoTracking()
                .Where(s => s.PayrollRunId == lastFinalizedRun.Id)
                .GroupBy(s => 1)
                .Select(g => new
                {
                    TotalGross = g.Sum(s => s.Gross),
                    TotalNet = g.Sum(s => s.Net)
                })
                .FirstOrDefaultAsync(ct);

            return new SalaryStatsDto(
                lastFinalizedRun.Period,
                salaryStats?.TotalGross ?? 0,
                salaryStats?.TotalNet ?? 0
            );
        }

        //private async Task<PerformanceStatsDto> GetPerformanceStatsAsync(DateOnly monthStart, DateOnly todayDO, CancellationToken ct)
        //{
        //    // 1. Thống kê chuyên cần
        //    var monthAttendanceStats = await _db.Attendances
        //        .AsNoTracking()
        //        .Where(a => a.Date >= monthStart && a.Date <= todayDO)
        //        .GroupBy(a => a.Status)
        //        .Select(g => new { Status = g.Key, Count = g.Count() })
        //        .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

        //    // 2. Thống kê đào tạo
        //    var allTrainingStats = await _db.TrainingRecords
        //        .AsNoTracking()
        //        .GroupBy(r => r.Status)
        //        .Select(g => new { Status = g.Key, Count = g.Count() })
        //        .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

        //    // 3. Điểm trung bình
        //    var avgScore = await _db.TrainingRecords
        //            .AsNoTracking()
        //            .Where(r => r.Status == TrainingStatus.completed || r.Status == TrainingStatus.failed)
        //            .AverageAsync(r => (decimal?)r.Score, ct);

        //    // Xử lý kết quả chuyên cần
        //    int perf_totalLate = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.late, 0);
        //    int perf_totalAbsent = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.absent, 0);
        //    int perf_totalOnTime = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.present, 0) +
        //                           monthAttendanceStats.GetValueOrDefault(AttendanceStatus.completed, 0);

        //    // Xử lý kết quả đào tạo
        //    int perf_trainCompleted = allTrainingStats.GetValueOrDefault(TrainingStatus.completed, 0);
        //    int perf_trainFailed = allTrainingStats.GetValueOrDefault(TrainingStatus.failed, 0);
        //    int perf_trainInProgress = allTrainingStats.GetValueOrDefault(TrainingStatus.in_progress, 0) +
        //                               allTrainingStats.GetValueOrDefault(TrainingStatus.not_completed, 0);

        //    decimal perf_trainAvgScore = Math.Round(avgScore ?? 0, 2);

        //    // Build DTO
        //    var attendancePayload = new
        //    {
        //        totalLate = perf_totalLate,
        //        totalAbsent = perf_totalAbsent,
        //        totalOnTime = perf_totalOnTime
        //    };

        //    var trainingPayload = new
        //    {
        //        completed = perf_trainCompleted,
        //        failed = perf_trainFailed,
        //        inProgress = perf_trainInProgress,
        //        averageScore = perf_trainAvgScore
        //    };

        //    return new PerformanceStatsDto(attendancePayload, trainingPayload);
        //}

        private async Task<PerformanceStatsDto> GetPerformanceStatsAsync(DateOnly monthStart, DateOnly todayDO, CancellationToken ct)
        {
            // 1. Thống kê chuyên cần (Giữ nguyên logic query)
            var monthAttendanceStats = await _db.Attendances
                .AsNoTracking()
                .Where(a => a.Date >= monthStart && a.Date <= todayDO)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

            // đúng giờ 224 số lịch đúng giờ
            // tất cả 224

            // 2. Thống kê đào tạo (Giữ nguyên logic query)
            var allTrainingStats = await _db.TrainingRecords
                .AsNoTracking()
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Status, v => v.Count, ct);

            // 3. Điểm trung bình (Giữ nguyên logic query)
            var avgScore = await _db.TrainingRecords
                    .AsNoTracking()
                    .Where(r => r.Status == TrainingStatus.completed || r.Status == TrainingStatus.failed)
                    .AverageAsync(r => (decimal?)r.Score, ct);

            // --- XỬ LÝ KẾT QUẢ CHUYÊN CẦN (CẬP NHẬT) ---

            // trễ
            int perf_totalLate = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.late, 0);

            // vắng
            int perf_totalAbsent = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.absent, 0);

            // đúng giờ
            int perf_totalOnTime = monthAttendanceStats.GetValueOrDefault(AttendanceStatus.present, 0) +
                                   monthAttendanceStats.GetValueOrDefault(AttendanceStatus.completed, 0);

            // 224

            // Tính tổng số bản ghi (Tổng dung lượng tối đa hiện tại)
            int totalAttendanceRecords = perf_totalLate + perf_totalAbsent + perf_totalOnTime;

            // Tính tỷ lệ % (0 - 100). Lưu ý ép kiểu double để không bị mất phần thập phân khi chia
            double OnTimeRate = 0;
            if (totalAttendanceRecords > 0)
            {
                OnTimeRate = Math.Round(((double)perf_totalOnTime / totalAttendanceRecords) * 100, 2);
            }

            double LateTimeRate = 0;
            if (totalAttendanceRecords > 0)
            {
                LateTimeRate = Math.Round(((double)perf_totalLate / totalAttendanceRecords) * 100, 2);
            }

            double AbsentRate = 0;
            if (totalAttendanceRecords > 0)
            {
                AbsentRate = Math.Round(((double)perf_totalAbsent / totalAttendanceRecords) * 100, 2);
            }

            // --- XỬ LÝ KẾT QUẢ ĐÀO TẠO ---
            int perf_trainCompleted = allTrainingStats.GetValueOrDefault(TrainingStatus.completed, 0);
            int perf_trainFailed = allTrainingStats.GetValueOrDefault(TrainingStatus.failed, 0);
            int perf_trainInProgress = allTrainingStats.GetValueOrDefault(TrainingStatus.in_progress, 0) +
                                       allTrainingStats.GetValueOrDefault(TrainingStatus.not_completed, 0);

            decimal perf_trainAvgScore = Math.Round(avgScore ?? 0, 2);

            // Build DTO
            // Bạn cần cập nhật DTO để hứng thêm trường 'rate' hoặc 'score'
            var attendancePayload = new
            {
                totalLate = perf_totalLate,
                totalAbsent = perf_totalAbsent,
                totalOnTime = perf_totalOnTime,
                OnTimeRate = OnTimeRate,
                LateTimeRate = LateTimeRate,
                AbsentRate = AbsentRate,// Giá trị từ 0 đến 100
            };

            var trainingPayload = new
            {
                completed = perf_trainCompleted,
                failed = perf_trainFailed,
                inProgress = perf_trainInProgress,
                averageScore = perf_trainAvgScore
            };

            // Lưu ý: Đảm bảo constructor của PerformanceStatsDto đã được cập nhật để nhận structure mới
            return new PerformanceStatsDto(attendancePayload, trainingPayload);
        }

        private async Task<HiresQuitsChartDto> GetHiresQuitsChartAsync(DateTime utcNow, CancellationToken ct)
        {
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
                    .Where(c => c.Status == DeTaiNhanSu.Enums.ContractStatus.terminated && c.EndDate != null)
                    .Select(c => new { TerminationDate = c.EndDate })
                    .ToListAsync(ct);

            var labels = new List<string>();
            var hires = new List<int>();
            var quits = new List<int>();

            foreach (var m in months)
            {
                var year = m.Year;
                var month = m.Month;

                labels.Add(m.ToString("MMM yyyy", CultureInfo.InvariantCulture));

                var hireCount = hiresRaw.Count(x =>
                {
                    if (x.HireDate is DateOnly d) return d.Year == year && d.Month == month;
                    return false;
                });

                var quitCount = quitsRaw.Count(x =>
                {
                    if (x.TerminationDate is DateOnly d) return d.Year == year && d.Month == month;
                    return false;
                });

                hires.Add(hireCount);
                quits.Add(quitCount);
            }

            return new HiresQuitsChartDto(labels, hires, quits);
        }

        private async Task<List<DeptChartDto>> GetDepartmentChartAsync(List<Guid> workingEmployeeIds, CancellationToken ct)
        {
            // 1. Đếm NV "đang làm việc" theo DepartmentId
            var workingCountsByDept = await _db.Employees
                .AsNoTracking()
                .Where(e => workingEmployeeIds.Contains(e.Id)) // Dùng list ID (in-memory)
                .GroupBy(e => e.DepartmentId)
                .Where(g => g.Key != null)
                .Select(g => new
                {
                    DepartmentId = g.Key.Value,
                    Count = g.Count()
                })
                .ToDictionaryAsync(k => k.DepartmentId, v => v.Count, ct);

            // 2. Lấy tên các phòng ban
            var allDepartments = await _db.Departments
                .AsNoTracking()
                .Select(d => new
                {
                    DepartmentId = d.Id,
                    DepartmentName = d.Name,
                })
                .ToListAsync(ct);

            // 3. Map 2 list lại (trong bộ nhớ)
            var employeesByDept = allDepartments
                .Select(d => new DeptChartDto(
                    d.DepartmentId,
                    d.DepartmentName,
                    workingCountsByDept.GetValueOrDefault(d.DepartmentId, 0)
                ))
                .OrderByDescending(d => d.Count)
                .ToList();

            return employeesByDept;
        }

        private async Task<List<ExpiringContractDto>> GetExpiringContractsListAsync(IQueryable<Contract> expiringQuery, CancellationToken ct)
        {
            // Tái sử dụng IQueryable đã build
            return await expiringQuery
                .OrderBy(c => c.EndDate)
                .Take(10)
                .Select(c => new ExpiringContractDto(
                    c.Id,
                    c.EmployeeId,
                    c.Employee.FullName,
                    c.ContractNumber,
                    c.EndDate,
                    c.Status.ToString().ToLower()
                ))
                .ToListAsync(ct);
        }

        private async Task<List<NewContractDto>> GetNewContractsListAsync(IQueryable<Contract> newContractsQuery, CancellationToken ct)
        {
            // Lấy ngày hiện tại (00:00:00) để so sánh chính xác
            var today = DateTime.Now.Date;

            DateOnly dateOnlyValue = DateOnly.FromDateTime(today);

            // Tái sử dụng IQueryable đã build
            return await newContractsQuery
                .Where(c => c.StartDate == dateOnlyValue) // QUAN TRỌNG: Lọc theo ngày bắt đầu là hôm nay
                .OrderByDescending(c => c.StartDate)   // Mới nhất lên đầu (trong ngày)
                .Take(10)                              // Lấy 10 bản ghi (nếu có quá nhiều hợp đồng trong 1 ngày)
                .Select(c => new NewContractDto(
                    c.Id,
                    c.EmployeeId,
                    c.Employee.FullName,
                    c.ContractNumber,
                    c.StartDate,
                    c.Status.ToString().ToLower()
                ))
                .ToListAsync(ct);
        }

        private async Task<List<NoContractEmployeeDto>> GetNoContractEmployeesListAsync(IQueryable<Employee> employeeQuery, CancellationToken ct)
        {
            return await employeeQuery
            .OrderBy(e => e.HireDate) // Ưu tiên xử lý người vào làm lâu nhất trước
            .Take(10)
            .Select(e => new NoContractEmployeeDto(
                e.Id,
                e.Code,
                e.FullName,
                e.HireDate,
                e.Department != null ? e.Department.Name : "N/A"
            ))
            .ToListAsync(ct);
        }
    }
}
