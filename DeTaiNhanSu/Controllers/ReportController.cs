using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Dtos; // Import DTO bạn vừa tạo
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using ClosedXML.Excel; // Import thư viện Excel
using System.ComponentModel.DataAnnotations;
using DeTaiNhanSu.Dtos.Report;
using DeTaiNhanSu.Dtos.CourseDtoFol; // Cho [Display]

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ReportController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Xuất báo cáo danh sách nhân viên đang hoạt động (Excel)
        /// </summary>
        [HttpGet("employees-report")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> ExportEmployeeList(CancellationToken ct)
        {
            try
            {
                var (todayDO, _) = GetVnTime(); // Dùng lại helper nếu bạn có

                // 1. Lấy ID của nhân viên đang làm việc (logic từ DashboardController)
                var workingEmployeeIds = _db.Contracts.AsNoTracking()
                    .Where(c => c.Status != ContractStatus.terminated &&
                                c.StartDate <= todayDO &&
                                (c.EndDate == null || c.EndDate >= todayDO))
                    .Select(c => c.EmployeeId)
                    .Distinct();

                // 2. Truy vấn dữ liệu nhân viên và làm phẳng
                var dataForReport = await _db.Employees
                    .AsNoTracking()
                    .Where(e => workingEmployeeIds.Contains(e.Id))
                    .Include(e => e.Department)
                    .Include(e => e.Position)
                    .OrderBy(e => e.FullName)
                    .Select(e => new EmployeeReportDto
                    {
                        EmployeeCode = e.Code,
                        FullName = e.FullName,
                        Email = e.Email,
                        DepartmentName = e.Department != null ? e.Department.Name : "N/A",
                        PositionName = e.Position != null ? e.Position.Name : "N/A",
                        HireDate = e.HireDate,
                        ContractStatus = "Đang làm việc" // Vì ta đã lọc
                    })
                    .ToListAsync(ct);

                // 3. Tạo file Excel bằng ClosedXML
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Danh Sách Nhân Viên");

                    // Chèn dữ liệu từ DTO
                    // ClosedXML sẽ tự động đọc thuộc tính [Display] làm tiêu đề
                    worksheet.Cell(1, 1).InsertTable(dataForReport);

                    // Tự động điều chỉnh độ rộng cột
                    worksheet.Columns().AdjustToContents();

                    // 4. Lưu workbook vào MemoryStream
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        var fileName = $"DSNV_HoatDong_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";

                        // 5. Trả file về
                        return File(content, contentType, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                // Trả về lỗi nếu có
                return StatusCode(500, new { message = $"Lỗi khi xuất báo cáo: {ex.Message}" });
            }
        }

        private (DateOnly Date, TimeOnly Time) GetVnTime()
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
            return (DateOnly.FromDateTime(vnNow), TimeOnly.FromDateTime(vnNow));
        }

        // báo cáo chi tiết kết quả đào tạo
        [HttpGet("training-record-report")]
        [Authorize(Roles = "HR, Admin, Manager")]

        public async Task<IActionResult> ExportTrainingRecords(
            [FromQuery] Guid? courseId,
            [FromQuery] TrainingStatus? status,
            CancellationToken ct
            )
        {
            try
            {
                var query = _db.TrainingRecords
                    .AsNoTracking()
                    .Include(r => r.Employee)
                    .Include(r => r.Course)
                    .Include(r => r.EvaluatedByUser)
                    .AsQueryable();

                if (courseId.HasValue)
                {
                    query = query.Where(r => r.CourseId == courseId.Value);
                }

                if (status.HasValue)
                {
                    query = query.Where(r => r.Status == status.Value);
                }

                var data = await query
                    .OrderByDescending(r => r.Course.Name)
                    .ThenBy(r => r.Employee.FullName)
                    .Select(r => new TrainingRecordExcelDto
                    {
                        EmployeeCode = r.Employee.Code,
                        EmployeeName = r.Employee.FullName,
                        CourseName = r.Course.Name,
                        Score = r.Score,
                        Status = r.Status.ToString(),
                        EvaluatedBy = r.EvaluatedByUser != null ? r.EvaluatedByUser.UserName : "",
                        Note = r.EvaluationNote
                    })
                    .ToListAsync(ct);

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("KetQuaDaoTao");

                    worksheet.Cell(1, 1).InsertTable(data);

                    var statusColumn = worksheet.Column(5);
                    foreach (var cell in statusColumn.CellsUsed().Skip(1))
                    {
                        string val = cell.GetString();
                        if (val == TrainingStatus.completed.ToString())
                        {
                            cell.Style.Font.FontColor = XLColor.Green;
                        }
                        else if (val == TrainingStatus.failed.ToString())
                        {
                            cell.Style.Font.FontColor = XLColor.Red;
                        }else if (val == TrainingStatus.in_progress.ToString())
                        {
                            cell.Style.Font.FontColor = XLColor.Orange;
                        }
                    }

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var fileName = $"KetQuaDaoTao_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi xuất báo cáo." });
            }
        }

        // báo cáo tổng hợp theo khóa học
        [HttpGet("course-summary-report")]
        [Authorize(Roles = "HR, Admin, Manager")]

        public async Task<IActionResult> ExportCourseSummary(CancellationToken ct)
        {
            try
            {
                // 1. Group by Course và tính toán Aggregate
                var summaryData = await _db.TrainingRecords
                    .AsNoTracking()
                    .GroupBy(r => r.Course.Name) // Group theo tên khóa học
                    .Select(g => new CourseSummaryExcelDto
                    {
                        CourseName = g.Key,
                        TotalParticipants = g.Count(),
                        PassedCount = g.Count(r => r.Status == TrainingStatus.completed),
                        FailedCount = g.Count(r => r.Status == TrainingStatus.failed),

                        // InProgress bao gồm in_progress và not_completed
                        InProgressCount = g.Count(r => r.Status == TrainingStatus.in_progress || r.Status == TrainingStatus.not_completed),

                        // Tính điểm trung bình (chỉ tính những bản ghi có điểm)
                        AverageScore = Math.Round(g.Average(r => (decimal?)r.Score) ?? 0, 2)
                    })
                    .OrderByDescending(x => x.TotalParticipants)
                    .ToListAsync(ct);

                // 2. Tạo Excel
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("TongHopKhoaHoc");

                    // Chèn dữ liệu
                    var table = worksheet.Cell(1, 1).InsertTable(summaryData);

                    // Thêm biểu đồ thanh (Data Bar) cho cột Điểm trung bình (cột 6) để trực quan
                    var avgScoreRange = worksheet.Range(2, 6, summaryData.Count + 1, 6);
                    avgScoreRange.AddConditionalFormat().DataBar(XLColor.LightBlue);
                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var fileName = $"TongHopDaoTao_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi xuất báo cáo tổng hợp: {ex.Message}" });
            }
        }

        // báo cáo bảng điểm cá nhân nhân viên
        [HttpGet("personal-training-record-report/{employeeId}")]
        public async Task<IActionResult> ExportEmployeeTranscript(Guid employeeId, CancellationToken ct)
        {
            try
            {
                // 1. Lấy thông tin nhân viên (Header)
                var employee = await _db.Employees
                    .AsNoTracking()
                    .Include(e => e.Department)
                    .Include(e => e.Position) // Giả sử có Position
                    .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

                if (employee == null)
                    return NotFound(new { message = "Nhân viên không tồn tại." });


                // 2. Lấy danh sách điểm (Body)
                var records = await _db.TrainingRecords
                    .AsNoTracking()
                    .Where(r => r.EmployeeId == employeeId)
                    .Include(r => r.Course)
                    .Include(r => r.EvaluatedByUser)
                    .OrderByDescending(r => r.Course.CreatedAt) // Hoặc StartDate
                    .Select(r => new TranscriptItemDto
                    {
                        CourseName = r.Course.Name,
                        ClassCode = r.Course.ClassCode,
                        EndDate = _db.CourseResults
                            .Where(cr => cr.EmployeeId == r.EmployeeId && cr.CourseId == r.CourseId)
                            .OrderByDescending(cr => cr.AnsweredAt)
                            .Select(cr => (DateOnly?)DateOnly.FromDateTime(cr.AnsweredAt))
                            .FirstOrDefault(),
                        Score = r.Score,
                        Status = r.Status.ToString(),
                        EvaluatedBy = r.EvaluatedByUser != null ? r.EvaluatedByUser.UserName : ""
                    })
                    .ToListAsync(ct);

                // 3. Tạo Excel với Format đẹp (Header + Table)
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("BangDiemCaNhan");

                    // --- PHẦN A: TIÊU ĐỀ & THÔNG TIN CÁ NHÂN ---

                    // Dòng 1: Tiêu đề lớn
                    var titleRange = ws.Range("A1:G1");
                    titleRange.Merge().Value = "BẢNG KẾT QUẢ ĐÀO TẠO CÁ NHÂN";
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.FontSize = 16;
                    titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Dòng 3-6: Thông tin nhân viên
                    ws.Cell("A3").Value = "Họ và Tên:";
                    ws.Cell("B3").Value = employee.FullName;
                    ws.Cell("B3").Style.Font.Bold = true;

                    ws.Cell("A4").Value = "Mã Nhân Viên:";
                    ws.Cell("B4").Value = employee.Code;

                    ws.Cell("D3").Value = "Phòng Ban:";
                    ws.Cell("E3").Value = employee.Department?.Name ?? "N/A";

                    ws.Cell("D4").Value = "Chức Vụ:";
                    ws.Cell("E4").Value = employee.Position?.Name ?? "N/A"; // Nếu có

                    ws.Cell("A5").Value = "Ngày Xuất Báo Cáo:";
                    ws.Cell("B5").Value = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm");

                    // --- PHẦN B: BẢNG ĐIỂM ---

                    // Bắt đầu bảng từ dòng 7
                    int tableStartRow = 7;

                    if (records.Any())
                    {
                        // Chèn dữ liệu
                        var table = ws.Cell(tableStartRow, 1).InsertTable(records);

                        // Style cho Header bảng (Màu xanh nhạt)
                        table.Theme = XLTableTheme.TableStyleMedium2;

                        // Tô màu trạng thái (Cột F - Status)
                        var statusColumn = table.DataRange.Column(6); // Cột thứ 6 trong range dữ liệu
                        foreach (var cell in statusColumn.Cells())
                        {
                            string status = cell.GetString();
                            if (status == TrainingStatus.completed.ToString())
                            {
                                cell.Style.Font.FontColor = XLColor.Green;
                                cell.Style.Font.Bold = true;
                            }
                            else if (status == TrainingStatus.failed.ToString())
                            {
                                cell.Style.Font.FontColor = XLColor.Red;
                            }
                        }
                    }
                    else
                    {
                        ws.Cell(tableStartRow, 1).Value = "(Nhân viên chưa tham gia khóa đào tạo nào)";
                        ws.Cell(tableStartRow, 1).Style.Font.Italic = true;
                    }

                    // --- FORMATTING CHUNG ---
                    ws.Columns().AdjustToContents(); // Tự động giãn cột
                    ws.Column(1).Width = 30; // Cột Tên khóa học rộng hơn chút

                    // Xuất file
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var fileName = $"BangDiem_{employee.Code}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi xuất bảng điểm: {ex.Message}" });
            }
        }

        [HttpGet("attendance-report")]
        public async Task<IActionResult> ExportAttendanceDetail(
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] Guid? departmentId,
            [FromQuery] Guid? employeeId,
            CancellationToken ct)
        {
            try
            {
                // 1. Xử lý khoảng thời gian (Mặc định tháng hiện tại nếu không gửi)
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var start = fromDate ?? new DateOnly(today.Year, today.Month, 1);
                var end = toDate ?? start.AddMonths(1).AddDays(-1);

                // 2. Truy vấn dữ liệu
                var query = _db.Attendances
                    .AsNoTracking()
                    .Include(a => a.Employee)
                    .ThenInclude(e => e.Department) // Giả sử Employee có Department
                    .Where(a => a.Date >= start && a.Date <= end)
                    .AsQueryable();

                // Áp dụng bộ lọc
                if (departmentId.HasValue)
                    query = query.Where(a => a.Employee.DepartmentId == departmentId);

                if (employeeId.HasValue)
                    query = query.Where(a => a.EmployeeId == employeeId);

                // Lấy dữ liệu và map sang DTO
                var data = await query
                    .OrderBy(a => a.Date)
                    .ThenBy(a => a.Employee.FullName)
                    .Select(a => new AttendanceReportDto
                    {
                        Date = a.Date.ToString("dd/MM/yyyy"),
                        EmployeeCode = a.Employee.Code,
                        EmployeeName = a.Employee.FullName,
                        Department = a.Employee.Department != null ? a.Employee.Department.Name : "",

                        // Format TimeOnly sang chuỗi HH:mm
                        CheckIn = a.CheckIn.HasValue ? a.CheckIn.Value.ToString("HH:mm") : "Không",
                        CheckOut = a.CheckOut.HasValue ? a.CheckOut.Value.ToString("HH:mm") : "Không",

                        Status = a.Status.ToString(),
                        Note = a.Note
                    })
                    .ToListAsync(ct);

                // 3. Tạo Excel
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("ChamCongChiTiet");

                    // --- HEADER REPORT ---
                    var titleRange = ws.Range("A1:H1");
                    titleRange.Merge().Value = "BẢNG CHẤM CÔNG CHI TIẾT";
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.FontSize = 16;
                    titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell("A2").Value = $"Từ ngày: {start:dd/MM/yyyy} - Đến ngày: {end:dd/MM/yyyy}";
                    ws.Range("A2:H2").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell("A3").Value = $"Ngày xuất báo cáo: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";

                    // --- DATA TABLE ---
                    int headerRow = 5;

                    if (data.Any())
                    {
                        var table = ws.Cell(headerRow, 1).InsertTable(data);

                        // Định dạng bảng
                        table.Theme = XLTableTheme.TableStyleLight9;

                        // --- TÔ MÀU TRẠNG THÁI (Conditional Formatting) ---
                        // Cột G (cột thứ 7) là Status
                        var statusColumn = table.DataRange.Column(7);
                        foreach (var cell in statusColumn.Cells())
                        {
                            string status = cell.GetString();

                            // Dựa trên Enum AttendanceStatus của bạn
                            if (status == AttendanceStatus.late.ToString())
                            {
                                cell.Style.Font.FontColor = XLColor.Red; // Chữ đỏ
                                cell.Style.Font.Bold = true;
                            }
                            else if (status == AttendanceStatus.absent.ToString())
                            {
                                cell.Style.Font.FontColor = XLColor.Orange;
                            }
                            else if (status == AttendanceStatus.leave.ToString())
                            {
                                cell.Style.Font.FontColor = XLColor.Green;
                            }
                            else if (status == AttendanceStatus.present.ToString() || status == AttendanceStatus.completed.ToString())
                            {
                                cell.Style.Font.FontColor = XLColor.Blue;
                            }
                        }

                        // Căn giữa cột Giờ vào/ra (Cột E, F)
                        table.DataRange.Column(5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        table.DataRange.Column(6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                    else
                    {
                        ws.Cell(headerRow, 1).Value = "Không có dữ liệu chấm công trong khoảng thời gian này.";
                        ws.Cell(headerRow, 1).Style.Font.Italic = true;
                    }

                    // Auto-fit
                    ws.Columns().AdjustToContents();

                    // Xuất file
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var fileName = $"ChamCong_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx";
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi xuất báo cáo: {ex.Message}" });
            }
        }

        [HttpGet("salaries-table-report")]
        public async Task<IActionResult> ExportRewardPenalty(
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] RewardPenaltyKind? kind, // Có thể lọc riêng Kỷ luật hoặc Khen thưởng
            [FromQuery] Guid? departmentId,
            CancellationToken ct)
        {
            try
            {
                // 1. Xử lý ngày tháng
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var start = fromDate ?? new DateOnly(today.Year, today.Month, 1);
                var end = toDate ?? start.AddMonths(1).AddDays(-1);

                // 2. Truy vấn dữ liệu
                var query = _db.RewardPenalties
                    .AsNoTracking()
                    .Include(rp => rp.Employee)
                    .ThenInclude(e => e.Department)
                    .Include(rp => rp.Type)
                    .Where(rp => rp.DecidedAt >= start && rp.DecidedAt <= end)
                    .AsQueryable();

                // Áp dụng bộ lọc
                if (kind.HasValue)
                    query = query.Where(rp => rp.Type.Type == kind.Value);

                if (departmentId.HasValue)
                    query = query.Where(rp => rp.Employee.DepartmentId == departmentId);

                // Projection sang DTO
                var data = await query
                    .OrderByDescending(rp => rp.DecidedAt)
                    .ThenBy(rp => rp.Employee.FullName)
                    .Select(rp => new RewardPenaltyReportDto
                    {
                        DecidedAt = rp.DecidedAt.ToString("dd/MM/yyyy"),
                        EmployeeCode = rp.Employee.Code,
                        EmployeeName = rp.Employee.FullName,
                        Department = rp.Employee.Department != null ? rp.Employee.Department.Name : "",

                        // Chuyển Enum sang tiếng Việt
                        Kind = rp.Type.Type == RewardPenaltyKind.reward ? "Khen thưởng" : "Kỷ luật",
                        TypeName = rp.Type.Name,

                        // Ưu tiên AmountOverride, nếu null thì lấy DefaultAmount
                        Amount = rp.AmountOverride ?? rp.Type.DefaultAmount ?? 0,

                        Reason = rp.CustomReason ?? ""
                    })
                    .ToListAsync(ct);

                // 3. Tạo Excel
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("KhenThuongKyLuat");

                    // --- HEADER ---
                    var titleRange = ws.Range("A1:H1");
                    // Đổi tiêu đề tùy theo bộ lọc
                    string reportTitle = "BÁO CÁO KHEN THƯỞNG & KỶ LUẬT";
                    if (kind == RewardPenaltyKind.penalty) reportTitle = "BÁO CÁO VI PHẠM & KỶ LUẬT";
                    if (kind == RewardPenaltyKind.reward) reportTitle = "BÁO CÁO KHEN THƯỞNG";

                    titleRange.Merge().Value = reportTitle;
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.FontSize = 16;
                    titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell("A2").Value = $"Thời gian: {start:dd/MM/yyyy} - {end:dd/MM/yyyy}";
                    ws.Range("A2:H2").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // --- TABLE ---
                    int headerRow = 4;

                    if (data.Any())
                    {
                        var table = ws.Cell(headerRow, 1).InsertTable(data);
                        table.Theme = XLTableTheme.TableStyleLight10;

                        // Format cột Tiền (Cột G - thứ 7)
                        table.DataRange.Column(7).Style.NumberFormat.Format = "#,##0";

                        // --- TÔ MÀU DỰA TRÊN LOẠI (Khen/Phạt) ---
                        // Cột E (thứ 5) là cột "Loại" (Kind)
                        var kindColumn = table.DataRange.Column(5);
                        foreach (var cell in kindColumn.Cells())
                        {
                            string val = cell.GetString();
                            var row = cell.WorksheetRow(); // Lấy cả dòng

                            if (val == "Kỷ luật")
                            {
                                // Kỷ luật: Chữ Đỏ
                                row.Cells(1, 8).Style.Font.FontColor = XLColor.Red;
                            }
                            else if (val == "Khen thưởng")
                            {
                                // Khen thưởng: Chữ Xanh
                                row.Cells(1, 8).Style.Font.FontColor = XLColor.Green;
                            }
                        }
                    }
                    else
                    {
                        ws.Cell(headerRow, 1).Value = "Không có dữ liệu trong khoảng thời gian này.";
                        ws.Cell(headerRow, 1).Style.Font.Italic = true;
                    }

                    ws.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var fileName = $"KTKL_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx";
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi xuất báo cáo: {ex.Message}" });
            }
        }

        [HttpGet("export-summary")]
        public async Task<IActionResult> ExportSalarySummary(Guid payrollRunId, CancellationToken ct)
        {
            try
            {
                // 1. Kiểm tra kỳ lương
                var payrollRun = await _db.PayrollRuns
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pr => pr.Id == payrollRunId, ct);

                if (payrollRun == null)
                    return NotFound(new { message = "Kỳ lương không tồn tại." });

                // 2. Lấy dữ liệu lương + chi tiết items
                var salaries = await _db.Salaries
                    .AsNoTracking()
                    .Where(s => s.PayrollRunId == payrollRunId)
                    .Include(s => s.Employee)
                    .ThenInclude(e => e.Department)
                    .Include(s => s.Items)
                    .OrderBy(s => s.Employee.Code)
                    .ToListAsync(ct);

                if (!salaries.Any())
                    return BadRequest(new { message = "Kỳ lương này chưa có dữ liệu lương." });

                // 3. Chuẩn bị dữ liệu Pivot
                // Lấy tất cả tên các khoản mục xuất hiện trong kỳ này dựa trên cột 'Note'
                var allItemNames = salaries
                    .SelectMany(s => s.Items)
                    // Group theo Type và Note (thay vì Name)
                    .GroupBy(i => new { i.Type, i.Note })
                    .Select(g => new {
                        Type = g.Key.Type,
                        // Dùng Note làm tên cột. Nếu Note null thì lấy tên Type
                        Name = !string.IsNullOrWhiteSpace(g.Key.Note) ? g.Key.Note : g.Key.Type.ToString()
                    })
                    .Distinct()
                    .ToList();

                // Phân loại dựa trên enum thực tế của bạn
                var incomeTypes = new[] { SalaryItemType.basic, SalaryItemType.allowance, SalaryItemType.bonus, SalaryItemType.ot };
                var deductionTypes = new[] { SalaryItemType.deduction, SalaryItemType.insurance, SalaryItemType.tax };

                var incomeColumns = allItemNames
                    .Where(x => incomeTypes.Contains(x.Type))
                    .OrderBy(x => x.Name)
                    .ToList();

                var deductionColumns = allItemNames
                    .Where(x => deductionTypes.Contains(x.Type))
                    .OrderBy(x => x.Name)
                    .ToList();

                // 4. Map sang DTO
                var reportData = salaries.Select(s => {
                    var dto = new SalaryReportRowDto
                    {
                        EmployeeCode = s.Employee.Code,
                        EmployeeName = s.Employee.FullName,
                        Department = s.Employee.Department?.Name ?? "",
                        GrossSalary = s.Gross,
                        NetSalary = s.Net,
                        // Tính tổng các khoản trừ dựa trên nhóm deductionTypes
                        TotalDeductions = s.Items.Where(i => deductionTypes.Contains(i.Type)).Sum(i => i.Amount)
                    };

                    // Điền các khoản mục động vào Dictionary
                    foreach (var item in s.Items)
                    {
                        // Key là Note (hoặc Type nếu Note null)
                        string key = !string.IsNullOrWhiteSpace(item.Note) ? item.Note : item.Type.ToString();

                        if (dto.DynamicItems.ContainsKey(key))
                            dto.DynamicItems[key] += item.Amount;
                        else
                            dto.DynamicItems[key] = item.Amount;
                    }
                    return dto;
                }).ToList();


                // 5. Tạo Excel
                // Sử dụng using statement gọn hơn (C# 8.0+)
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("BangLuongTong");

                // --- HEADER ---
                ws.Cell("A1").Value = $"BẢNG THANH TOÁN LƯƠNG - KỲ: {payrollRun.Period}";
                ws.Range("A1:H1").Merge().Style.Font.Bold = true;
                ws.Range("A1:H1").Style.Font.FontSize = 16;

                // --- TABLE HEADER (Dòng 3) ---
                int col = 1;
                ws.Cell(3, col++).Value = "Mã NV";
                ws.Cell(3, col++).Value = "Họ Tên";
                ws.Cell(3, col++).Value = "Phòng Ban";

                // Cột Động: Thu Nhập
                foreach (var inc in incomeColumns)
                {
                    ws.Cell(3, col).Value = inc.Name;
                    ws.Cell(3, col).Style.Fill.BackgroundColor = XLColor.LightGreen;
                    col++;
                }

                ws.Cell(3, col++).Value = "TỔNG THU NHẬP (GROSS)";

                // Cột Động: Khấu Trừ
                foreach (var ded in deductionColumns)
                {
                    ws.Cell(3, col).Value = ded.Name;
                    ws.Cell(3, col).Style.Fill.BackgroundColor = XLColor.LightSalmon;
                    col++;
                }

                ws.Cell(3, col++).Value = "THỰC LĨNH (NET)";

                var headerRange = ws.Range(3, 1, 3, col - 1);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                // --- TABLE BODY ---
                int row = 4;
                foreach (var item in reportData)
                {
                    int c = 1;
                    ws.Cell(row, c++).Value = item.EmployeeCode;
                    ws.Cell(row, c++).Value = item.EmployeeName;
                    ws.Cell(row, c++).Value = item.Department;

                    // Fill Income Columns
                    foreach (var inc in incomeColumns)
                    {
                        decimal val = item.DynamicItems.ContainsKey(inc.Name) ? item.DynamicItems[inc.Name] : 0;
                        ws.Cell(row, c++).Value = val;
                    }

                    // Total Gross
                    ws.Cell(row, c).Value = item.GrossSalary;
                    ws.Cell(row, c).Style.Font.Bold = true;
                    c++;

                    // Fill Deduction Columns
                    foreach (var ded in deductionColumns)
                    {
                        decimal val = item.DynamicItems.ContainsKey(ded.Name) ? item.DynamicItems[ded.Name] : 0;
                        ws.Cell(row, c++).Value = val;
                    }

                    // Net Salary
                    ws.Cell(row, c).Value = item.NetSalary;
                    ws.Cell(row, c).Style.Font.Bold = true;
                    ws.Cell(row, c).Style.Font.FontColor = XLColor.Blue;

                    row++;
                }

                // Format tiền tệ cho các cột số liệu
                var moneyRange = ws.Range(4, 4, row - 1, col - 1);
                moneyRange.Style.NumberFormat.Format = "#,##0";

                ws.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var fileName = $"BangLuong_{payrollRun.Period}_{DateTime.UtcNow:yyyyMMdd}.xlsx";

                // Reset stream position về đầu để file không bị lỗi 0 byte khi tải về
                stream.Position = 0;

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                // Log lỗi chi tiết (nếu có logger) ở đây: _logger.LogError(ex, "Lỗi xuất báo cáo lương");
                return StatusCode(500, new { message = $"Lỗi xuất báo cáo lương: {ex.Message}" });
            }
        }
    }
}