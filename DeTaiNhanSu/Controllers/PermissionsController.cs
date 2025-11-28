using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PermissionsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PermissionsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /api/permissions?q=...
        [HttpGet]
        [Authorize(Roles = "Admin")] // Chỉ Admin mới được xem/quản lý quyền
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
                if (pageSize < 1 || pageSize > 200) pageSize = 20;

                var query = _db.Permissions.AsNoTracking()
                    .Include(p => p.RolePermissions) // Để đếm số role
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(x => x.Code.Contains(q) || (x.Description != null && x.Description.Contains(q)));
                }

                query = sort?.Trim() switch
                {
                    "-Code" => query.OrderByDescending(x => x.Code),
                    "Description" => query.OrderBy(x => x.Description),
                    "-Description" => query.OrderByDescending(x => x.Description),
                    _ => query.OrderBy(x => x.Code)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new PermissionDto
                    {
                        Id = x.Id,
                        Code = x.Code,
                        Description = x.Description,
                        RolesCount = x.RolePermissions.Count
                    })
                    .ToListAsync(ct);

                var meta = new { current, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize), total };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = total > 0 ? "Tìm thấy quyền hạn." : "Không có kết quả.",
                    data = new { meta, result },
                    success = true
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tìm kiếm quyền hạn." });
            }
        }

        // GET: /api/permissions/all (Dùng cho dropdown gán quyền)
        [HttpGet("all")]
        [Authorize(Roles = "Admin, HR")]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            try
            {
                var result = await _db.Permissions.AsNoTracking()
                    .OrderBy(x => x.Code)
                    .Select(x => new
                    {
                        x.Id,
                        x.Code,
                        x.Description
                    })
                    .ToListAsync(ct);

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Lấy danh sách thành công.",
                    data = new { result },
                    success = true
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi server." });
            }
        }

        // GET: /api/permissions/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var p = await _db.Permissions.AsNoTracking()
                    .Include(x => x.RolePermissions)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (p is null)
                    return StatusCode(StatusCodes.Status404NotFound, new { message = "Không tìm thấy quyền." });

                var dto = new PermissionDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Description = p.Description,
                    RolesCount = p.RolePermissions.Count
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Lấy thông tin thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy thông tin." });
            }
        }

        // POST: /api/permissions
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreatePermissionRequest req, CancellationToken ct)
        {
            try
            {
                if (await _db.Permissions.AnyAsync(x => x.Code == req.Code, ct))
                    return StatusCode(StatusCodes.Status409Conflict, new { message = $"Mã quyền '{req.Code}' đã tồn tại." });

                var entity = new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = req.Code.Trim(),
                    Description = req.Description?.Trim()
                };

                _db.Permissions.Add(entity);
                await _db.SaveChangesAsync(ct);

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo quyền thành công.",
                    data = new { result = entity },
                    success = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi tạo quyền: {ex.Message}" });
            }
        }

        // PUT: /api/permissions/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePermissionRequest req, CancellationToken ct)
        {
            try
            {
                var p = await _db.Permissions.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (p is null) return StatusCode(StatusCodes.Status404NotFound, new { message = "Không tìm thấy quyền." });

                // Nếu sửa Code, kiểm tra trùng
                if (!string.IsNullOrWhiteSpace(req.Code) && req.Code != p.Code)
                {
                    var dup = await _db.Permissions.AnyAsync(x => x.Code == req.Code && x.Id != id, ct);
                    if (dup) return StatusCode(StatusCodes.Status409Conflict, new { message = "Mã quyền đã tồn tại." });
                    p.Code = req.Code.Trim();
                }

                if (req.Description != null) p.Description = req.Description.Trim();

                await _db.SaveChangesAsync(ct);

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật thành công.",
                    success = true
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi cập nhật." });
            }
        }

        // DELETE: /api/permissions/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var p = await _db.Permissions.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (p is null) return StatusCode(StatusCodes.Status404NotFound, new { message = "Không tìm thấy quyền." });

                // Kiểm tra đang được sử dụng
                var inUse = await _db.RolePermissions.AnyAsync(rp => rp.PermissionId == id, ct);
                if (inUse)
                    return StatusCode(StatusCodes.Status409Conflict, new { message = "Không thể xóa vì quyền này đang được gán cho Vai trò (Role)." });

                _db.Permissions.Remove(p);
                await _db.SaveChangesAsync(ct);

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Xóa thành công.",
                    success = true
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi xóa." });
            }
        }
    }
}
