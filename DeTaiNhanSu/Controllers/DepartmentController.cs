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
    public class DepartmentController : ControllerBase
    {
        //  private readonly AppDbContext _db;
        //  public DepartmentController(AppDbContext db) => _db = db;

        //  [HttpGet]
        //  public async Task<IActionResult> Search(
        //[FromQuery] string? q,
        //[FromQuery] int page = 1,
        //[FromQuery] int pageSize = 20,
        //[FromQuery] string? sort = "Name",
        //CancellationToken ct = default)
        //  {
        //      if (page < 1) page = 1;
        //      if (pageSize is < 1 or > 200) pageSize = 20;

        //      var query = _db.Departments.AsNoTracking()
        //          .Include(d => d.Manager)
        //          .Include(d => d.Employees)
        //          .AsQueryable();

        //      if (!string.IsNullOrWhiteSpace(q))
        //      {
        //          q = q.Trim();
        //          query = query.Where(d =>
        //              d.Name.Contains(q) || (d.Description != null && d.Description.Contains(q)));
        //      }

        //      query = sort?.Trim() switch
        //      {
        //          "-Name" => query.OrderByDescending(d => d.Name),
        //          _ => query.OrderBy(d => d.Name)
        //      };

        //      var total = await query.CountAsync(ct);

        //      var items = await query
        //          .Skip((page - 1) * pageSize)
        //          .Take(pageSize)
        //          .Select(d => new DepartmentDto
        //          {
        //              Id = d.Id,
        //              Name = d.Name,
        //              Description = d.Description,
        //              ManagerId = d.ManagerId,
        //              ManagerName = d.Manager != null ? d.Manager.FullName : null,
        //              EmployeesCount = d.Employees.Count
        //          })
        //          .ToListAsync(ct);

        //      return Ok(new { total, page, pageSize, items });
        //  }

        //  [HttpGet("{id:guid}")]
        //  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        //  {
        //      var d = await _db.Departments
        //          .AsNoTracking()
        //          .Include(x => x.Manager)
        //          .Include(x => x.Employees)
        //          .FirstOrDefaultAsync(x => x.Id == id, ct);

        //      if (d is null) return NotFound();

        //      var dto = new DepartmentDto
        //      {
        //          Id = d.Id,
        //          Name = d.Name,
        //          Description = d.Description,
        //          ManagerId = d.ManagerId,
        //          ManagerName = d.Manager?.FullName,
        //          EmployeesCount = d.Employees.Count
        //      };
        //      return Ok(dto);
        //  }

        //  [HttpPost]
        //  [Authorize(Roles = "HR")]
        //  public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest req, CancellationToken ct)
        //  {
        //      // unique name
        //      if (await _db.Departments.AnyAsync(x => x.Name == req.Name!, ct))
        //          return Conflict(Problem409("Duplicate Name", "Tên phòng ban đã tồn tại."));

        //      var dep = new Department
        //      {
        //          Id = Guid.NewGuid(),
        //          Name = req.Name!,
        //          Description = req.Description,
        //          ManagerId = null // set sau nếu có
        //      };
        //      _db.Departments.Add(dep);

        //      // nếu có ManagerId thì kiểm tra sau khi có dep.Id
        //      if (req.ManagerId is not null)
        //      {
        //          var ok = await ValidateManagerAsync(req.ManagerId.Value, dep.Id, ct);
        //          if (!ok.ok) return Conflict(Problem409("Invalid Manager", ok.reason!));
        //          dep.ManagerId = req.ManagerId;
        //      }

        //      await _db.SaveChangesAsync(ct);
        //      return CreatedAtAction(nameof(GetById), new { id = dep.Id }, new { dep.Id });
        //  }

        //  [HttpPut("{id:guid}")]
        //  [Authorize(Roles = "HR")]
        //  public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentRequest req, CancellationToken ct)
        //  {
        //      var dep = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct);
        //      if (dep is null)
        //      {
        //          return NotFound();
        //      }

        //      if (!string.Equals(dep.Name, req.Name, StringComparison.OrdinalIgnoreCase) && await _db.Departments.AnyAsync(x => x.Name == req.Name!, ct))
        //      {
        //          return Conflict(Problem409("Duplicate Name", "Tên phòng ban đã tồn tại"));
        //      }

        //      dep.Name = req.Name!;
        //      dep.Description = req.Description;

        //      if (req.ManagerId != dep.ManagerId)
        //      {
        //          if (req.ManagerId is null)
        //          {
        //              dep.ManagerId = null;
        //          }
        //          else
        //          {
        //              var ok = await ValidateManagerAsync(req.ManagerId.Value, dep.Id, ct);
        //              if (!ok.ok)
        //              {
        //                  return Conflict(Problem409("Invalid Manager", ok.reason!));
        //              }
        //              dep.ManagerId = req.ManagerId;
        //          }
        //      }
        //      await _db.SaveChangesAsync(ct);
        //      return NoContent();
        //  }

        //  [HttpDelete("{id:guid}")]
        //  [Authorize(Roles = "HR")]
        //  public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        //  {
        //      var dep = await _db.Departments
        //          .Include(d => d.Employees)
        //          .FirstOrDefaultAsync(x => x.Id == id, ct);
        //      if (dep is null) return NotFound();

        //      // Business rule: không xoá nếu còn nhân viên
        //      if (dep.Employees.Any())
        //          return Conflict(Problem409("Has Employees", "Không thể xoá phòng ban khi vẫn còn nhân viên trực thuộc."));

        //      _db.Departments.Remove(dep);
        //      await _db.SaveChangesAsync(ct);
        //      return NoContent();
        //  }

        // follow schema
        private readonly AppDbContext _db;
        public DepartmentController(AppDbContext db) => _db = db;

        // GET /api/department?...
        //[HttpGet]
        ////[HasPermission("Departments.View")]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> Search(
        //    [FromQuery] string? q,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20,
        //    [FromQuery] string? sort = "Name",
        //    CancellationToken ct = default)
        //{
        //    try
        //    {
        //        if (page < 1) page = 1;
        //        if (pageSize is < 1 or > 200) pageSize = 20;

        //        var query = _db.Departments.AsNoTracking()
        //            .Include(d => d.Manager)
        //            .Include(d => d.Employees)
        //            .AsQueryable();

        //        if (!string.IsNullOrWhiteSpace(q))
        //        {
        //            q = q.Trim();
        //            query = query.Where(d =>
        //                d.Name.Contains(q) || (d.Description != null && d.Description.Contains(q)));
        //        }

        //        query = sort?.Trim() switch
        //        {
        //            "-Name" => query.OrderByDescending(d => d.Name),
        //            _ => query.OrderBy(d => d.Name)
        //        };

        //        var total = await query.CountAsync(ct);

        //        var items = await query
        //            .Skip((page - 1) * pageSize)
        //            .Take(pageSize)
        //            .Select(d => new DepartmentDto
        //            {
        //                Id = d.Id,
        //                Name = d.Name,
        //                Description = d.Description,
        //                ManagerId = d.ManagerId,
        //                ManagerName = d.Manager != null ? d.Manager.FullName : null,
        //                EmployeesCount = d.Employees.Count
        //            })
        //            .ToListAsync(ct);

        //        var payload = new { total, page, pageSize, items };
        //        return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} phòng ban." : "Không có kết quả.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm phòng ban.");
        //    }
        //}

        [HttpGet]
        //[HasPermission("Departments.View")]
        [Authorize(Roles = "HR, Admin")]
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

                var query = _db.Departments.AsNoTracking()
                    .Include(d => d.Manager)
                    .Include(d => d.Employees)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(d =>
                        d.Name.Contains(q) || (d.Description != null && d.Description.Contains(q)));
                }

                query = sort?.Trim() switch
                {
                    "-Name" => query.OrderByDescending(d => d.Name),
                    _ => query.OrderBy(d => d.Name)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new DepartmentDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Description = d.Description,
                        ManagerId = d.ManagerId,
                        ManagerName = d.Manager != null ? d.Manager.FullName : null,
                        EmployeesCount = d.Employees.Count
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
                return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} phòng ban." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm phòng ban.");
            }
        }

        [HttpGet("all-id-name")]
        //[HasPermission("Departments.View")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetAllDepartments([FromQuery] string? q, CancellationToken ct = default)
        {
            try
            {
                var query = _db.Departments.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(d =>
                        d.Name.Contains(q) || (d.Description != null && d.Description.Contains(q)));
                }

                var result = await query
                    .OrderBy(d => d.Name)
                    .Select(d => new
                    {
                        departmentId = d.Id,
                        departmentName = d.Name
                    })
                    .ToListAsync(ct);

                // Không phân trang -> chỉ trả result
                var payload = new { result };
                return this.OKSingle(payload, result.Count > 0 ? $"Có {result.Count} phòng ban." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy danh sách phòng ban.");
            }
        }




        // GET /api/department/{id}
        [HttpGet("{id:guid}")]
        //[HasPermission("Departments.View")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var d = await _db.Departments
                    .AsNoTracking()
                    .Include(x => x.Manager)
                    .Include(x => x.Employees)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (d is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy phòng ban.");

                var dto = new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    ManagerId = d.ManagerId,
                    ManagerName = d.Manager?.FullName,
                    EmployeesCount = d.Employees.Count
                };
                return this.OKSingle(dto, "Lấy thông tin phòng ban thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin phòng ban.");
            }
        }

        // POST /api/department
        //[HttpPost]
        //[Authorize(Roles = "HR")]
        ////[HasPermission("Departments.Manage")]
        //public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        if (await _db.Departments.AnyAsync(x => x.Name == req.Name!, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "Tên phòng ban đã tồn tại.");

        //        var dep = new Department
        //        {
        //            Id = Guid.NewGuid(),
        //            Name = req.Name!,
        //            Description = req.Description,
        //            ManagerId = null
        //        };
        //        _db.Departments.Add(dep);

        //        if (req.ManagerId is not null)
        //        {
        //            var ok = await ValidateManagerAsync(req.ManagerId.Value, dep.Id, ct);
        //            if (!ok.ok) return this.FAIL(StatusCodes.Status409Conflict, ok.reason!);
        //            dep.ManagerId = req.ManagerId;
        //        }

        //        await _db.SaveChangesAsync(ct);

        //        // Trả 201 theo schema của bạn
        //        return StatusCode(StatusCodes.Status201Created, new
        //        {
        //            statusCode = StatusCodes.Status201Created,
        //            message = "Tạo phòng ban thành công.",
        //            data = new[] { new { dep.Id } },
        //            success = true
        //        });
        //    }
        //    catch (DbUpdateException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo phòng ban do xung đột dữ liệu.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo phòng ban.");
        //    }
        //}

        [HttpPost]
        [Authorize(Roles = "HR")]
        //[HasPermission("Departments.Manage")]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest req, CancellationToken ct)
        {
            try
            {
                // 1) Validate duy nhất theo Name
                if (await _db.Departments.AnyAsync(x => x.Name == req.Name!, ct))
                    return this.FAIL(StatusCodes.Status409Conflict, "Tên phòng ban đã tồn tại.");

                // 2) Tạo entity
                var dep = new Department
                {
                    Id = Guid.NewGuid(),
                    Name = req.Name!,
                    Description = req.Description,
                    ManagerId = null
                };
                _db.Departments.Add(dep);

                // 3) Gán Manager nếu có (sau khi đã có dep.Id)
                if (req.ManagerId is not null)
                {
                    var ok = await ValidateManagerAsync(req.ManagerId.Value, dep.Id, ct);
                    if (!ok.ok) return this.FAIL(StatusCodes.Status409Conflict, ok.reason!);
                    dep.ManagerId = req.ManagerId;
                }

                await _db.SaveChangesAsync(ct);

                // 4) LOAD LẠI đầy đủ để trả về đủ trường (Manager, EmployeesCount)
                var d = await _db.Departments
                    .AsNoTracking()
                    .Include(x => x.Manager)
                    .Include(x => x.Employees)
                    .FirstAsync(x => x.Id == dep.Id, ct);

                var dto = new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    ManagerId = d.ManagerId,
                    ManagerName = d.Manager?.FullName,
                    EmployeesCount = d.Employees.Count
                };

                // 5) Trả 201 với FULL đối tượng theo schema
                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo phòng ban thành công.",
                    data = new { result = dto },   // trả full object
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo phòng ban do xung đột dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo phòng ban.");
            }
        }


        // PUT /api/department/{id}
        //[HttpPut("{id:guid}")]
        //[Authorize(Roles = "HR")]
        ////[HasPermission("Departments.Manage")]
        //public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        var dep = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct);
        //        if (dep is null)
        //            return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy phòng ban.");

        //        if (!string.Equals(dep.Name, req.Name, StringComparison.OrdinalIgnoreCase) &&
        //            await _db.Departments.AnyAsync(x => x.Name == req.Name!, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "Tên phòng ban đã tồn tại.");

        //        dep.Name = req.Name!;
        //        dep.Description = req.Description;

        //        if (req.ManagerId != dep.ManagerId)
        //        {
        //            if (req.ManagerId is null)
        //            {
        //                dep.ManagerId = null;
        //            }
        //            else
        //            {
        //                var ok = await ValidateManagerAsync(req.ManagerId.Value, dep.Id, ct);
        //                if (!ok.ok) return this.FAIL(StatusCodes.Status409Conflict, ok.reason!);
        //                dep.ManagerId = req.ManagerId;
        //            }
        //        }

        //        await _db.SaveChangesAsync(ct);
        //        return this.OK(message: "Cập nhật phòng ban thành công.");
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật phòng ban.");
        //    }
        //}

        //[HttpPut("{id:guid}")]
        //[Authorize(Roles = "HR")]
        //public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        //{
        //    try
        //    {
        //        if (body.ValueKind != JsonValueKind.Object)
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

        //        var dep = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct);
        //        if (dep is null)
        //            return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy phòng ban.");

        //        // name
        //        if (body.TryGetProperty("name", out var nameProp))
        //        {
        //            var newName = nameProp.ValueKind == JsonValueKind.Null ? null : nameProp.GetString()?.Trim();
        //            if (string.IsNullOrWhiteSpace(newName))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Tên phòng ban không được để trống.");

        //            if (!string.Equals(dep.Name, newName, StringComparison.OrdinalIgnoreCase) &&
        //                await _db.Departments.AnyAsync(x => x.Name == newName!, ct))
        //                return this.FAIL(StatusCodes.Status409Conflict, "Tên phòng ban đã tồn tại.");

        //            dep.Name = newName!;
        //        }

        //        // description
        //        if (body.TryGetProperty("description", out var descProp))
        //        {
        //            dep.Description = descProp.ValueKind == JsonValueKind.Null ? null : descProp.GetString();
        //        }

        //        // managerId
        //        if (body.TryGetProperty("managerId", out var mgrProp))
        //        {
        //            Guid? newMgrId = mgrProp.ValueKind switch
        //            {
        //                JsonValueKind.Null => (Guid?)null,
        //                JsonValueKind.String when Guid.TryParse(mgrProp.GetString(), out var g) => g,
        //                _ => null
        //            };

        //            if (mgrProp.ValueKind != JsonValueKind.Null &&
        //                !(mgrProp.ValueKind == JsonValueKind.String && newMgrId.HasValue))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "managerId phải là GUID hoặc null.");

        //            if (newMgrId != dep.ManagerId)
        //            {
        //                if (newMgrId is null)
        //                {
        //                    dep.ManagerId = null;
        //                }
        //                else
        //                {
        //                    var ok = await ValidateManagerAsync(newMgrId.Value, dep.Id, ct);
        //                    if (!ok.ok) return this.FAIL(StatusCodes.Status409Conflict, ok.reason!);
        //                    dep.ManagerId = newMgrId;
        //                }
        //            }
        //        }

        //        await _db.SaveChangesAsync(ct);
        //        return this.OK(message: "Cập nhật phòng ban thành công.");
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật phòng ban.");
        //    }
        //}

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var dep = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (dep is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy phòng ban.");

                // name
                if (body.TryGetProperty("name", out var nameProp))
                {
                    var newName = nameProp.ValueKind == JsonValueKind.Null ? null : nameProp.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(newName))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Tên phòng ban không được để trống.");

                    if (!string.Equals(dep.Name, newName, StringComparison.OrdinalIgnoreCase) &&
                        await _db.Departments.AnyAsync(x => x.Name == newName!, ct))
                        return this.FAIL(StatusCodes.Status409Conflict, "Tên phòng ban đã tồn tại.");

                    dep.Name = newName!;
                }

                // description
                if (body.TryGetProperty("description", out var descProp))
                {
                    dep.Description = descProp.ValueKind == JsonValueKind.Null ? null : descProp.GetString();
                }

                // managerId
                if (body.TryGetProperty("managerId", out var mgrProp))
                {
                    Guid? newMgrId = mgrProp.ValueKind switch
                    {
                        JsonValueKind.Null => (Guid?)null,
                        JsonValueKind.String when Guid.TryParse(mgrProp.GetString(), out var g) => g,
                        _ => null
                    };

                    if (mgrProp.ValueKind != JsonValueKind.Null &&
                        !(mgrProp.ValueKind == JsonValueKind.String && newMgrId.HasValue))
                        return this.FAIL(StatusCodes.Status400BadRequest, "managerId phải là GUID hoặc null.");

                    if (newMgrId != dep.ManagerId)
                    {
                        if (newMgrId is null)
                        {
                            dep.ManagerId = null;
                        }
                        else
                        {
                            var ok = await ValidateManagerAsync(newMgrId.Value, dep.Id, ct);
                            if (!ok.ok) return this.FAIL(StatusCodes.Status409Conflict, ok.reason!);
                            dep.ManagerId = newMgrId;
                        }
                    }
                }

                await _db.SaveChangesAsync(ct);

                // === Load lại đầy đủ để trả về FULL object ===
                var d = await _db.Departments
                    .AsNoTracking()
                    .Include(x => x.Manager)
                    .Include(x => x.Employees)
                    .FirstAsync(x => x.Id == dep.Id, ct);

                var dto = new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    ManagerId = d.ManagerId,
                    ManagerName = d.Manager?.FullName,
                    EmployeesCount = d.Employees.Count
                };

                // 200 theo schema: data.result = dto
                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật phòng ban thành công.",
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
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật phòng ban.");
            }
        }


        // DELETE /api/department/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR")]
        //[HasPermission("Departments.Manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var dep = await _db.Departments
                    .Include(d => d.Employees)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);
                if (dep is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy phòng ban.");

                if (dep.Employees.Any())
                    return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá phòng ban khi vẫn còn nhân viên trực thuộc.");

                _db.Departments.Remove(dep);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá phòng ban thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi xoá phòng ban.");
            }
        }



        private async Task<(bool ok, string? reason)> ValidateManagerAsync(Guid managerId, Guid departmentId, CancellationToken ct)
        {
            var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == managerId);

            if (emp is null)
            {
                return (false, "Nhân viên không tồn tại");
            }

            if (emp.DepartmentId != departmentId)
            {
                return (false, "Trưởng phòng phải là nhân viên thuộc phòng ban đó");
            }

            return (true, null);
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
