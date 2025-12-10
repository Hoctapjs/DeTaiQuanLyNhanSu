using System.Text.Json;
using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Hubs;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PositionController : ControllerBase
    {
        //follow schema
        private readonly AppDbContext _db;

        private readonly IHubContext<NotificationNewHub> _hubContext;

        public PositionController(AppDbContext db, IHubContext<NotificationNewHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }


        [HttpGet]
        //[HasPermission("Positions.View")]
        [Authorize(Roles = "HR, Manager")]
        public async Task<IActionResult> Search(
    [FromQuery] string? q,
    [FromQuery] int current = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? sort = "Name",
    CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.Positions.AsNoTracking()
                    .Include(p => p.Employees)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(p =>
                        p.Name.Contains(q) ||
                        (p.Level != null && p.Level.Contains(q)));
                }

                query = sort?.Trim() switch
                {
                    "-Name" => query.OrderByDescending(p => p.Name),
                    "Level" => query.OrderBy(p => p.Level),
                    "-Level" => query.OrderByDescending(p => p.Level),
                    _ => query.OrderBy(p => p.Name),
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PositionDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Level = p.Level,
                        EmployeesCount = p.Employees.Count
                    })
                    .ToListAsync(ct);

                var meta = new
                {
                    current = current,
                    pageSize = pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                var payload = new { meta, result };
                return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} chức vụ." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm chức vụ.");
            }
        }

        [HttpGet("all-id-name")]
        [Authorize(Roles = "HR, Manager")]
        public async Task<IActionResult> GetAllPositions(
    [FromQuery] string? q,
    [FromQuery] string? sort = "Name",
    CancellationToken ct = default)
        {
            try
            {
                var query = _db.Positions
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(p =>
                        p.Name.Contains(q) ||
                        (p.Level != null && p.Level.Contains(q)));
                }

                query = sort?.Trim() switch
                {
                    "-Name" => query.OrderByDescending(p => p.Name),
                    "Level" => query.OrderBy(p => p.Level),
                    "-Level" => query.OrderByDescending(p => p.Level),
                    _ => query.OrderBy(p => p.Name),
                };

                var result = await query
                    .Select(p => new
                    {
                        positionId = p.Id,
                        positionName = p.Name
                    })
                    .ToListAsync(ct);

                return this.OKSingle(new { result },
                    result.Count > 0 ? $"Có {result.Count} chức vụ." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy danh sách chức vụ.");
            }
        }



        // GET /api/position/{id}
        [HttpGet("{id:guid}")]
        //[HasPermission("Positions.View")]
        [Authorize(Roles = "HR, Manager")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var p = await _db.Positions
                    .AsNoTracking()
                    .Include(x => x.Employees)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (p is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy chức vụ.");

                var dto = new PositionDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Level = p.Level,
                    EmployeesCount = p.Employees.Count
                };

                return this.OKSingle(dto, "Lấy thông tin chức vụ thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin chức vụ.");
            }
        }

        [HttpPost]
        [Authorize(Roles = "HR")]
        //[HasPermission("Positions.Manage")]
        public async Task<IActionResult> Create([FromBody] CreatePositionRequest req, CancellationToken ct)
        {
            try
            {
                // Unique theo Name
                if (await _db.Positions.AnyAsync(x => x.Name == req.Name!, ct))
                    return this.FAIL(StatusCodes.Status409Conflict, "Tên chức vụ đã tồn tại.");

                // Tạo entity
                var p = new Position
                {
                    Id = Guid.NewGuid(),
                    Name = req.Name!,
                    Level = req.Level
                };
                _db.Positions.Add(p);
                await _db.SaveChangesAsync(ct);

                // Load lại đầy đủ để có EmployeesCount
                var pos = await _db.Positions
                    .AsNoTracking()
                    .Include(x => x.Employees)
                    .FirstAsync(x => x.Id == p.Id, ct);

                var dto = new PositionDto
                {
                    Id = pos.Id,
                    Name = pos.Name,
                    Level = pos.Level,
                    EmployeesCount = pos.Employees.Count
                };

                // thêm gọi signal ir sau khi thêm
                await _hubContext.Clients.Group("HR_Admins").SendAsync(
                    "PositionChanged",
                    new { action = "create", data = dto },
                    ct);

                // Trả 201 với full object theo schema (data.result)
                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo chức vụ thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo chức vụ do xung đột dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo chức vụ.");
            }
        }


        

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR")]
        //[HasPermission("Positions.Manage")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var p = await _db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (p is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy chức vụ.");

                // helpers
                static string? GetStringOrNull(JsonElement prop) =>
                    prop.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => string.IsNullOrWhiteSpace(prop.GetString()) ? null : prop.GetString()!.Trim(),
                        _ => null
                    };

                // name (unique, non-empty) — chỉ xử lý khi key "name" xuất hiện
                if (body.TryGetProperty("name", out var nameProp))
                {
                    var newName = GetStringOrNull(nameProp);
                    if (string.IsNullOrWhiteSpace(newName))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Tên chức vụ không được để trống.");

                    if (!string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase))
                    {
                        var dup = await _db.Positions.AnyAsync(x => x.Name == newName!, ct);
                        if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Tên chức vụ đã tồn tại.");
                        p.Name = newName!;
                    }
                }

                // level (string). Chỉ set khi có gửi; cho phép null nếu DB schema cho phép.
                if (body.TryGetProperty("level", out var levelProp))
                {
                    if (levelProp.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                        return this.FAIL(StatusCodes.Status400BadRequest, "level phải là chuỗi (string) hoặc null.");

                    var newLevel = levelProp.ValueKind == JsonValueKind.Null
                        ? null
                        : levelProp.GetString()?.Trim();

                    // Nếu cột Level là NOT NULL trong DB thì bỏ comment dòng dưới để enforce:
                    // if (string.IsNullOrWhiteSpace(newLevel)) return this.FAIL(StatusCodes.Status400BadRequest, "level không được để trống.");

                    p.Level = newLevel;
                }

                await _db.SaveChangesAsync(ct);

                // === Load lại đầy đủ để trả về FULL object ===
                var pos = await _db.Positions
                    .AsNoTracking()
                    .Include(x => x.Employees)
                    .FirstAsync(x => x.Id == p.Id, ct);

                var dto = new PositionDto
                {
                    Id = pos.Id,
                    Name = pos.Name,
                    Level = pos.Level,
                    EmployeesCount = pos.Employees.Count
                };

                await _hubContext.Clients.Group("HR_Admins").SendAsync(
                    "PositionChanged",
                    new { action = "update", data = dto },
                    ct);


                // 200 theo schema: data.result = dto
                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật chức vụ thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật chức vụ.");
            }
        }


        // DELETE /api/position/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR")]
        //[HasPermission("Positions.Manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var p = await _db.Positions
                    .Include(x => x.Employees)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (p is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy chức vụ.");

                if (p.Employees.Any())
                    return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá chức vụ khi vẫn còn nhân viên sử dụng.");

                _db.Positions.Remove(p);
                await _db.SaveChangesAsync(ct);

                await _hubContext.Clients.Group("HR_Admins").SendAsync(
                    "PositionChanged",
                    new { action = "delete", data = new { id = id } },
                    ct);

                return this.OK(message: "Xoá chức vụ thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi xoá chức vụ.");
            }
        }

        private static ProblemDetails Problem409(string title, string detail)
        {
            return new()
            {
                Title = title,
                Detail = detail,
                Status = StatusCodes.Status409Conflict
            };
        }
    }
}
