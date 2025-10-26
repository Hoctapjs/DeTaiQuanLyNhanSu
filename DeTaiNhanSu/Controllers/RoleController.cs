using System.Text.Json;
using DeTaiNhanSu.Common;
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
    public class RoleController : ControllerBase
    {
        private readonly AppDbContext _db;

        public RoleController(AppDbContext db)
        {
            _db = db;
        }

        //[HttpGet]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> Search(
        //    [FromQuery] string? q,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20,
        //    [FromQuery] string? sort = "Name",
        //    CancellationToken ct = default
        //    )
        //{
        //    try
        //    {
        //        if (page < 1) page = 1;
        //        if (pageSize is < 1 or > 200) pageSize = 20;

        //        var query = _db.Roles.AsNoTracking()
        //            .Include(r => r.Users)
        //            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
        //            .AsQueryable();

        //        if (!string.IsNullOrWhiteSpace(q))
        //        {
        //            q = q.Trim();
        //            query = query.Where(r => r.Name.Contains(q) ||
        //                                     (r.Description != null && r.Description.Contains(q)));
        //        }

        //        query = (sort?.Trim()) switch
        //        {
        //            "-Name" => query.OrderByDescending(r => r.Name),
        //            _ => query.OrderBy(r => r.Name)
        //        };

        //        var total = await query.CountAsync(ct);

        //        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(r => new RoleDto
        //        {
        //            Id = r.Id,
        //            Name = r.Name,
        //            Description = r.Description,
        //            UsersCount = r.Users.Count,
        //            Permissions = r.RolePermissions.Select(rp => new PermissionLiteDto
        //            {
        //                Id = rp.PermissionId,
        //                Code = rp.Permission.Code,
        //                Description = rp.Permission.Description,
        //            }).ToList()
        //        }).ToListAsync(ct);

        //        var payload = new { total, page, pageSize, items };
        //        return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} role." : "Không có kết quả.");
        //    }
        //    catch (Exception)
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm role.");
        //    }
        //}

        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
    [FromQuery] string? q,
    [FromQuery] int current = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? sort = "Name",
    CancellationToken ct = default
)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.Roles.AsNoTracking()
                    .Include(r => r.Users)
                    .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(r =>
                        r.Name.Contains(q) ||
                        (r.Description != null && r.Description.Contains(q)));
                }

                query = (sort?.Trim()) switch
                {
                    "-Name" => query.OrderByDescending(r => r.Name),
                    _ => query.OrderBy(r => r.Name)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new RoleDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        UsersCount = r.Users.Count,
                        Permissions = r.RolePermissions.Select(rp => new PermissionLiteDto
                        {
                            Id = rp.PermissionId,
                            Code = rp.Permission.Code,
                            Description = rp.Permission.Description,
                        }).ToList()
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
                return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} role." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm role.");
            }
        }


        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var r = await _db.Roles.AsNoTracking()
                    .Include(x => x.Users)
                    .Include(x => x.RolePermissions).ThenInclude(rp => rp.Permission)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (r is null) return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

                var dto = new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    UsersCount = r.Users.Count,
                    Permissions = r.RolePermissions.Select(rp => new PermissionLiteDto
                    {
                        Id = rp.PermissionId,
                        Code = rp.Permission.Code,
                        Description = rp.Permission.Description
                    }).ToList()
                };

                return this.OKSingle(dto, "Lấy thông tin role thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin role.");
            }
        }

        //[HttpPost]
        //[Authorize(Roles = "Admin")]
        //public async Task<IActionResult> Create([FromBody] CreateRoleRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        if (await _db.Roles.AnyAsync(r => r.Name == req.Name!, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "Tên role đã tồn tại.");

        //        var role = new Role
        //        {
        //            Id = Guid.NewGuid(),
        //            Name = req.Name!,
        //            Description = req.Description
        //        };
        //        _db.Roles.Add(role);
        //        await _db.SaveChangesAsync(ct);

        //        return StatusCode(StatusCodes.Status201Created, new
        //        {
        //            statusCode = StatusCodes.Status201Created,
        //            message = "Tạo role thành công.",
        //            data = new[] { new { role.Id } },
        //            success = true
        //        });
        //    }
        //    catch (DbUpdateException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo role do xung đột dữ liệu.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo role.");
        //    }
        //}

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null || string.IsNullOrWhiteSpace(req.Name))
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                // Unique theo Name
                if (await _db.Roles.AnyAsync(r => r.Name == req.Name!, ct))
                    return this.FAIL(StatusCodes.Status409Conflict, "Tên role đã tồn tại.");

                // Tạo role
                var role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = req.Name!.Trim(),
                    Description = req.Description
                };
                _db.Roles.Add(role);
                await _db.SaveChangesAsync(ct);

                // Load lại đầy đủ để trả DTO (users count + permissions)
                var r = await _db.Roles
                    .AsNoTracking()
                    .Include(x => x.Users)
                    .Include(x => x.RolePermissions).ThenInclude(rp => rp.Permission)
                    .FirstAsync(x => x.Id == role.Id, ct);

                var dto = new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    UsersCount = r.Users.Count,
                    Permissions = r.RolePermissions.Select(rp => new PermissionLiteDto
                    {
                        Id = rp.PermissionId,
                        Code = rp.Permission.Code,
                        Description = rp.Permission.Description
                    }).ToList()
                };

                // 201 + full object theo schema (data.result)
                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo role thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo role do xung đột dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo role.");
            }
        }


        //[HttpPut("{id:guid}")]
        //[Authorize(Roles = "Admin")]
        //public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        //        if (role is null) return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

        //        if (!string.Equals(role.Name, req.Name, StringComparison.OrdinalIgnoreCase) &&
        //            await _db.Roles.AnyAsync(r => r.Name == req.Name!, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "Tên role đã tồn tại.");

        //        role.Name = req.Name!;
        //        role.Description = req.Description;

        //        await _db.SaveChangesAsync(ct);
        //        return this.OK(message: "Cập nhật role thành công.");
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật role.");
        //    }
        //}

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
                if (role is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

                // helper
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
                        return this.FAIL(StatusCodes.Status400BadRequest, "Tên role không được để trống.");

                    if (!string.Equals(role.Name, newName, StringComparison.OrdinalIgnoreCase))
                    {
                        var dup = await _db.Roles.AnyAsync(r => r.Name == newName!, ct);
                        if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Tên role đã tồn tại.");
                        role.Name = newName!;
                    }
                }

                // description (cho phép null để xóa)
                if (body.TryGetProperty("description", out var descProp))
                {
                    role.Description = GetStringOrNull(descProp);
                }

                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Cập nhật role thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật role.");
            }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var role = await _db.Roles.Include(r => r.Users)
                                          .Include(r => r.RolePermissions)
                                          .FirstOrDefaultAsync(r => r.Id == id, ct);
                if (role is null) return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

                if (role.Users.Any())
                    return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá role khi vẫn còn người dùng sử dụng.");

                _db.RolePermissions.RemoveRange(role.RolePermissions);
                _db.Roles.Remove(role);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá role thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi xoá role.");
            }
        }

        [HttpGet("{id:guid}/permissions")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetPermissions(Guid id, CancellationToken ct)
        {
            try
            {
                var exists = await _db.Roles.AnyAsync(r => r.Id == id, ct);
                if (!exists) return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

                var perms = await _db.RolePermissions
                    .Where(rp => rp.RoleId == id)
                    .Include(rp => rp.Permission)
                    .Select(rp => new PermissionLiteDto
                    {
                        Id = rp.PermissionId,
                        Code = rp.Permission.Code,
                        Description = rp.Permission.Description
                    })
                    .OrderBy(p => p.Code)
                    .ToListAsync(ct);

                return this.OKList(perms, $"Role có {perms.Count} permission.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy permissions.");
            }
        }


        //[HttpGet("{id:guid}/users")]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> GetUsers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        if (page < 1) page = 1;
        //        if (pageSize is < 1 or > 200) pageSize = 20;

        //        var query = _db.Users.AsNoTracking().Where(u => u.RoleId == id);

        //        var total = await query.CountAsync(ct);
        //        var items = await query
        //            .OrderBy(u => u.UserName)
        //            .Skip((page - 1) * pageSize)
        //            .Take(pageSize)
        //            .Select(u => new
        //            {
        //                id = u.Id,
        //                username = u.UserName,
        //                employee_id = u.EmployeeId,
        //                status = u.Status.ToString().ToLower(),
        //                last_login_at = u.LastLoginAt
        //            })
        //            .ToListAsync(ct);

        //        var payload = new { total, page, pageSize, items };
        //        return this.OKSingle(payload, $"Tìm thấy {total} người dùng thuộc role.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy danh sách người dùng của role.");
        //    }
        //}

        [HttpGet("{id:guid}/users")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetUsers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.Users.AsNoTracking().Where(u => u.RoleId == id);

                var total = await query.CountAsync(ct);

                var result = await query
                    .OrderBy(u => u.UserName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.UserName,
                        employee_id = u.EmployeeId,
                        status = u.Status.ToString().ToLower(),
                        last_login_at = u.LastLoginAt
                    })
                    .ToListAsync(ct);

                var meta = new
                {
                    current = page,
                    pageSize = pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                var payload = new { meta, result };
                return this.OKSingle(payload, $"Tìm thấy {total} người dùng thuộc role.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy danh sách người dùng của role.");
            }
        }

    }
}
