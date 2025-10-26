using DeTaiNhanSu.Common;               // this.OK / this.FAIL / OKSingle...
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.OvertimeDtoFol;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class OvertimesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public OvertimesController(AppDbContext db) => _db = db;

        // ========= GET: /api/overtimes?employeeId=&from=&to=&minHours=&maxHours=&current=&pageSize=&sort=
        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] Guid? employeeId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] decimal? minHours,
            [FromQuery] decimal? maxHours,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.Overtimes.AsNoTracking().AsQueryable();

                if (employeeId is not null) query = query.Where(x => x.EmployeeId == employeeId);
                if (from is not null) query = query.Where(x => x.Date >= from);
                if (to is not null) query = query.Where(x => x.Date <= to);
                if (minHours is not null) query = query.Where(x => x.Hours >= minHours);
                if (maxHours is not null) query = query.Where(x => x.Hours <= maxHours);

                query = sort?.Trim() switch
                {
                    "-Date" => query.OrderByDescending(x => x.Date).ThenBy(x => x.Id),
                    "Date" => query.OrderBy(x => x.Date).ThenBy(x => x.Id),
                    "-Hours" => query.OrderByDescending(x => x.Hours).ThenByDescending(x => x.Date),
                    "Hours" => query.OrderBy(x => x.Hours).ThenByDescending(x => x.Date),
                    "-Rate" => query.OrderByDescending(x => x.Rate).ThenByDescending(x => x.Date),
                    "Rate" => query.OrderBy(x => x.Rate).ThenByDescending(x => x.Date),
                    _ => query.OrderByDescending(x => x.Date).ThenBy(x => x.Id)
                };

                var total = await query.CountAsync(ct);

                //var result = await query
                //    .Skip((current - 1) * pageSize)
                //    .Take(pageSize)
                //    .Select(x => new OvertimeDto
                //    {
                //        Id = x.Id,
                //        EmployeeId = x.EmployeeId,
                //        Date = x.Date,
                //        Hours = x.Hours,
                //        Rate = x.Rate,
                //        Reason = x.Reason
                //    })
                //    .ToListAsync(ct);

                var page = query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize);

                var result = await
                (
                    from o in page
                    join e0 in _db.Employees.AsNoTracking() on o.EmployeeId equals e0.Id into ej
                    from e in ej.DefaultIfEmpty()

                    join d0 in _db.Departments.AsNoTracking() on e!.DepartmentId equals d0.Id into dj
                    from d in dj.DefaultIfEmpty()

                    join p0 in _db.Positions.AsNoTracking() on e!.PositionId equals p0.Id into pj
                    from p in pj.DefaultIfEmpty()

                    select new OvertimeDto
                    {
                        Id = o.Id,
                        EmployeeId = o.EmployeeId,
                        EmployeeFullName = e != null ? e.FullName : null,
                        DepartmentId = e != null ? e.DepartmentId : null,
                        DepartmentName = d != null ? d.Name : null,
                        PositionId = e != null ? e.PositionId : null,
                        PositionName = p != null ? p.Name : null,
                        Date = o.Date,
                        Hours = o.Hours,
                        Rate = o.Rate,
                        Reason = o.Reason
                    }
                ).ToListAsync(ct);


                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Tìm thấy {total} bản ghi tăng ca." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm tăng ca.");
            }
        }

        // ========= GET: /api/overtimes/all?employeeId=&from=&to=&minHours=&maxHours=&sort=
        [HttpGet("all")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? employeeId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] decimal? minHours,
            [FromQuery] decimal? maxHours,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                var query = _db.Overtimes.AsNoTracking().AsQueryable();

                if (employeeId is not null) query = query.Where(x => x.EmployeeId == employeeId);
                if (from is not null) query = query.Where(x => x.Date >= from);
                if (to is not null) query = query.Where(x => x.Date <= to);
                if (minHours is not null) query = query.Where(x => x.Hours >= minHours);
                if (maxHours is not null) query = query.Where(x => x.Hours <= maxHours);

                query = sort?.Trim() switch
                {
                    "-Date" => query.OrderByDescending(x => x.Date).ThenBy(x => x.Id),
                    "Date" => query.OrderBy(x => x.Date).ThenBy(x => x.Id),
                    "-Hours" => query.OrderByDescending(x => x.Hours).ThenByDescending(x => x.Date),
                    "Hours" => query.OrderBy(x => x.Hours).ThenByDescending(x => x.Date),
                    "-Rate" => query.OrderByDescending(x => x.Rate).ThenByDescending(x => x.Date),
                    "Rate" => query.OrderBy(x => x.Rate).ThenByDescending(x => x.Date),
                    _ => query.OrderByDescending(x => x.Date).ThenBy(x => x.Id)
                };

                //var result = await query.Select(x => new OvertimeDto
                //{
                //    Id = x.Id,
                //    EmployeeId = x.EmployeeId,
                //    Date = x.Date,
                //    Hours = x.Hours,
                //    Rate = x.Rate,
                //    Reason = x.Reason
                //}).ToListAsync(ct);

                var result = await
                (
                    from o in query
                    join e0 in _db.Employees.AsNoTracking() on o.EmployeeId equals e0.Id into ej
                    from e in ej.DefaultIfEmpty()
                    join d0 in _db.Departments.AsNoTracking() on e!.DepartmentId equals d0.Id into dj
                    from d in dj.DefaultIfEmpty()
                    join p0 in _db.Positions.AsNoTracking() on e!.PositionId equals p0.Id into pj
                    from p in pj.DefaultIfEmpty()
                    select new OvertimeDto
                    {
                        Id = o.Id,
                        EmployeeId = o.EmployeeId,
                        EmployeeFullName = e != null ? e.FullName : null,
                        DepartmentId = e != null ? e.DepartmentId : null,
                        DepartmentName = d != null ? d.Name : null,
                        PositionId = e != null ? e.PositionId : null,
                        PositionName = p != null ? p.Name : null,
                        Date = o.Date,
                        Hours = o.Hours,
                        Rate = o.Rate,
                        Reason = o.Reason
                    }
                ).ToListAsync(ct);


                var total = result.Count;
                var meta = new { current = 1, pageSize = total, pages = 1, total };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Có {total} bản ghi tăng ca." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy danh sách tăng ca.");
            }
        }

        // ========= GET: /api/overtimes/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var x = await _db.Overtimes.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == id, ct);

                if (x is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy bản ghi tăng ca.");

                //var dto = new OvertimeDto
                //{
                //    Id = x.Id,
                //    EmployeeId = x.EmployeeId,
                //    Date = x.Date,
                //    Hours = x.Hours,
                //    Rate = x.Rate,
                //    Reason = x.Reason
                //};

                var dto = await
                (
                    from o in _db.Overtimes.AsNoTracking().Where(z => z.Id == id)
                    join e0 in _db.Employees.AsNoTracking() on o.EmployeeId equals e0.Id into ej
                    from e in ej.DefaultIfEmpty()
                    join d0 in _db.Departments.AsNoTracking() on e!.DepartmentId equals d0.Id into dj
                    from d in dj.DefaultIfEmpty()
                    join p0 in _db.Positions.AsNoTracking() on e!.PositionId equals p0.Id into pj
                    from p in pj.DefaultIfEmpty()
                    select new OvertimeDto
                    {
                        Id = o.Id,
                        EmployeeId = o.EmployeeId,
                        EmployeeFullName = e != null ? e.FullName : null,
                        DepartmentId = e != null ? e.DepartmentId : null,
                        DepartmentName = d != null ? d.Name : null,
                        PositionId = e != null ? e.PositionId : null,
                        PositionName = p != null ? p.Name : null,
                        Date = o.Date,
                        Hours = o.Hours,
                        Rate = o.Rate,
                        Reason = o.Reason
                    }
                ).FirstOrDefaultAsync(ct);


                return this.OKSingle(dto, "Lấy thông tin thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy thông tin tăng ca.");
            }
        }

        // ========= POST: /api/overtimes
        [HttpPost]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Create([FromBody] CreateOvertimeRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                // Validate FK
                var empExists = await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct);
                if (!empExists) return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                // Validate fields
                if (req.Date is null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Thiếu ngày tăng ca (date).");
                if (req.Hours is null || req.Hours <= 0 || req.Hours > 24)
                    return this.FAIL(StatusCodes.Status422UnprocessableEntity, "Số giờ phải trong (0, 24].");
                if (req.Rate is null || req.Rate < 1)
                    return this.FAIL(StatusCodes.Status422UnprocessableEntity, "Rate phải ≥ 1.");

                // Business: không cho trùng employee + date (nếu bạn muốn cho phép, bỏ check này)
                var dup = await _db.Overtimes.AnyAsync(o => o.EmployeeId == req.EmployeeId && o.Date == req.Date, ct);
                if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Đã tồn tại bản ghi tăng ca cùng ngày cho nhân viên này.");

                var entity = new Overtime
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    Date = req.Date.Value,
                    Hours = req.Hours.Value,
                    Rate = req.Rate.Value,
                    Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason!.Trim()
                };

                _db.Overtimes.Add(entity);
                await _db.SaveChangesAsync(ct);

                var dto = new OvertimeDto
                {
                    Id = entity.Id,
                    EmployeeId = entity.EmployeeId,
                    Date = entity.Date,
                    Hours = entity.Hours,
                    Rate = entity.Rate,
                    Reason = entity.Reason
                };

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo tăng ca thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo do xung đột dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo tăng ca.");
            }
        }

        // ========= PUT (partial): /api/overtimes/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var e = await _db.Overtimes.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy bản ghi tăng ca.");

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
                static bool TryGetDateOnly(JsonElement prop, out DateOnly? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.String && DateOnly.TryParse(prop.GetString(), out var d)) { value = d; return true; }
                    return false;
                }
                static bool TryGetDecimal(JsonElement prop, out decimal? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d)) { value = d; return true; }
                    if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var s)) { value = s; return true; }
                    return false;
                }

                // --- EmployeeId ---
                if (body.TryGetProperty("employeeId", out var empProp))
                {
                    var newEmp = GetGuidOrNull(empProp);
                    if (empProp.ValueKind != JsonValueKind.Null && newEmp is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "employeeId phải là GUID hoặc null.");
                    if (newEmp.HasValue && newEmp.Value != e.EmployeeId)
                    {
                        var exists = await _db.Employees.AnyAsync(x => x.Id == newEmp.Value, ct);
                        if (!exists) return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");
                        e.EmployeeId = newEmp.Value;
                    }
                }

                // --- Date ---
                if (body.TryGetProperty("date", out var dateProp))
                {
                    if (!TryGetDateOnly(dateProp, out var newDate))
                        return this.FAIL(StatusCodes.Status400BadRequest, "date phải là 'yyyy-MM-dd' hoặc null.");
                    if (newDate.HasValue)
                    {
                        // chống trùng employee + date
                        var conflict = await _db.Overtimes.AnyAsync(o => o.EmployeeId == e.EmployeeId && o.Date == newDate.Value && o.Id != e.Id, ct);
                        if (conflict) return this.FAIL(StatusCodes.Status409Conflict, "Đã tồn tại bản ghi tăng ca cùng ngày cho nhân viên này.");
                        e.Date = newDate.Value;
                    }
                }

                // --- Hours ---
                if (body.TryGetProperty("hours", out var hoursProp))
                {
                    if (!TryGetDecimal(hoursProp, out var newHours))
                        return this.FAIL(StatusCodes.Status400BadRequest, "hours phải là số hoặc null.");
                    if (newHours.HasValue)
                    {
                        if (newHours.Value <= 0 || newHours.Value > 24)
                            return this.FAIL(StatusCodes.Status422UnprocessableEntity, "hours phải trong (0, 24].");
                        e.Hours = newHours.Value;
                    }
                }

                // --- Rate ---
                if (body.TryGetProperty("rate", out var rateProp))
                {
                    if (!TryGetDecimal(rateProp, out var newRate))
                        return this.FAIL(StatusCodes.Status400BadRequest, "rate phải là số hoặc null.");
                    if (newRate.HasValue)
                    {
                        if (newRate.Value < 1)
                            return this.FAIL(StatusCodes.Status422UnprocessableEntity, "rate phải ≥ 1.");
                        e.Rate = newRate.Value;
                    }
                }

                // --- Reason ---
                if (body.TryGetProperty("reason", out var reasonProp))
                    e.Reason = GetStringOrNull(reasonProp);

                await _db.SaveChangesAsync(ct);

                var dto = new OvertimeDto
                {
                    Id = e.Id,
                    EmployeeId = e.EmployeeId,
                    Date = e.Date,
                    Hours = e.Hours,
                    Rate = e.Rate,
                    Reason = e.Reason
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật tăng ca thành công.",
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
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi cập nhật tăng ca.");
            }
        }

        // ========= DELETE: /api/overtimes/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.Overtimes.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy bản ghi tăng ca.");

                _db.Overtimes.Remove(e);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá tăng ca thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do ràng buộc dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá tăng ca.");
            }
        }
    }
}