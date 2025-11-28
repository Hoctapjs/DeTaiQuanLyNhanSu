using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.WorkScheduleDtoFol;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShiftTemplateController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ShiftTemplateController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /api/shifttemplate
        [HttpGet]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = "Code",
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.ShiftTemplates.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(x => x.Code.Contains(q) || x.Name.Contains(q));
                }

                query = sort?.Trim() switch
                {
                    "-Code" => query.OrderByDescending(x => x.Code),
                    "Name" => query.OrderBy(x => x.Name),
                    "-Name" => query.OrderByDescending(x => x.Name),
                    _ => query.OrderBy(x => x.Code)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new ShiftTemplateDto
                    {
                        Id = x.Id,
                        Code = x.Code,
                        Name = x.Name,
                        StartTime = x.StartTime,
                        EndTime = x.EndTime,
                        BreakDurationMinutes = x.BreakDurationMinutes,
                        TotalWorkingHours = x.TotalWorkingHours,
                        Description = x.Description,
                        WorkDay = (double)(x.TotalWorkingHours / 8)
                    })
                    .ToListAsync(ct);

                var meta = new { current, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize), total };
                return this.OKSingle(new { meta, result }, total > 0 ? "Tìm thấy mẫu ca." : "Không có kết quả.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm mẫu ca."); }
        }

        // GET: /api/shifttemplate/all (Dropdown)
        [HttpGet("all")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            try
            {
                // Trả về list gọn nhẹ để đổ vào dropdown
                var result = await _db.ShiftTemplates.AsNoTracking()
                    .OrderBy(x => x.Code)
                    .Select(x => new {
                        x.Id,
                        DisplayName = $"{x.Code} - {x.Name} ({x.StartTime:HH:mm}-{x.EndTime:HH:mm})"
                    })
                    .ToListAsync(ct);
                return this.OKList(result, "Lấy danh sách thành công.");
            }
            catch { return this.FAIL(500, "Lỗi server."); }
        }

        // GET: /api/shifttemplate/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin, Manager")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var x = await _db.ShiftTemplates.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
                if (x is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy mẫu ca.");

                var dto = new ShiftTemplateDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime,
                    BreakDurationMinutes = x.BreakDurationMinutes,
                    TotalWorkingHours = x.TotalWorkingHours,
                    Description = x.Description
                };
                return this.OKSingle(dto, "Lấy thông tin thành công.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy thông tin."); }
        }

        // POST: /api/shifttemplate
        [HttpPost]
        [Authorize(Roles = "HR, Admin")] // Chỉ HR/Admin tạo
        public async Task<IActionResult> Create([FromBody] CreateShiftTemplateRequest req, CancellationToken ct)
        {
            try
            {
                if (req == null) return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu lỗi.");

                // Validate Code unique
                if (await _db.ShiftTemplates.AnyAsync(x => x.Code == req.Code, ct))
                    return this.FAIL(StatusCodes.Status409Conflict, $"Mã ca '{req.Code}' đã tồn tại.");

                // Tính toán giờ làm việc
                // Logic: (End - Start) - Break
                TimeSpan duration = req.EndTime - req.StartTime;
                if (duration.TotalMinutes < 0) duration = duration.Add(TimeSpan.FromDays(1)); // Xử lý ca qua đêm
                decimal totalHours = (decimal)(duration.TotalMinutes - req.BreakDurationMinutes) / 60m;

                var entity = new ShiftTemplate
                {
                    Id = Guid.NewGuid(),
                    Code = req.Code.Trim().ToUpperInvariant(),
                    Name = req.Name.Trim(),
                    StartTime = req.StartTime,
                    EndTime = req.EndTime,
                    BreakDurationMinutes = req.BreakDurationMinutes,
                    TotalWorkingHours = Math.Round(totalHours, 2),
                    Description = req.Description
                };

                _db.ShiftTemplates.Add(entity);
                await _db.SaveChangesAsync(ct);

                var dto = new ShiftTemplateDto
                {
                    Id = entity.Id,
                    Code = entity.Code,
                    Name = entity.Name,
                    StartTime = entity.StartTime,
                    EndTime = entity.EndTime,
                    BreakDurationMinutes = entity.BreakDurationMinutes,
                    TotalWorkingHours = entity.TotalWorkingHours,
                    Description = entity.Description
                };

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo mẫu ca thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tạo mẫu ca."); }
        }

        // PUT: /api/shifttemplate/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShiftTemplateRequest req, CancellationToken ct)
        {
            try
            {
                var entity = await _db.ShiftTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (entity is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy.");

                // Update fields
                if (!string.IsNullOrWhiteSpace(req.Code))
                {
                    var newCode = req.Code.Trim().ToUpperInvariant();
                    if (newCode != entity.Code && await _db.ShiftTemplates.AnyAsync(x => x.Code == newCode, ct))
                        return this.FAIL(StatusCodes.Status409Conflict, "Mã ca đã tồn tại.");
                    entity.Code = newCode;
                }

                if (!string.IsNullOrWhiteSpace(req.Name)) entity.Name = req.Name.Trim();
                if (req.Description != null) entity.Description = req.Description;

                bool timeChanged = false;
                if (req.StartTime.HasValue) { entity.StartTime = req.StartTime.Value; timeChanged = true; }
                if (req.EndTime.HasValue) { entity.EndTime = req.EndTime.Value; timeChanged = true; }
                if (req.BreakDurationMinutes.HasValue) { entity.BreakDurationMinutes = req.BreakDurationMinutes.Value; timeChanged = true; }

                // Tính lại tổng giờ nếu thời gian thay đổi
                if (timeChanged)
                {
                    TimeSpan duration = entity.EndTime - entity.StartTime;
                    if (duration.TotalMinutes < 0) duration = duration.Add(TimeSpan.FromDays(1));
                    decimal totalHours = (decimal)(duration.TotalMinutes - entity.BreakDurationMinutes) / 60m;
                    entity.TotalWorkingHours = Math.Round(totalHours, 2);
                }

                await _db.SaveChangesAsync(ct);

                var dto = new ShiftTemplateDto
                {
                    Id = entity.Id,
                    Code = entity.Code,
                    Name = entity.Name,
                    StartTime = entity.StartTime,
                    EndTime = entity.EndTime,
                    BreakDurationMinutes = entity.BreakDurationMinutes,
                    TotalWorkingHours = entity.TotalWorkingHours,
                    Description = entity.Description
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi cập nhật."); }
        }

        // DELETE: /api/shifttemplate/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var entity = await _db.ShiftTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (entity is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy.");

                // Kiểm tra ràng buộc: Nếu đã có WorkSchedule dùng ca này thì không cho xóa
                bool inUse = await _db.WorkSchedules.AnyAsync(w => w.ShiftTemplateId == id, ct);
                if (inUse) return this.FAIL(StatusCodes.Status409Conflict, "Không thể xóa vì ca này đang được sử dụng trong lịch làm việc.");

                _db.ShiftTemplates.Remove(entity);
                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Xóa thành công.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi xóa."); }
        }
    }
}
