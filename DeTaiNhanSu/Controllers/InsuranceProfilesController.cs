using DeTaiNhanSu.Common;               // this.OK / this.FAIL
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.InsuranceProfileDtoFol;
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
    public sealed class InsuranceProfilesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public InsuranceProfilesController(AppDbContext db) => _db = db;

        // ========= GET: /api/insuranceprofiles?employeeId=&hasBhxh=&hasBhyt=&hasBhtn=&current=&pageSize=&sort=
        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] Guid? employeeId,
            [FromQuery] bool? hasBhxh,
            [FromQuery] bool? hasBhyt,
            [FromQuery] bool? hasBhtn,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.InsuranceProfiles.AsNoTracking().AsQueryable();

                if (employeeId is not null) query = query.Where(x => x.EmployeeId == employeeId);
                if (hasBhxh is true) query = query.Where(x => x.Bhxh != null);
                if (hasBhxh is false) query = query.Where(x => x.Bhxh == null);
                if (hasBhyt is true) query = query.Where(x => x.Bhyt != null);
                if (hasBhyt is false) query = query.Where(x => x.Bhyt == null);
                if (hasBhtn is true) query = query.Where(x => x.Bhtn != null);
                if (hasBhtn is false) query = query.Where(x => x.Bhtn == null);

                query = sort?.Trim() switch
                {
                    "-Bhxh" => query.OrderByDescending(x => x.Bhxh).ThenBy(x => x.EmployeeId),
                    "Bhxh" => query.OrderBy(x => x.Bhxh).ThenBy(x => x.EmployeeId),
                    "-Bhyt" => query.OrderByDescending(x => x.Bhyt).ThenBy(x => x.EmployeeId),
                    "Bhyt" => query.OrderBy(x => x.Bhyt).ThenBy(x => x.EmployeeId),
                    "-Bhtn" => query.OrderByDescending(x => x.Bhtn).ThenBy(x => x.EmployeeId),
                    "Bhtn" => query.OrderBy(x => x.Bhtn).ThenBy(x => x.EmployeeId),
                    "-EmployeeId" => query.OrderByDescending(x => x.EmployeeId),
                    "EmployeeId" => query.OrderBy(x => x.EmployeeId),
                    _ => query.OrderBy(x => x.EmployeeId)
                };

                var total = await query.CountAsync(ct);

                var page = query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize);

                var result = await
                (
                    from i in page
                    join e0 in _db.Employees.AsNoTracking() on i.EmployeeId equals e0.Id into ej
                    from e in ej.DefaultIfEmpty()
                    join d0 in _db.Departments.AsNoTracking() on e!.DepartmentId equals d0.Id into dj
                    from d in dj.DefaultIfEmpty()
                    join p0 in _db.Positions.AsNoTracking() on e!.PositionId equals p0.Id into pj
                    from p in pj.DefaultIfEmpty()
                    select new InsuranceProfileDto
                    {
                        Id = i.Id,
                        EmployeeId = i.EmployeeId,
                        EmployeeFullName = e != null ? e.FullName : null,
                        DepartmentId = e != null ? e.DepartmentId : null,
                        DepartmentName = d != null ? d.Name : null,
                        PositionId = e != null ? e.PositionId : null,
                        PositionName = p != null ? p.Name : null,
                        Bhxh = i.Bhxh,
                        Bhyt = i.Bhyt,
                        Bhtn = i.Bhtn
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
                    total > 0 ? $"Tìm thấy {total} hồ sơ bảo hiểm." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm hồ sơ bảo hiểm.");
            }
        }

        // ========= GET: /api/insuranceprofiles/all?employeeId=&hasBhxh=&hasBhyt=&hasBhtn=&sort=
        [HttpGet("all")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? employeeId,
            [FromQuery] bool? hasBhxh,
            [FromQuery] bool? hasBhyt,
            [FromQuery] bool? hasBhtn,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                var query = _db.InsuranceProfiles.AsNoTracking().AsQueryable();

                if (employeeId is not null) query = query.Where(x => x.EmployeeId == employeeId);
                if (hasBhxh is true) query = query.Where(x => x.Bhxh != null);
                if (hasBhxh is false) query = query.Where(x => x.Bhxh == null);
                if (hasBhyt is true) query = query.Where(x => x.Bhyt != null);
                if (hasBhyt is false) query = query.Where(x => x.Bhyt == null);
                if (hasBhtn is true) query = query.Where(x => x.Bhtn != null);
                if (hasBhtn is false) query = query.Where(x => x.Bhtn == null);

                query = sort?.Trim() switch
                {
                    "-Bhxh" => query.OrderByDescending(x => x.Bhxh).ThenBy(x => x.EmployeeId),
                    "Bhxh" => query.OrderBy(x => x.Bhxh).ThenBy(x => x.EmployeeId),
                    "-Bhyt" => query.OrderByDescending(x => x.Bhyt).ThenBy(x => x.EmployeeId),
                    "Bhyt" => query.OrderBy(x => x.Bhyt).ThenBy(x => x.EmployeeId),
                    "-Bhtn" => query.OrderByDescending(x => x.Bhtn).ThenBy(x => x.EmployeeId),
                    "Bhtn" => query.OrderBy(x => x.Bhtn).ThenBy(x => x.EmployeeId),
                    "-EmployeeId" => query.OrderByDescending(x => x.EmployeeId),
                    "EmployeeId" => query.OrderBy(x => x.EmployeeId),
                    _ => query.OrderBy(x => x.EmployeeId)
                };

                //var result = await query
                //    .Select(x => new InsuranceProfileDto
                //    {
                //        Id = x.Id,
                //        EmployeeId = x.EmployeeId,
                //        Bhxh = x.Bhxh,
                //        Bhyt = x.Bhyt,
                //        Bhtn = x.Bhtn
                //    })
                //    .ToListAsync(ct);

                var result = await
                (
                    from i in query
                    join e0 in _db.Employees.AsNoTracking() on i.EmployeeId equals e0.Id into ej
                    from e in ej.DefaultIfEmpty()
                    join d0 in _db.Departments.AsNoTracking() on e!.DepartmentId equals d0.Id into dj
                    from d in dj.DefaultIfEmpty()
                    join p0 in _db.Positions.AsNoTracking() on e!.PositionId equals p0.Id into pj
                    from p in pj.DefaultIfEmpty()
                    select new InsuranceProfileDto
                    {
                        Id = i.Id,
                        EmployeeId = i.EmployeeId,
                        EmployeeFullName = e != null ? e.FullName : null,
                        DepartmentId = e != null ? e.DepartmentId : null,
                        DepartmentName = d != null ? d.Name : null,
                        PositionId = e != null ? e.PositionId : null,
                        PositionName = p != null ? p.Name : null,
                        Bhxh = i.Bhxh,
                        Bhyt = i.Bhyt,
                        Bhtn = i.Bhtn
                    }
                ).ToListAsync(ct);  


                var total = result.Count;
                var meta = new { current = 1, pageSize = total, pages = 1, total };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Có {total} hồ sơ bảo hiểm." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy danh sách hồ sơ bảo hiểm.");
            }
        }

        // ========= GET: /api/insuranceprofiles/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var x = await _db.InsuranceProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == id, ct);

                if (x is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ bảo hiểm.");

                //var dto = new InsuranceProfileDto
                //{
                //    Id = x.Id,
                //    EmployeeId = x.EmployeeId,
                //    Bhxh = x.Bhxh,
                //    Bhyt = x.Bhyt,
                //    Bhtn = x.Bhtn
                //};

                var dto = await
                (
                    from i in _db.InsuranceProfiles.AsNoTracking().Where(z => z.Id == id)
                    join e0 in _db.Employees.AsNoTracking() on i.EmployeeId equals e0.Id into ej
                    from e in ej.DefaultIfEmpty()
                    join d0 in _db.Departments.AsNoTracking() on e!.DepartmentId equals d0.Id into dj
                    from d in dj.DefaultIfEmpty()
                    join p0 in _db.Positions.AsNoTracking() on e!.PositionId equals p0.Id into pj
                    from p in pj.DefaultIfEmpty()
                    select new InsuranceProfileDto
                    {
                        Id = i.Id,
                        EmployeeId = i.EmployeeId,
                        EmployeeFullName = e != null ? e.FullName : null,
                        DepartmentId = e != null ? e.DepartmentId : null,
                        DepartmentName = d != null ? d.Name : null,
                        PositionId = e != null ? e.PositionId : null,
                        PositionName = p != null ? p.Name : null,
                        Bhxh = i.Bhxh,
                        Bhyt = i.Bhyt,
                        Bhtn = i.Bhtn
                    }
                ).FirstOrDefaultAsync(ct);

                if (dto is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ bảo hiểm.");

                return this.OKSingle(dto, "Lấy thông tin thành công.");


                return this.OKSingle(dto, "Lấy thông tin thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy thông tin hồ sơ bảo hiểm.");
            }
        }

        // ========= POST: /api/insuranceprofiles
        [HttpPost]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Create([FromBody] CreateInsuranceProfileRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                // Validate FK
                var empExists = await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct);
                if (!empExists) return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                // 1–1: mỗi Employee chỉ có 1 hồ sơ
                var dup = await _db.InsuranceProfiles.AnyAsync(x => x.EmployeeId == req.EmployeeId, ct);
                if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Nhân viên đã có hồ sơ bảo hiểm.");

                // validate số không âm
                if (req.Bhxh is not null && req.Bhxh < 0) return this.FAIL(StatusCodes.Status422UnprocessableEntity, "BHXH phải ≥ 0.");
                if (req.Bhyt is not null && req.Bhyt < 0) return this.FAIL(StatusCodes.Status422UnprocessableEntity, "BHYT phải ≥ 0.");
                if (req.Bhtn is not null && req.Bhtn < 0) return this.FAIL(StatusCodes.Status422UnprocessableEntity, "BHTN phải ≥ 0.");

                var entity = new InsuranceProfile
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    Bhxh = req.Bhxh,
                    Bhyt = req.Bhyt,
                    Bhtn = req.Bhtn
                };

                _db.InsuranceProfiles.Add(entity);
                await _db.SaveChangesAsync(ct);

                var dto = new InsuranceProfileDto
                {
                    Id = entity.Id,
                    EmployeeId = entity.EmployeeId,
                    Bhxh = entity.Bhxh,
                    Bhyt = entity.Bhyt,
                    Bhtn = entity.Bhtn
                };

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo hồ sơ bảo hiểm thành công.",
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
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo hồ sơ bảo hiểm.");
            }
        }

        // ========= PUT (partial): /api/insuranceprofiles/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var e = await _db.InsuranceProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ bảo hiểm.");

                // helpers
                static Guid? GetGuidOrNull(JsonElement prop)
                {
                    if (prop.ValueKind == JsonValueKind.Null) return null;
                    if (prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var g)) return g;
                    return null;
                }
                static bool TryGetDecimal(JsonElement prop, out decimal? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d)) { value = d; return true; }
                    if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var s)) { value = s; return true; }
                    return false;
                }

                // --- EmployeeId (đổi chủ sở hữu hồ sơ): kiểm tra 1–1 ---
                if (body.TryGetProperty("employeeId", out var empProp))
                {
                    var newEmp = GetGuidOrNull(empProp);
                    if (empProp.ValueKind != JsonValueKind.Null && newEmp is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "employeeId phải là GUID hoặc null.");
                    if (newEmp.HasValue && newEmp.Value != e.EmployeeId)
                    {
                        var exists = await _db.Employees.AnyAsync(x => x.Id == newEmp.Value, ct);
                        if (!exists) return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                        var owned = await _db.InsuranceProfiles.AnyAsync(x => x.EmployeeId == newEmp.Value && x.Id != e.Id, ct);
                        if (owned) return this.FAIL(StatusCodes.Status409Conflict, "Nhân viên này đã có hồ sơ bảo hiểm.");
                        e.EmployeeId = newEmp.Value;
                    }
                }

                // --- Bhxh ---
                if (body.TryGetProperty("bhxh", out var bhxhProp))
                {
                    if (!TryGetDecimal(bhxhProp, out var v))
                        return this.FAIL(StatusCodes.Status400BadRequest, "bhxh phải là số hoặc null.");
                    if (v.HasValue && v.Value < 0)
                        return this.FAIL(StatusCodes.Status422UnprocessableEntity, "bhxh phải ≥ 0.");
                    e.Bhxh = v;
                }

                // --- Bhyt ---
                if (body.TryGetProperty("bhyt", out var bhytProp))
                {
                    if (!TryGetDecimal(bhytProp, out var v))
                        return this.FAIL(StatusCodes.Status400BadRequest, "bhyt phải là số hoặc null.");
                    if (v.HasValue && v.Value < 0)
                        return this.FAIL(StatusCodes.Status422UnprocessableEntity, "bhyt phải ≥ 0.");
                    e.Bhyt = v;
                }

                // --- Bhtn ---
                if (body.TryGetProperty("bhtn", out var bhtnProp))
                {
                    if (!TryGetDecimal(bhtnProp, out var v))
                        return this.FAIL(StatusCodes.Status400BadRequest, "bhtn phải là số hoặc null.");
                    if (v.HasValue && v.Value < 0)
                        return this.FAIL(StatusCodes.Status422UnprocessableEntity, "bhtn phải ≥ 0.");
                    e.Bhtn = v;
                }

                await _db.SaveChangesAsync(ct);

                var dto = new InsuranceProfileDto
                {
                    Id = e.Id,
                    EmployeeId = e.EmployeeId,
                    Bhxh = e.Bhxh,
                    Bhyt = e.Bhyt,
                    Bhtn = e.Bhtn
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật hồ sơ bảo hiểm thành công.",
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
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi cập nhật hồ sơ bảo hiểm.");
            }
        }

        // ========= DELETE: /api/insuranceprofiles/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.InsuranceProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ bảo hiểm.");

                _db.InsuranceProfiles.Remove(e);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá hồ sơ bảo hiểm thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do ràng buộc dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá hồ sơ bảo hiểm.");
            }
        }
    }
}