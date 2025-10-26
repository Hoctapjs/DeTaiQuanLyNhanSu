using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using System.Security.Claims;
using DeTaiNhanSu.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DeTaiNhanSu.Common;
using Microsoft.EntityFrameworkCore;
using DeTaiNhanSu.Dtos;
using System.Text.Json;


namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        //[HttpGet]
        //[Authorize(Roles = "Admin")]
        //public IActionResult GetAll()
        //{
        //    return Ok("Only Admins can see."); //temp
        //}

        //[HttpGet("me/permissions")]
        //[HasPermission("Users.View")]
        //public IActionResult MyPerms()
        //{
        //    return Ok("You have Users.View"); //temp
        //}

        // nghiệp vụ
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _hasher;

        public UsersController(AppDbContext db, IPasswordHasher<User> hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        // GET /api/users/Search?q=son&page=1&pageSize=20&role=Admin&status=active
        //[HttpGet("Search")]
        //[Authorize]
        //[HasPermission("Users.View")]
        //public async Task<IActionResult> Search(
        //    [FromQuery] string? q,
        //    [FromQuery] string? role,
        //    [FromQuery] UserStatus? status,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20,
        //    CancellationToken ct = default)
        //{
        //    try
        //    {
        //        if (page < 1) page = 1;
        //        if (pageSize is < 1 or > 200) pageSize = 20;

        //        var query = _db.Users.AsNoTracking()
        //            .Include(u => u.Employee)
        //            .Include(u => u.Role)
        //            .AsQueryable();

        //        if (!string.IsNullOrWhiteSpace(q))
        //        {
        //            q = q.Trim();
        //            query = query.Where(u =>
        //                u.UserName.Contains(q) ||
        //                (u.Employee.FullName != null && u.Employee.FullName.Contains(q)) ||
        //                (u.Employee.Email != null && u.Employee.Email.Contains(q)));
        //        }

        //        if (!string.IsNullOrWhiteSpace(role))
        //            query = query.Where(u => u.Role.Name == role);

        //        if (status is not null)
        //            query = query.Where(u => u.Status == status);

        //        query = query.OrderBy(u => u.Employee.FullName).ThenBy(u => u.UserName);

        //        var total = await query.CountAsync(ct);

        //        var items = await query
        //            .Skip((page - 1) * pageSize)
        //            .Take(pageSize)
        //            .Select(u => new UserDto
        //            {
        //                Id = u.Id,
        //                UserName = u.UserName,
        //                RoleId = u.RoleId,
        //                RoleName = u.Role.Name,
        //                Status = u.Status,
        //                LastLoginAt = u.LastLoginAt,
        //                EmployeeId = u.EmployeeId,
        //                EmployeeCode = u.Employee.Code,
        //                EmployeeName = u.Employee.FullName,
        //                EmployeeEmail = u.Employee.Email
        //            })
        //            .ToListAsync(ct);

        //        var payload = new { total, page, pageSize, items };
        //        return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} người dùng." : "Không có kết quả.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm người dùng.");
        //    }
        //}

        [HttpGet("Search")]
        [Authorize(Roles = "HR, Admin")]
        [HasPermission("Users.View")]
        public async Task<IActionResult> Search(
    [FromQuery] string? q,
    [FromQuery] string? role,
    [FromQuery] UserStatus? status,
    [FromQuery] int current = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.Users.AsNoTracking()
                    .Include(u => u.Employee)
                    .Include(u => u.Role)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(u =>
                        u.UserName.Contains(q) ||
                        (u.Employee.FullName != null && u.Employee.FullName.Contains(q)) ||
                        (u.Employee.Email != null && u.Employee.Email.Contains(q)));
                }

                if (!string.IsNullOrWhiteSpace(role))
                    query = query.Where(u => u.Role.Name == role);

                if (status is not null)
                    query = query.Where(u => u.Status == status);

                query = query.OrderBy(u => u.Employee.FullName).ThenBy(u => u.UserName);

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        RoleId = u.RoleId,
                        RoleName = u.Role.Name,
                        Status = u.Status,
                        LastLoginAt = u.LastLoginAt,
                        EmployeeId = u.EmployeeId,
                        EmployeeCode = u.Employee.Code,
                        EmployeeName = u.Employee.FullName,
                        EmployeeEmail = u.Employee.Email
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
                return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} người dùng." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm người dùng.");
            }
        }


        // GET /api/users/GetById?id={guid}
        [HttpGet("GetById")]
        [Authorize(Roles = "HR, Admin")]
        [HasPermission("Users.View")]
        public async Task<IActionResult> GetById([FromQuery] Guid id, CancellationToken ct)
        {
            try
            {
                var u = await _db.Users.AsNoTracking()
                    .Include(x => x.Employee)
                    .Include(x => x.Role)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (u is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");

                var dto = new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    RoleId = u.RoleId,
                    RoleName = u.Role.Name,
                    Status = u.Status,
                    LastLoginAt = u.LastLoginAt,
                    EmployeeId = u.EmployeeId,
                    EmployeeCode = u.Employee.Code,
                    EmployeeName = u.Employee.FullName,
                    EmployeeEmail = u.Employee.Email
                };

                return this.OKSingle(dto, "Lấy thông tin user thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin user.");
            }
        }

        // POST /api/users
        [HttpPost]
        [Authorize(Roles = "HR, Admin")]
        [HasPermission("Users.Manage")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
        {
            try
            {
                // employee tồn tại?
                var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == req.EmployeeId, ct);
                if (emp is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Employee không tồn tại.");

                // mỗi employee 1 user
                var hasUser = await _db.Users.AnyAsync(x => x.EmployeeId == req.EmployeeId, ct);
                if (hasUser)
                    return this.FAIL(StatusCodes.Status409Conflict, "Nhân viên này đã có tài khoản.");

                // unique username
                if (await _db.Users.AnyAsync(x => x.UserName == req.UserName, ct))
                    return this.FAIL(StatusCodes.Status409Conflict, "UserName đã tồn tại.");

                // role tồn tại?
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == req.RoleId, ct);
                if (role is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    UserName = req.UserName!,
                    RoleId = req.RoleId,
                    Status = req.Status ?? UserStatus.active
                };
                user.PasswordHash = _hasher.HashPassword(user, req.TempPassword ?? "Temp@123");

                _db.Users.Add(user);
                await _db.SaveChangesAsync(ct);

                // trả 201 với Id
                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo tài khoản thành công.",
                    data = new[] { new { user.Id } },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo user do xung đột dữ liệu (trùng/ràng buộc).");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo user.");
            }
        }

        // PUT /api/users/{id}
        //[HttpPut("{id:guid}")]
        //[Authorize(Roles = "HR, Admin")]
        //[HasPermission("Users.Manage")]
        //public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        //        if (u is null)
        //            return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");

        //        if (!string.Equals(u.UserName, req.UserName, StringComparison.OrdinalIgnoreCase) &&
        //            await _db.Users.AnyAsync(x => x.UserName == req.UserName, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "UserName đã tồn tại.");

        //        if (req.RoleId != u.RoleId)
        //        {
        //            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == req.RoleId, ct);
        //            if (role is null)
        //                return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");
        //            u.RoleId = req.RoleId;
        //        }

        //        u.UserName = req.UserName!;
        //        if (req.Status is not null) u.Status = req.Status.Value;

        //        await _db.SaveChangesAsync(ct);
        //        return this.OK(message: "Cập nhật user thành công.");
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật user.");
        //    }
        //}

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        [HasPermission("Users.Manage")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (u is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");

                // helpers
                static string? GetStringOrNull(JsonElement prop) =>
                    prop.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => string.IsNullOrWhiteSpace(prop.GetString()) ? null : prop.GetString()!.Trim(),
                        _ => null
                    };

                static Guid? GetGuidOrNull(JsonElement prop)
                {
                    if (prop.ValueKind == JsonValueKind.Null) return null;
                    if (prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var g)) return g;
                    return null;
                }

                static bool TryGetInt(JsonElement prop, out int? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) { value = null; return true; }
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) { value = i; return true; }
                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var j)) { value = j; return true; }
                    return false;
                }

                // userName (unique, non-empty) — chỉ xử lý khi key "userName" xuất hiện
                if (body.TryGetProperty("userName", out var userNameProp))
                {
                    var newUserName = GetStringOrNull(userNameProp);
                    if (string.IsNullOrWhiteSpace(newUserName))
                        return this.FAIL(StatusCodes.Status400BadRequest, "UserName không được để trống.");

                    if (!string.Equals(u.UserName, newUserName, StringComparison.OrdinalIgnoreCase))
                    {
                        var dup = await _db.Users.AnyAsync(x => x.UserName == newUserName!, ct);
                        if (dup) return this.FAIL(StatusCodes.Status409Conflict, "UserName đã tồn tại.");

                        u.UserName = newUserName!;
                        // Nếu bạn có cột NormalizedUserName:
                        // u.NormalizedUserName = newUserName!.ToUpperInvariant();
                    }
                }

                // roleId (GUID, bắt buộc GUID hợp lệ; tùy DB có cho null không)
                if (body.TryGetProperty("roleId", out var roleProp))
                {
                    var newRoleId = GetGuidOrNull(roleProp);
                    if (newRoleId is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "roleId phải là GUID hợp lệ.");

                    if (newRoleId != u.RoleId)
                    {
                        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == newRoleId.Value, ct);
                        if (role is null)
                            return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

                        u.RoleId = newRoleId.Value;
                    }
                }

                // status (int enum, không cho null nếu cột không nullable)
                if (body.TryGetProperty("status", out var statusProp))
                {
                    if (!TryGetInt(statusProp, out var newStatus) || !newStatus.HasValue)
                        return this.FAIL(StatusCodes.Status400BadRequest, "status phải là số và không được null.");

                    // (tuỳ chọn) validate range enum
                    // if (newStatus < 0 || newStatus > 5) return this.FAIL(400, "Giá trị status không hợp lệ.");

                    u.Status = (UserStatus)newStatus.Value;
                }

                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Cập nhật user thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật user.");
            }
        }

        // PATCH /api/users/{id}/role
        [HttpPatch("{id:guid}/role")]
        [Authorize(Roles = "HR, Admin")]
        [HasPermission("Users.Manage")]
        public async Task<IActionResult> SetRole(Guid id, [FromBody] SetRoleRequest body, CancellationToken ct)
        {
            try
            {
                var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (u is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");

                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == body.RoleId, ct);
                if (role is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Role không tồn tại.");

                u.RoleId = body.RoleId;
                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Đã đổi role.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi đổi role.");
            }
        }

        // PATCH /api/users/{id}/status?value=locked|active
        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = "HR, Admin")]
        [HasPermission("Users.Manage")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromQuery] UserStatus value, CancellationToken ct)
        {
            try
            {
                var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (u is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(currentUserId, out var me) && me == id && value == UserStatus.locked)
                    return this.FAIL(StatusCodes.Status409Conflict, "Không thể tự khoá tài khoản đang đăng nhập.");

                u.Status = value;
                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Đã cập nhật trạng thái.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi cập nhật trạng thái.");
            }
        }

        // GET /api/users/{id}/permissions
        [HttpGet("{id:guid}/permissions")]
        [Authorize]
        [HasPermission("Users.View")]
        public async Task<IActionResult> GetPermissions(Guid id, CancellationToken ct)
        {
            try
            {
                var exists = await _db.Users.AnyAsync(u => u.Id == id, ct);
                if (!exists)
                    return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");

                var perms = await _db.RolePermissions
                    .Where(rp => rp.Role.Users.Any(u => u.Id == id))
                    .Select(rp => rp.Permission.Code)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(ct);

                return this.OKList(perms, $"User có {perms.Count} permission.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy permissions.");
            }
        }

        // DELETE /api/users/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        [HasPermission("Users.Manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (u is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(currentUserId, out var me) && me == id)
                    return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá chính tài khoản đang đăng nhập.");

                _db.Users.Remove(u);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Đã xoá user.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi xoá user.");
            }
        }

    }
}
