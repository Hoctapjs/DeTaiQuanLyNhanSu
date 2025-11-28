using System.Security.Claims;
using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.WorkScheduleDtoFol;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Scope;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkScheduleController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IDataScopeService _dataScope;

        public WorkScheduleController(AppDbContext db, IDataScopeService dataScope)
        {
            _db = db;
            _dataScope = dataScope;
        }

        // ========= GET: /api/workschedule?employeeId=&from=&to=&current=&pageSize=
        // ========= GET: /api/workschedule
        [HttpGet]
        [Authorize(Roles = "HR, Admin, Manager, Employee")]
        public async Task<IActionResult> Search(
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? departmentId,
            [FromQuery] Guid? shiftTemplateId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = "Date",
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.WorkSchedules.AsNoTracking()
                    .Include(x => x.Employee)
                    .Include(x => x.ShiftTemplate) // [QUAN TRỌNG]: Include bảng mẫu ca
                    .AsQueryable();

                // bộ lọc chung chung cho role Manager
                var filterDeptId = await _dataScope.GetAllowedDepartmentIdAsync(departmentId, ct);

                if (!User.IsInRole("Admin") && !User.IsInRole("HR") && !User.IsInRole("Manager"))
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (Guid.TryParse(userIdStr, out var uid))
                    {
                        // Lấy EmployeeId của user đang đăng nhập
                        var myInfo = await _db.Users.AsNoTracking()
                            .Where(u => u.Id == uid)
                            .Select(u => new { u.EmployeeId })
                            .FirstOrDefaultAsync(ct);

                        if (myInfo != null && myInfo.EmployeeId != Guid.Empty)
                        {
                            // BẮT BUỘC lọc theo EmployeeId của chính họ
                            query = query.Where(x => x.EmployeeId == myInfo.EmployeeId);

                            // (Tùy chọn) Ghi đè tham số employeeId để logic bên dưới không bị conflict (dù query đã filter rồi)
                            employeeId = myInfo.EmployeeId;
                        }
                        else
                        {
                            // Trường hợp User đăng nhập nhưng chưa liên kết với Employee -> Trả về rỗng
                            return this.OKSingle(new { meta = new { total = 0 }, result = new List<object>() }, "Tài khoản chưa liên kết hồ sơ nhân viên.");
                        }
                    }
                }

                // --- Filtering ---
                if (employeeId is not null)
                    query = query.Where(x => x.EmployeeId == employeeId);


                // bộ lọc chung chung cho role Manager kiểm tra
                if (filterDeptId.HasValue)
                {
                    query = query.Where(x => x.Employee.DepartmentId == filterDeptId.Value);
                }


                // 3. Lọc theo Ca làm việc
                if (shiftTemplateId.HasValue)
                    query = query.Where(x => x.ShiftTemplateId == shiftTemplateId);

                if (from is not null)
                    query = query.Where(x => x.Date >= from);
                if (to is not null)
                    query = query.Where(x => x.Date <= to);

                // --- Sorting ---
                // Sắp xếp theo Ngày, sau đó đến Giờ bắt đầu của ca (từ ShiftTemplate)
                query = sort?.Trim() switch
                {
                    "-Date" => query.OrderByDescending(x => x.Date).ThenBy(x => x.ShiftTemplate.StartTime),
                    "EmployeeId" => query.OrderBy(x => x.EmployeeId).ThenBy(x => x.Date),
                    _ => query.OrderBy(x => x.Date).ThenBy(x => x.ShiftTemplate.StartTime)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new WorkScheduleDto
                    {
                        Id = x.Id,
                        EmployeeId = x.EmployeeId,
                        EmployeeFullName = x.Employee.FullName,
                        Date = x.Date,

                        //  Mapping dữ liệu từ ShiftTemplate sang DTO
                        ShiftTemplateId = x.ShiftTemplateId,
                        ShiftName = x.ShiftTemplate.Name,       // VD: "Ca Hành Chính"
                        ShiftStartTime = x.ShiftTemplate.StartTime,
                        ShiftEndTime = x.ShiftTemplate.EndTime,
                        TotalWorkingHours = x.ShiftTemplate.TotalWorkingHours,

                        Note = x.Note,

                        // quy đổi công làm
                        WorkDay = (double)(x.ShiftTemplate.TotalWorkingHours / 8)
                    })
                    .ToListAsync(ct);

                var meta = new { current, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize), total };
                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Tìm thấy {total} lịch làm việc." : "Không có kết quả.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm lịch làm việc."); }
        }

        // ========= GET: /api/workschedule/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var allowedDeptId = await _dataScope.GetAllowedDepartmentIdAsync(null, ct);

                var query = _db.WorkSchedules
                    .AsNoTracking()
                    .Include(x => x.Employee) 
                    .Include(x => x.ShiftTemplate)
                    .AsQueryable();

                if (allowedDeptId.HasValue)
                {
                    query = query.Where(c => c.Employee.DepartmentId == allowedDeptId.Value);
                }

                var ws = await query
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (ws is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy lịch làm việc.");

                var dto = new WorkScheduleDto
                {
                    Id = ws.Id,
                    EmployeeId = ws.EmployeeId,
                    EmployeeFullName = ws.Employee.FullName,
                    Date = ws.Date,

                    // [MỚI]: Mapping dữ liệu từ ShiftTemplate
                    ShiftTemplateId = ws.ShiftTemplateId,
                    ShiftName = ws.ShiftTemplate.Name,
                    ShiftStartTime = ws.ShiftTemplate.StartTime,
                    ShiftEndTime = ws.ShiftTemplate.EndTime,

                    Note = ws.Note
                };
                return this.OKSingle(dto, "Lấy thông tin thành công.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy thông tin."); }
        }

        // ========= POST: /api/workschedule
        [HttpPost]
        [Authorize(Roles = "HR, Admin, Manager")] // Manager có thể tự tạo schedule cho team
        public async Task<IActionResult> Create([FromBody] CreateWorkScheduleRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null) return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                // Xác thực khóa ngoại nhân viên
                if (!await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct))
                    return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                // Lấy thông tin ca làm việc từ ShiftTemplate
                var shift = await _db.ShiftTemplates.AsNoTracking().FirstOrDefaultAsync(s => s.Id == req.ShiftTemplateId, ct);
                if (shift == null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Mẫu ca làm việc (ShiftTemplate) không tồn tại.");

                // Validate Business Rule: Check trùng ca
                var dup = await _db.WorkSchedules
                    .AnyAsync(w => w.EmployeeId == req.EmployeeId && w.Date == req.Date, ct);
                if (dup)
                    return this.FAIL(StatusCodes.Status409Conflict, $"Nhân viên đã có lịch làm việc ngày {req.Date}.");

                var entity = new WorkSchedule
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    Date = req.Date,
                    ShiftTemplateId = req.ShiftTemplateId, // Gán ID ca
                    Note = req.Note
                    // StartTime/EndTime được lấy từ ShiftTemplate khi truy vấn
                };

                _db.WorkSchedules.Add(entity);
                await _db.SaveChangesAsync(ct);

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo lịch làm việc thành công.",
                    data = new[] { new { entity.Id } },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo lịch làm việc do xung đột dữ liệu.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo lịch làm việc."); }
        }

        // ========= PUT (partial): /api/workschedule/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkScheduleRequest req, CancellationToken ct)
        {
            try
            {
                var ws = await _db.WorkSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (ws is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy lịch làm việc.");

                // --- Cập nhật trường nếu có ---
                if (req.Note is not null) ws.Note = req.Note;

                // [LOGIC MỚI] Cập nhật ShiftTemplateId
                if (req.ShiftTemplateId.HasValue)
                {
                    // 1. Validate ShiftTemplateId có tồn tại không
                    var shiftExists = await _db.ShiftTemplates.AnyAsync(s => s.Id == req.ShiftTemplateId.Value, ct);
                    if (!shiftExists)
                        return this.FAIL(StatusCodes.Status404NotFound, "Mẫu ca làm việc không tồn tại.");

                    ws.ShiftTemplateId = req.ShiftTemplateId.Value;
                }

                // [BUSINESS RULE] Kiểm tra trùng lịch nếu thay đổi EmployeeId HOẶC Date
                // (Logic trùng lặp phức tạp nhất, cần kiểm tra 3 trường hợp)

                Guid newEmployeeId = req.EmployeeId ?? ws.EmployeeId;
                DateOnly newDate = req.Date ?? ws.Date;

                // 1. Kiểm tra tồn tại Employee mới nếu ID được gửi
                if (req.EmployeeId.HasValue && req.EmployeeId.Value != ws.EmployeeId)
                {
                    if (!await _db.Employees.AnyAsync(e => e.Id == newEmployeeId, ct))
                        return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");
                }

                // 2. Kiểm tra xung đột lịch: Có bản ghi nào KHÁC ID hiện tại mà trùng EmployeeId và Date không?
                if ((req.EmployeeId.HasValue && req.EmployeeId.Value != ws.EmployeeId) || req.Date.HasValue)
                {
                    var dup = await _db.WorkSchedules
                        .AnyAsync(w =>
                            w.EmployeeId == newEmployeeId &&
                            w.Date == newDate &&
                            w.Id != id, ct);

                    if (dup)
                        return this.FAIL(StatusCodes.Status409Conflict, $"Nhân viên đã có lịch làm việc ngày {newDate}.");
                }

                // 3. Gán các giá trị đã qua kiểm tra
                if (req.EmployeeId.HasValue) ws.EmployeeId = req.EmployeeId.Value;
                if (req.Date.HasValue) ws.Date = req.Date.Value;


                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Cập nhật lịch làm việc thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi cập nhật."); }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR, Admin")] // Giữ nguyên role hạn chế hơn cho xoá
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.WorkSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy bản ghi.");

                _db.WorkSchedules.Remove(e);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá lịch làm việc thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do ràng buộc dữ liệu.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá."); }
        }

        [HttpPost("bulk")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> BulkCreate([FromBody] BulkCreateWorkScheduleRequest req, CancellationToken ct)
        {
            try
            {
                // 1. Kiểm tra đầu vào
                if (req.FromDate > req.ToDate)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Ngày bắt đầu phải <= Ngày kết thúc.");

                // 2. Xác định danh sách nhân viên
                var targetEmployeeIds = new List<Guid>();

                if (req.EmployeeIds != null && req.EmployeeIds.Any())
                {
                    // Nếu truyền danh sách ID cụ thể
                    // (Nên validate xem các ID này có tồn tại không nếu cần kỹ)
                    targetEmployeeIds = req.EmployeeIds.Distinct().ToList();
                }
                else if (req.DepartmentId.HasValue)
                {
                    // Nếu chọn theo phòng ban -> Lấy tất cả NV đang hoạt động của phòng đó
                    targetEmployeeIds = await _db.Employees
                        .AsNoTracking()
                        .Where(e => e.DepartmentId == req.DepartmentId.Value && e.Status == EmployeeStatus.active)
                        .Select(e => e.Id)
                        .ToListAsync(ct);
                }
                else
                {
                    return this.FAIL(StatusCodes.Status400BadRequest, "Phải chọn Nhân viên hoặc Phòng ban.");
                }

                if (!targetEmployeeIds.Any())
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy nhân viên nào để xếp lịch.");

                // 3. Kiểm tra mẫu ca làm việc
                var shiftExists = await _db.ShiftTemplates.AnyAsync(s => s.Id == req.ShiftTemplateId, ct);
                if (!shiftExists)
                    return this.FAIL(StatusCodes.Status404NotFound, "Mẫu ca làm việc không tồn tại.");

                // 4. Lấy dữ liệu lịch ĐÃ CÓ trong khoảng thời gian này để tránh trùng lặp (hoặc để ghi đè)
                var existingSchedules = await _db.WorkSchedules
                    .Where(w => targetEmployeeIds.Contains(w.EmployeeId) && w.Date >= req.FromDate && w.Date <= req.ToDate)
                    .ToListAsync(ct);

                var newSchedules = new List<WorkSchedule>();
                var schedulesToDelete = new List<WorkSchedule>(); // Dùng cho mode Overwrite

                // 5. Duyệt qua từng ngày và từng nhân viên để tạo lịch
                for (var date = req.FromDate; date <= req.ToDate; date = date.AddDays(1))
                {
                    // Kiểm tra xem ngày này có nằm trong DaysOfWeek được chọn không?
                    // Nếu req.DaysOfWeek null hoặc rỗng -> Áp dụng tất cả các ngày
                    if (req.DaysOfWeek != null && req.DaysOfWeek.Any() && !req.DaysOfWeek.Contains(date.DayOfWeek))
                    {
                        continue; // Bỏ qua ngày này
                    }

                    foreach (var empId in targetEmployeeIds)
                    {
                        // Kiểm tra xem đã có lịch ngày này chưa
                        var existing = existingSchedules.FirstOrDefault(x => x.EmployeeId == empId && x.Date == date);

                        if (existing != null)
                        {
                            if (req.Overwrite)
                            {
                                // Nếu chọn Ghi đè -> Đưa vào danh sách xóa
                                if (!schedulesToDelete.Contains(existing))
                                    schedulesToDelete.Add(existing);
                            }
                            else
                            {
                                // Nếu không ghi đè -> Bỏ qua, giữ lịch cũ
                                continue;
                            }
                        }

                        // Tạo mới
                        newSchedules.Add(new WorkSchedule
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = empId,
                            Date = date,
                            ShiftTemplateId = req.ShiftTemplateId,
                            Note = req.Note // Có thể thêm "Batch job" vào note nếu muốn
                        });
                    }
                }

                if(!newSchedules.Any() && !schedulesToDelete.Any())
                {
                    // Trường hợp: Chọn ngày 20/11 (Thứ 5) nhưng DaysOfWeek chỉ chọn [Thứ 2, Thứ 3]
                    // -> Không có ngày nào khớp.
                    return this.OK(message: "Không có lịch làm việc nào được tạo (Ngày chọn không khớp với ngày trong tuần).");
                }

                // 7. Thực hiện Transaction (Nếu có dữ liệu thay đổi)
                using var transaction = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    if (schedulesToDelete.Any())
                    {
                        _db.WorkSchedules.RemoveRange(schedulesToDelete);
                    }

                    if (newSchedules.Any())
                    {
                        await _db.WorkSchedules.AddRangeAsync(newSchedules, ct);
                    }

                    await _db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = 201,
                    message = $"Đã xếp lịch thành công. Thêm mới: {newSchedules.Count}, Ghi đè: {schedulesToDelete.Count}.",
                    success = true
                });
            }
            catch (Exception ex)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, $"Lỗi xếp lịch hàng loạt: {ex.Message}");
            }
        }

        [HttpDelete("bulk")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteWorkScheduleRequest req, CancellationToken ct)
        {
            try
            {
                // 1. Validate Input
                if (req.FromDate > req.ToDate)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Ngày bắt đầu phải <= Ngày kết thúc.");

                if ((req.EmployeeIds == null || !req.EmployeeIds.Any()) && req.DepartmentId == null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Phải chọn danh sách Nhân viên hoặc Phòng ban để xóa.");

                // 2. Xác định danh sách ID nhân viên cần xóa lịch
                var targetEmployeeIds = new List<Guid>();

                if (req.EmployeeIds != null && req.EmployeeIds.Any())
                {
                    targetEmployeeIds = req.EmployeeIds;
                }
                else if (req.DepartmentId.HasValue)
                {
                    // Lấy tất cả nhân viên thuộc phòng ban
                    targetEmployeeIds = await _db.Employees
                        .AsNoTracking()
                        .Where(e => e.DepartmentId == req.DepartmentId.Value)
                        .Select(e => e.Id)
                        .ToListAsync(ct);
                }

                if (!targetEmployeeIds.Any())
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy nhân viên nào trong danh sách chọn.");

                // 3. Truy vấn các bản ghi cần xóa
                var schedulesToDelete = await _db.WorkSchedules
                    .Where(w => targetEmployeeIds.Contains(w.EmployeeId) &&
                                w.Date >= req.FromDate &&
                                w.Date <= req.ToDate)
                    .ToListAsync(ct);

                if (!schedulesToDelete.Any())
                    return this.OK(message: "Không có lịch làm việc nào trong khoảng thời gian này để xóa.");

                // 4. Thực hiện xóa
                _db.WorkSchedules.RemoveRange(schedulesToDelete);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: $"Đã xóa {schedulesToDelete.Count} bản ghi lịch làm việc.");
            }
            catch (Exception ex)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, $"Lỗi khi xóa lịch hàng loạt: {ex.Message}");
            }
        }

        [HttpGet("all")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? departmentId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken ct)
        {
            try
            {
                // 1. Validate: Bắt buộc phải có khoảng thời gian để tránh tải quá nặng
                if (from == null || to == null)
                {
                    // Mặc định lấy tháng hiện tại nếu không truyền
                    var now = DateTime.UtcNow;
                    from ??= new DateOnly(now.Year, now.Month, 1);
                    to ??= from.Value.AddMonths(1).AddDays(-1);
                }

                var query = _db.WorkSchedules.AsNoTracking()
                    .Include(x => x.Employee)
                    .Include(x => x.ShiftTemplate) // Quan trọng: Include ShiftTemplate
                    .AsQueryable();

                // 2. Filtering
                if (employeeId.HasValue)
                    query = query.Where(x => x.EmployeeId == employeeId);

                if (departmentId.HasValue)
                    query = query.Where(x => x.Employee.DepartmentId == departmentId); // Join ngầm qua Employee

                query = query.Where(x => x.Date >= from && x.Date <= to);

                // 3. Sắp xếp
                query = query.OrderBy(x => x.Date).ThenBy(x => x.Employee.FullName);

                // 4. Select DTO (KHÔNG PHÂN TRANG - Dùng ToListAsync luôn)
                var result = await query.Select(x => new WorkScheduleDto
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    EmployeeFullName = x.Employee.FullName,
                    Date = x.Date,

                    // Mapping ShiftTemplate
                    ShiftTemplateId = x.ShiftTemplateId,
                    ShiftName = x.ShiftTemplate.Name,
                    ShiftStartTime = x.ShiftTemplate.StartTime,
                    ShiftEndTime = x.ShiftTemplate.EndTime,

                    Note = x.Note
                }).ToListAsync(ct);

                // Trả về mảng trực tiếp (hoặc bọc trong object tùy chuẩn của bạn)
                return this.OKList(result, $"Lấy thành công {result.Count} lịch.");
            }
            catch (Exception ex)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, $"Lỗi khi lấy toàn bộ lịch: {ex.Message}");
            }
        }
    }
}
