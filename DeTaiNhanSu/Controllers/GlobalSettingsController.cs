using DeTaiNhanSu.Common; // this.OK / this.FAIL / OKSingle...
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Linq.Dynamic.Core;
using DeTaiNhanSu.Dtos; // Cần thiết cho OrderBy(sort)

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class GlobalSettingsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public GlobalSettingsController(AppDbContext db) => _db = db;

        // ========= GET: /api/globalsettings?q=&current=&pageSize=&sort=
        // Cho phép tìm kiếm theo Key và Description
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.GlobalSettings.AsNoTracking().AsQueryable();

                // Lọc theo chuỗi tìm kiếm (Key hoặc Description)
                if (!string.IsNullOrWhiteSpace(q))
                {
                    string search = q.Trim();
                    query = query.Where(x => x.Key.Contains(search) || (x.Description != null && x.Description.Contains(search)));
                }

                // Sắp xếp
                query = sort?.Trim() switch
                {
                    "-Key" => query.OrderByDescending(x => x.Key).ThenBy(x => x.Id),
                    "Key" => query.OrderBy(x => x.Key).ThenBy(x => x.Id),
                    _ => query.OrderBy(x => x.Key) // Mặc định sắp xếp theo Key
                };

                var total = await query.CountAsync(ct);

                var page = query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize);

                var result = await page
                    .Select(x => new GlobalSettingDto
                    {
                        Id = x.Id,
                        Key = x.Key,
                        Value = x.Value,
                        Description = x.Description
                    })
                    .ToListAsync(ct);

                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Tìm thấy {total} cấu hình toàn cục." : "Không có cấu hình.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm cấu hình.");
            }
        }

        // ========= GET: /api/globalsettings/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var x = await _db.GlobalSettings.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == id, ct);

                if (x is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy cấu hình này.");

                var dto = new GlobalSettingDto
                {
                    Id = x.Id,
                    Key = x.Key,
                    Value = x.Value,
                    Description = x.Description
                };

                return this.OKSingle(dto, "Lấy thông tin cấu hình thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy thông tin cấu hình.");
            }
        }

        // ========= POST: /api/globalsettings
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateGlobalSettingRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                // Validate fields (Key/Value already handled by [Required] and nullable types)

                // Business: Không cho trùng Key
                var dup = await _db.GlobalSettings.AnyAsync(o => o.Key == req.Key.Trim(), ct);
                if (dup) return this.FAIL(StatusCodes.Status409Conflict, $"Key '{req.Key}' đã tồn tại.");

                var entity = new GlobalSetting
                {
                    Id = Guid.NewGuid(),
                    Key = req.Key.Trim(),
                    Value = req.Value.Trim(),
                    Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim()
                };

                _db.GlobalSettings.Add(entity);
                await _db.SaveChangesAsync(ct);

                var dto = new GlobalSettingDto
                {
                    Id = entity.Id,
                    Key = entity.Key,
                    Value = entity.Value,
                    Description = entity.Description
                };

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo cấu hình toàn cục thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo do xung đột dữ liệu (Key có thể đã tồn tại).");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo cấu hình.");
            }
        }

        // ========= PUT (partial): /api/globalsettings/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var e = await _db.GlobalSettings.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy cấu hình.");

                bool changed = false;
                string? newKey = null;

                // --- Key ---
                if (body.TryGetProperty("key", out var keyProp) && keyProp.ValueKind == JsonValueKind.String)
                {
                    newKey = keyProp.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(newKey))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Key không được để trống.");

                    if (newKey != e.Key)
                    {
                        // Chống trùng Key
                        var conflict = await _db.GlobalSettings.AnyAsync(o => o.Key == newKey && o.Id != e.Id, ct);
                        if (conflict) return this.FAIL(StatusCodes.Status409Conflict, $"Key '{newKey}' đã tồn tại.");

                        e.Key = newKey;
                        changed = true;
                    }
                }

                // --- Value ---
                if (body.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.String)
                {
                    var newValue = valueProp.GetString()?.Trim() ?? string.Empty;
                    if (newValue != e.Value)
                    {
                        e.Value = newValue;
                        changed = true;
                    }
                }
                else if (body.TryGetProperty("value", out var valueNullProp) && valueNullProp.ValueKind == JsonValueKind.Null)
                {
                    if (e.Value != string.Empty)
                    {
                        e.Value = string.Empty; // Value là NOT NULL trong DB
                        changed = true;
                    }
                }

                // --- Description ---
                if (body.TryGetProperty("description", out var descProp))
                {
                    var newDesc = descProp.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => string.IsNullOrWhiteSpace(descProp.GetString()) ? null : descProp.GetString()!.Trim(),
                        _ => null
                    };

                    if (newDesc != e.Description)
                    {
                        e.Description = newDesc;
                        changed = true;
                    }
                }

                if (changed)
                {
                    await _db.SaveChangesAsync(ct);
                }

                var dto = new GlobalSettingDto
                {
                    Id = e.Id,
                    Key = e.Key,
                    Value = e.Value,
                    Description = e.Description
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = changed ? "Cập nhật cấu hình thành công." : "Không có thay đổi nào được gửi.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Key cấu hình mới có thể đã trùng lặp.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi cập nhật cấu hình.");
            }
        }

        // ========= DELETE: /api/globalsettings/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.GlobalSettings.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy cấu hình.");

                _db.GlobalSettings.Remove(e);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá cấu hình thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do ràng buộc dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá cấu hình.");
            }
        }
    }
}