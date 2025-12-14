using DeTaiNhanSu.Common;               // this.OK / this.FAIL
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.RewardPenaltyDtoFol;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Auth;
using DeTaiNhanSu.Services.Notification;
using DeTaiNhanSu.Services.Scope;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class RewardPenaltiesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IDataScopeService _dataScope;
        private readonly INotificationService _notificationService;


        public RewardPenaltiesController(
              AppDbContext db,
              IDataScopeService dataScope,
              INotificationService notificationService)
        {
            _db = db;
            _dataScope = dataScope;
            _notificationService = notificationService;
        }

        // ========= GET: /api/rewardpenalties?employeeId=&typeId=&kind=&from=&to=&decidedBy=&current=&pageSize=&sort=
        [HttpGet]
        [Authorize(Roles = "HR, Manager")]
        public async Task<IActionResult> Search(
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? typeId,
            [FromQuery] RewardPenaltyKind? kind,      // lọc theo Type.Type (Reward/Penalty)
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? decidedBy,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var allowedDeptId = await _dataScope.GetAllowedDepartmentIdAsync(null, ct);


                var query = _db.RewardPenalties
                    .AsNoTracking()
                    .Include(x => x.Type) // để lọc kind + lấy DefaultAmount/Name
                    .Include(x => x.Employee)
                    .AsQueryable();

                if (employeeId is not null) query = query.Where(x => x.EmployeeId == employeeId);
                if (typeId is not null) query = query.Where(x => x.TypeId == typeId);
                if (kind is not null) query = query.Where(x => x.Type!.Type == kind);
                if (from is not null) query = query.Where(x => x.DecidedAt >= from);
                if (to is not null) query = query.Where(x => x.DecidedAt <= to);
                if (decidedBy is not null) query = query.Where(x => x.DecidedBy == decidedBy);

                // filter manager
                if (allowedDeptId.HasValue)
                {
                    query = query.Where(x => x.Employee.DepartmentId == allowedDeptId.Value);
                }

                query = sort?.Trim() switch
                {
                    "-DecidedAt" => query.OrderByDescending(x => x.DecidedAt).ThenBy(x => x.Id),
                    "DecidedAt" => query.OrderBy(x => x.DecidedAt).ThenBy(x => x.Id),
                    "-Amount" => query.OrderByDescending(x => (x.AmountOverride ?? x.Type!.DefaultAmount) ?? 0).ThenBy(x => x.DecidedAt),
                    "Amount" => query.OrderBy(x => (x.AmountOverride ?? x.Type!.DefaultAmount) ?? 0).ThenBy(x => x.DecidedAt),
                    _ => query.OrderByDescending(x => x.DecidedAt).ThenBy(x => x.Id)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new RewardPenaltyDto
                    {
                        Id = x.Id,
                        EmployeeId = x.EmployeeId,
                        EmployeeFullName = x.Employee != null ? x.Employee.FullName : null,
                        EmployeeAvatarUrl = x.Employee != null ? x.Employee.AvatarUrl : null,
                        TypeId = x.TypeId,
                        TypeName = x.Type != null ? x.Type.Name : null,
                        Kind = x.Type.Type.ToString(),
                        DefaultAmount = x.Type != null ? x.Type.DefaultAmount : null,
                        AmountOverride = x.AmountOverride,
                        FinalAmount = (x.AmountOverride ?? (x.Type != null ? x.Type.DefaultAmount : null)),
                        CustomReason = x.CustomReason,
                        DecidedAt = x.DecidedAt,
                        DecidedBy = x.DecidedBy
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
                    total > 0 ? $"Tìm thấy {total} quyết định KT/KL." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm quyết định KT/KL.");
            }
        }

        // ========= GET: /api/rewardpenalties/all?employeeId=&typeId=&kind=&from=&to=&decidedBy=&sort=
        [HttpGet("all")]
        [Authorize(Roles = "HR, Manager")]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? typeId,
            [FromQuery] RewardPenaltyKind? kind,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? decidedBy,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                var allowedDeptId = await _dataScope.GetAllowedDepartmentIdAsync(null, ct);

                var query = _db.RewardPenalties
                    .AsNoTracking()
                    .Include(x => x.Type)
                    .AsQueryable();

                if (employeeId is not null) query = query.Where(x => x.EmployeeId == employeeId);
                if (typeId is not null) query = query.Where(x => x.TypeId == typeId);
                if (kind is not null) query = query.Where(x => x.Type!.Type == kind);
                if (from is not null) query = query.Where(x => x.DecidedAt >= from);
                if (to is not null) query = query.Where(x => x.DecidedAt <= to);
                if (decidedBy is not null) query = query.Where(x => x.DecidedBy == decidedBy);

                // filter manager
                if (allowedDeptId.HasValue)
                {
                    query = query.Where(x => x.Employee.DepartmentId == allowedDeptId.Value);
                }


                query = sort?.Trim() switch
                {
                    "-DecidedAt" => query.OrderByDescending(x => x.DecidedAt).ThenBy(x => x.Id),
                    "DecidedAt" => query.OrderBy(x => x.DecidedAt).ThenBy(x => x.Id),
                    "-Amount" => query.OrderByDescending(x => (x.AmountOverride ?? x.Type!.DefaultAmount) ?? 0).ThenBy(x => x.DecidedAt),
                    "Amount" => query.OrderBy(x => (x.AmountOverride ?? x.Type!.DefaultAmount) ?? 0).ThenBy(x => x.DecidedAt),
                    _ => query.OrderByDescending(x => x.DecidedAt).ThenBy(x => x.Id)
                };

                var result = await query.Select(x => new RewardPenaltyDto
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    TypeId = x.TypeId,
                    TypeName = x.Type != null ? x.Type.Name : null,
                    Kind = x.Type.Type.ToString(),
                    DefaultAmount = x.Type != null ? x.Type.DefaultAmount : null,
                    AmountOverride = x.AmountOverride,
                    FinalAmount = (x.AmountOverride ?? (x.Type != null ? x.Type.DefaultAmount : null)),
                    CustomReason = x.CustomReason,
                    DecidedAt = x.DecidedAt,
                    DecidedBy = x.DecidedBy
                }).ToListAsync(ct);

                var total = result.Count;
                var meta = new { current = 1, pageSize = total, pages = 1, total };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Có {total} quyết định KT/KL." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy danh sách quyết định KT/KL.");
            }
        }

        // ========= GET: /api/rewardpenalties/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Manager")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var x = await _db.RewardPenalties
                    .AsNoTracking()
                    .Include(r => r.Type)
                    .FirstOrDefaultAsync(r => r.Id == id, ct);

                if (x is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy quyết định.");

                var dto = new RewardPenaltyDto
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    TypeId = x.TypeId,
                    TypeName = x.Type?.Name,
                    Kind = x.Type.Type.ToString(),
                    DefaultAmount = x.Type?.DefaultAmount,
                    AmountOverride = x.AmountOverride,
                    FinalAmount = x.AmountOverride ?? x.Type?.DefaultAmount,
                    CustomReason = x.CustomReason,
                    DecidedAt = x.DecidedAt,
                    DecidedBy = x.DecidedBy
                };

                return this.OKSingle(dto, "Lấy thông tin thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy thông tin quyết định KT/KL.");
            }
        }

        // ========= POST: /api/rewardpenalties
        [HttpPost]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Create([FromBody] CreateRewardPenaltyRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                // 1. Validate dữ liệu (Giữ nguyên)
                var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == req.EmployeeId, ct); // Lấy thông tin NV để lấy tên nếu cần
                if (emp == null) return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                var type = await _db.RewardPenaltyTypes.FirstOrDefaultAsync(t => t.Id == req.TypeId, ct);
                if (type is null) return this.FAIL(StatusCodes.Status404NotFound, "Loại KT/KL không tồn tại.");

                var userExists = await _db.Users.AnyAsync(u => u.Id == req.DecidedBy, ct);
                if (!userExists) return this.FAIL(StatusCodes.Status404NotFound, "Người quyết định không tồn tại.");

                if (req.AmountOverride is not null && req.AmountOverride < 0)
                    return this.FAIL(StatusCodes.Status422UnprocessableEntity, "AmountOverride phải ≥ 0.");

                var finalAmount = req.AmountOverride ?? type.DefaultAmount;
                if (finalAmount is null)
                    return this.FAIL(StatusCodes.Status422UnprocessableEntity, "Không xác định được số tiền.");

                if (req.DecidedAt is null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Thiếu ngày quyết định (decidedAt).");

                // 2. Tạo Entity RewardPenalty (Giữ nguyên)
                var entity = new RewardPenalty
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    TypeId = req.TypeId,
                    AmountOverride = req.AmountOverride,
                    CustomReason = string.IsNullOrWhiteSpace(req.CustomReason) ? null : req.CustomReason!.Trim(),
                    DecidedAt = req.DecidedAt!.Value,
                    DecidedBy = req.DecidedBy
                };

                _db.RewardPenalties.Add(entity);

                // =================================================================================
                //  LOGIC MỚI 1: CẬP NHẬT ATTENDANCE (Thêm ghi chú vào ngày DecidedAt)
                // =================================================================================
                var attendance = await _db.Attendances
                    .FirstOrDefaultAsync(a => a.EmployeeId == req.EmployeeId && a.Date == req.DecidedAt.Value, ct);

                if (attendance != null)
                {
                    // Tạo nội dung ghi chú: "Thưởng: Lý do..." hoặc "Phạt: Lý do..."
                    string kindLabel = type.Type == RewardPenaltyKind.reward ? "Thưởng" : "Phạt";
                    string reasonText = !string.IsNullOrWhiteSpace(req.CustomReason) ? req.CustomReason : type.Name;

                    string noteToAdd = $" | {kindLabel}: {reasonText}";

                    // Nối thêm vào ghi chú có sẵn (nếu có)
                    if (string.IsNullOrEmpty(attendance.Note))
                    {
                        attendance.Note = $"{kindLabel}: {reasonText}";
                    }
                    else
                    {
                        attendance.Note += noteToAdd;
                    }
                    // Không cần _db.Attendances.Update(attendance) vì EF Core tự track thay đổi
                }
                // =================================================================================

                // 3. Lưu vào DB
                await _db.SaveChangesAsync(ct);

                // =================================================================================
                //  LOGIC MỚI 2: GỬI THÔNG BÁO (NOTIFICATION)
                // =================================================================================
                // Cần lấy UserId từ EmployeeId để gửi thông báo
                var userId = await _db.Users
                    .Where(u => u.EmployeeId == req.EmployeeId)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync(ct);

                if (userId != Guid.Empty)
                {
                    // Chạy task gửi thông báo ngầm (không await để trả về response nhanh hơn, 
                    // hoặc await nếu muốn đảm bảo gửi xong mới báo thành công)
                    await _notificationService.SendRewardPenaltyNotificationAsync(
                        targetUserId: userId,
                        typeName: type.Name,
                        kind: type.Type.ToString(), // "reward" hoặc "penalty"
                        amount: finalAmount.Value,
                        reason: req.CustomReason,
                        date: req.DecidedAt.Value
                    );
                }
                // =================================================================================

                // 4. Trả về kết quả (Giữ nguyên)
                var full = await _db.RewardPenalties
                    .AsNoTracking()
                    .Include(r => r.Type)
                    .FirstAsync(r => r.Id == entity.Id, ct);

                var dto = new RewardPenaltyDto
                {
                    Id = full.Id,
                    EmployeeId = full.EmployeeId,
                    TypeId = full.TypeId,
                    TypeName = full.Type?.Name,
                    Kind = full.Type.Type.ToString(),
                    DefaultAmount = full.Type?.DefaultAmount,
                    AmountOverride = full.AmountOverride,
                    FinalAmount = full.AmountOverride ?? full.Type?.DefaultAmount,
                    CustomReason = full.CustomReason,
                    DecidedAt = full.DecidedAt,
                    DecidedBy = full.DecidedBy
                };

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo quyết định KT/KL thành công (đã cập nhật chấm công & gửi thông báo).",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (Exception ex) // Bắt Exception chung để debug tốt hơn
            {
                // Console.WriteLine(ex); // Log lỗi nếu cần
                return this.FAIL(StatusCodes.Status500InternalServerError, $"Lỗi: {ex.Message}");
            }
        }

        // ========= PUT (partial): /api/rewardpenalties/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var e = await _db.RewardPenalties.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy quyết định.");

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

                // --- TypeId ---
                if (body.TryGetProperty("typeId", out var typeProp))
                {
                    var newTypeId = GetGuidOrNull(typeProp);
                    if (typeProp.ValueKind != JsonValueKind.Null && newTypeId is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "typeId phải là GUID hoặc null.");
                    if (newTypeId.HasValue && newTypeId.Value != e.TypeId)
                    {
                        var exists = await _db.RewardPenaltyTypes.AnyAsync(t => t.Id == newTypeId.Value, ct);
                        if (!exists) return this.FAIL(StatusCodes.Status404NotFound, "Loại KT/KL không tồn tại.");
                        e.TypeId = newTypeId.Value;
                    }
                }

                // --- AmountOverride (>=0 or null) ---
                if (body.TryGetProperty("amountOverride", out var amtProp))
                {
                    if (!TryGetDecimal(amtProp, out var newAmt))
                        return this.FAIL(StatusCodes.Status400BadRequest, "amountOverride phải là số hoặc null.");
                    if (newAmt.HasValue && newAmt.Value < 0)
                        return this.FAIL(StatusCodes.Status422UnprocessableEntity, "amountOverride phải ≥ 0.");
                    e.AmountOverride = newAmt;
                }

                // --- CustomReason (allow null) ---
                if (body.TryGetProperty("customReason", out var reasonProp))
                    e.CustomReason = GetStringOrNull(reasonProp);

                // --- DecidedAt (DateOnly, required; nếu null -> giữ nguyên) ---
                if (body.TryGetProperty("decidedAt", out var decidedProp))
                {
                    if (!TryGetDateOnly(decidedProp, out var newDate))
                        return this.FAIL(StatusCodes.Status400BadRequest, "decidedAt phải là 'yyyy-MM-dd' hoặc null.");
                    if (newDate.HasValue) e.DecidedAt = newDate.Value;
                }

                // --- DecidedBy (Guid; validate user) ---
                if (body.TryGetProperty("decidedBy", out var decidedByProp))
                {
                    var newUser = GetGuidOrNull(decidedByProp);
                    if (decidedByProp.ValueKind != JsonValueKind.Null && newUser is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "decidedBy phải là GUID hoặc null.");
                    if (newUser.HasValue && newUser.Value != e.DecidedBy)
                    {
                        var exists = await _db.Users.AnyAsync(u => u.Id == newUser.Value, ct);
                        if (!exists) return this.FAIL(StatusCodes.Status404NotFound, "Người quyết định không tồn tại.");
                        e.DecidedBy = newUser.Value;
                    }
                }

                await _db.SaveChangesAsync(ct);

                // trả full
                var full = await _db.RewardPenalties
                    .AsNoTracking()
                    .Include(r => r.Type)
                    .FirstAsync(r => r.Id == e.Id, ct);

                var dto = new RewardPenaltyDto
                {
                    Id = full.Id,
                    EmployeeId = full.EmployeeId,
                    TypeId = full.TypeId,
                    TypeName = full.Type?.Name,
                    Kind = full.Type.Type.ToString(),
                    DefaultAmount = full.Type?.DefaultAmount,
                    AmountOverride = full.AmountOverride,
                    FinalAmount = full.AmountOverride ?? full.Type?.DefaultAmount,
                    CustomReason = full.CustomReason,
                    DecidedAt = full.DecidedAt,
                    DecidedBy = full.DecidedBy
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật quyết định KT/KL thành công.",
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
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi cập nhật quyết định KT/KL.");
            }
        }

        // ========= DELETE: /api/rewardpenalties/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.RewardPenalties.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy quyết định.");

                _db.RewardPenalties.Remove(e);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá quyết định KT/KL thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do ràng buộc dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá quyết định KT/KL.");
            }
        }

    }
}