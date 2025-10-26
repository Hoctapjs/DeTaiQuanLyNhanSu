using System.Text.Json;
using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.RewardPenaltyTypeDtoFol;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeTaiNhanSu.Enums;


namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class RewardPenaltyTypesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public RewardPenaltyTypesController(AppDbContext db) => _db = db;

        // ========= GET: /api/rewardpenaltytypes?current=&pageSize=&q=&type=&level=&form=&sort=
        [HttpGet]
        //[HasPermission("RewardPenaltyTypes.View")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] RewardPenaltyKind? type,
            [FromQuery] SeverityLevel? level,
            [FromQuery] ActionForm? form,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.RewardPenaltyTypes.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var qn = q.Trim();
                    query = query.Where(x =>
                        x.Name.Contains(qn) ||
                        (x.Description != null && x.Description.Contains(qn)));
                }
                if (type is not null) query = query.Where(x => x.Type == type);
                if (level is not null) query = query.Where(x => x.Level == level);
                if (form is not null) query = query.Where(x => x.Form == form);

                query = sort?.Trim() switch
                {
                    "-Name" => query.OrderByDescending(x => x.Name),
                    "Name" => query.OrderBy(x => x.Name),
                    "-DefaultAmount" => query.OrderByDescending(x => x.DefaultAmount).ThenBy(x => x.Name),
                    "DefaultAmount" => query.OrderBy(x => x.DefaultAmount).ThenBy(x => x.Name),
                    "-Level" => query.OrderByDescending(x => x.Level).ThenBy(x => x.Name),
                    "Level" => query.OrderBy(x => x.Level).ThenBy(x => x.Name),
                    _ => query.OrderBy(x => x.Name)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new RewardPenaltyTypeDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Type = x.Type.ToString(),
                        DefaultAmount = x.DefaultAmount,
                        Level = x.Level.ToString(),
                        Form = x.Form.ToString(),
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
                    total > 0 ? $"Tìm thấy {total} loại KT/KL." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm loại khen thưởng/kỷ luật.");
            }
        }

        // ========= GET: /api/rewardpenaltytypes/all?q=&type=&level=&form=&sort=
        [HttpGet("all")]
        //[HasPermission("RewardPenaltyTypes.View")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? q,
            [FromQuery] RewardPenaltyKind? type,
            [FromQuery] SeverityLevel? level,
            [FromQuery] ActionForm? form,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            try
            {
                var query = _db.RewardPenaltyTypes.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var qn = q.Trim();
                    query = query.Where(x =>
                        x.Name.Contains(qn) ||
                        (x.Description != null && x.Description.Contains(qn)));
                }
                if (type is not null) query = query.Where(x => x.Type == type);
                if (level is not null) query = query.Where(x => x.Level == level);
                if (form is not null) query = query.Where(x => x.Form == form);

                query = sort?.Trim() switch
                {
                    "-Name" => query.OrderByDescending(x => x.Name),
                    "Name" => query.OrderBy(x => x.Name),
                    "-DefaultAmount" => query.OrderByDescending(x => x.DefaultAmount).ThenBy(x => x.Name),
                    "DefaultAmount" => query.OrderBy(x => x.DefaultAmount).ThenBy(x => x.Name),
                    "-Level" => query.OrderByDescending(x => x.Level).ThenBy(x => x.Name),
                    "Level" => query.OrderBy(x => x.Level).ThenBy(x => x.Name),
                    _ => query.OrderBy(x => x.Name)
                };

                var result = await query.Select(x => new RewardPenaltyTypeDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Type = x.Type.ToString(),
                    DefaultAmount = x.DefaultAmount,
                    Level = x.Level.ToString(),
                    Form = x.Form.ToString(),
                    Description = x.Description
                }).ToListAsync(ct);

                var total = result.Count;
                var meta = new { current = 1, pageSize = total, pages = 1, total };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Có {total} loại KT/KL." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy danh sách loại khen thưởng/kỷ luật.");
            }
        }

        // ========= GET: /api/rewardpenaltytypes/{id}
        [HttpGet("{id:guid}")]
        //[HasPermission("RewardPenaltyTypes.View")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var x = await _db.RewardPenaltyTypes.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == id, ct);

                if (x is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy loại KT/KL.");

                var dto = new RewardPenaltyTypeDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Type = x.Type.ToString(),
                    DefaultAmount = x.DefaultAmount,
                    Level = x.Level.ToString(),
                    Form = x.Form.ToString(),
                    Description = x.Description
                };

                return this.OKSingle(dto, "Lấy thông tin thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy thông tin loại khen thưởng/kỷ luật.");
            }
        }

        // ========= POST: /api/rewardpenaltytypes
        [HttpPost]
        //[HasPermission("RewardPenaltyTypes.Manage")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Create([FromBody] CreateRewardPenaltyTypeRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null || string.IsNullOrWhiteSpace(req.Name))
                    return this.FAIL(StatusCodes.Status400BadRequest, "Tên không được để trống.");

                var dup = await _db.RewardPenaltyTypes
                    .AnyAsync(x => x.Name.ToLower() == req.Name.Trim().ToLower(), ct);
                if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Tên loại đã tồn tại.");

                if (req.Type is null) return this.FAIL(StatusCodes.Status400BadRequest, "Thiếu trường 'type'.");
                if (req.DefaultAmount is not null && req.DefaultAmount < 0)
                    return this.FAIL(StatusCodes.Status422UnprocessableEntity, "DefaultAmount phải ≥ 0.");

                var entity = new RewardPenaltyType
                {
                    Id = Guid.NewGuid(),
                    Name = req.Name.Trim(),
                    Type = req.Type.Value, // enum của bạn
                    DefaultAmount = req.DefaultAmount,
                    Level = req.Level ?? default,
                    Form = req.Form ?? default,
                    Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim()
                };

                _db.RewardPenaltyTypes.Add(entity);
                await _db.SaveChangesAsync(ct);

                var dto = new RewardPenaltyTypeDto
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Type = entity.Type.ToString(),
                    DefaultAmount = entity.DefaultAmount,
                    Level = entity.Level.ToString(),
                    Form = entity.Form.ToString(),
                    Description = entity.Description
                };

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo loại khen thưởng/kỷ luật thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo do xung đột dữ liệu (trùng/ràng buộc).");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo loại khen thưởng/kỷ luật.");
            }
        }

        // ========= PUT (partial): /api/rewardpenaltytypes/{id}
        [HttpPut("{id:guid}")]
        //[HasPermission("RewardPenaltyTypes.Manage")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var e = await _db.RewardPenaltyTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy loại KT/KL.");

                static string? GetStringOrNull(JsonElement prop) =>
                    prop.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => string.IsNullOrWhiteSpace(prop.GetString()) ? null : prop.GetString()!.Trim(),
                        _ => null
                    };

                static bool TryGetDecimal(JsonElement prop, out decimal? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d)) { value = d; return true; }
                    if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var s)) { value = s; return true; }
                    return false;
                }

                static bool TryGetInt(JsonElement prop, out int? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) { value = i; return true; }
                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var j)) { value = j; return true; }
                    return false;
                }

                static bool TryGetEnum<TEnum>(JsonElement prop, out TEnum? value) where TEnum : struct
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(TEnum), prop.GetInt32()))
                    { value = (TEnum)Enum.ToObject(typeof(TEnum), prop.GetInt32()); return true; }
                    if (prop.ValueKind == JsonValueKind.String && Enum.TryParse<TEnum>(prop.GetString(), true, out var v))
                    { value = v; return true; }
                    return false;
                }

                // --- Name (unique, non-empty) ---
                if (body.TryGetProperty("name", out var nameProp))
                {
                    var newName = GetStringOrNull(nameProp);
                    if (string.IsNullOrWhiteSpace(newName))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Tên không được để trống.");

                    var dup = await _db.RewardPenaltyTypes
                        .AnyAsync(x => x.Id != id && x.Name.ToLower() == newName.ToLower(), ct);
                    if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Tên loại đã tồn tại.");

                    e.Name = newName!;
                }

                // --- Type (enum int) ---
                if (body.TryGetProperty("type", out var typeProp))
                {
                    if (!TryGetEnum<RewardPenaltyKind>(typeProp, out var newType))
                        return this.FAIL(StatusCodes.Status400BadRequest, "type phải là enum hợp lệ hoặc null.");
                    if (newType.HasValue) e.Type = newType.Value;
                }

                // --- DefaultAmount (>= 0, nullable) ---
                if (body.TryGetProperty("defaultAmount", out var amtProp))
                {
                    if (!TryGetDecimal(amtProp, out var newAmt))
                        return this.FAIL(StatusCodes.Status400BadRequest, "defaultAmount phải là số hoặc null.");
                    if (newAmt.HasValue && newAmt.Value < 0)
                        return this.FAIL(StatusCodes.Status422UnprocessableEntity, "defaultAmount phải ≥ 0.");
                    e.DefaultAmount = newAmt;
                }

                // --- Level (int?); Form (int?)
                if (body.TryGetProperty("level", out var levelProp))
                {
                    if (!TryGetEnum<SeverityLevel>(levelProp, out var newLevel))
                        return this.FAIL(StatusCodes.Status400BadRequest, "level phải là enum hợp lệ hoặc null.");
                    if (newLevel.HasValue) e.Level = newLevel.Value;
                }
                if (body.TryGetProperty("form", out var formProp))
                {
                    if (!TryGetEnum<ActionForm>(formProp, out var newForm))
                        return this.FAIL(StatusCodes.Status400BadRequest, "form phải là enum hợp lệ hoặc null.");
                    if (newForm.HasValue) e.Form = newForm.Value;
                }

                // --- Description (string? allow null to clear) ---
                if (body.TryGetProperty("description", out var descProp))
                    e.Description = GetStringOrNull(descProp);

                await _db.SaveChangesAsync(ct);

                // Trả FULL object vừa cập nhật
                var full = await _db.RewardPenaltyTypes.AsNoTracking().FirstAsync(x => x.Id == e.Id, ct);
                var dto = new RewardPenaltyTypeDto
                {
                    Id = full.Id,
                    Name = full.Name,
                    Type = full.Type.ToString(),
                    DefaultAmount = full.DefaultAmount,
                    Level = full.Level.ToString(),
                    Form = full.Form.ToString(),
                    Description = full.Description
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật loại khen thưởng/kỷ luật thành công.",
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
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi cập nhật loại khen thưởng/kỷ luật.");
            }
        }

        // ========= GET: /api/rewardpenaltytypes/filter?fields=...&q=...&type=... (selective fields)
        //[HttpGet("filter")]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> SelectiveSearch(
        //    [FromQuery] string fields,
        //    [FromQuery] string? q,
        //    [FromQuery] RewardPenaltyKind? type,
        //    [FromQuery] SeverityLevel? level,
        //    [FromQuery] ActionForm? form,
        //    [FromQuery] int current = 1,
        //    [FromQuery] int pageSize = 20,
        //    [FromQuery] string? sort = null,
        //    CancellationToken ct = default)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(fields))
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Thiếu tham số 'fields'.");

        //        if (current < 1) current = 1;
        //        if (pageSize is < 1 or > 200) pageSize = 20;

        //        var reqFields = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        //                              .Select(f => f.ToLowerInvariant())
        //                              .Distinct()
        //                              .ToList();

        //        var allowed = new HashSet<string>(new[]
        //        {
        //            "id","name","type","defaultamount","level","form","description"
        //        });
        //        var invalid = reqFields.Where(f => !allowed.Contains(f)).ToList();
        //        if (invalid.Count > 0)
        //            return this.FAIL(StatusCodes.Status400BadRequest, $"Trường không hợp lệ: {string.Join(", ", invalid)}");

        //        var query = _db.RewardPenaltyTypes.AsNoTracking().AsQueryable();

        //        if (!string.IsNullOrWhiteSpace(q))
        //        {
        //            var qn = q.Trim();
        //            query = query.Where(x =>
        //                x.Name.Contains(qn) ||
        //                (x.Description != null && x.Description.Contains(qn)));
        //        }
        //        if (type is not null) query = query.Where(x => x.Type == type);
        //        if (level is not null) query = query.Where(x => x.Level == level);
        //        if (form is not null) query = query.Where(x => x.Form == form);

        //        query = sort?.Trim() switch
        //        {
        //            "-Name" => query.OrderByDescending(x => x.Name),
        //            "Name" => query.OrderBy(x => x.Name),
        //            "-DefaultAmount" => query.OrderByDescending(x => x.DefaultAmount).ThenBy(x => x.Name),
        //            "DefaultAmount" => query.OrderBy(x => x.DefaultAmount).ThenBy(x => x.Name),
        //            "-Level" => query.OrderByDescending(x => x.Level).ThenBy(x => x.Name),
        //            "Level" => query.OrderBy(x => x.Level).ThenBy(x => x.Name),
        //            _ => query.OrderBy(x => x.Name)
        //        };

        //        var total = await query.CountAsync(ct);

        //        // 1) EF chỉ select đúng cột
        //        var projected = ProjectTypes(query, reqFields);

        //        // 2) Materialize
        //        var list = await projected
        //            .Skip((current - 1) * pageSize)
        //            .Take(pageSize)
        //            .ToListAsync(ct);

        //        // 3) Shape output (ẩn field không chọn)
        //        var result = list.Select(x =>
        //        {
        //            var o = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;

        //            void Add(string key, object? val)
        //            {
        //                if (reqFields.Contains(key)) o[key switch
        //                {
        //                    "defaultamount" => "defaultAmount",
        //                    _ => key
        //                }] = val;
        //            }

        //            Add("id", x.Id);
        //            Add("name", x.Name);
        //            Add("type", x.Type);
        //            Add("defaultamount", x.DefaultAmount);
        //            Add("level", x.Level);
        //            Add("form", x.Form);
        //            Add("description", x.Description);
        //            return (object)o;
        //        }).ToList();

        //        var meta = new
        //        {
        //            current,
        //            pageSize,
        //            pages = (int)Math.Ceiling(total / (double)pageSize),
        //            total
        //        };

        //        return this.OKSingle(new { meta, result }, total > 0 ? $"Tìm thấy {total} loại KT/KL." : "Không có kết quả.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi truy vấn danh sách loại khen thưởng/kỷ luật.");
        //    }
        //}

        // ========= DELETE: /api/rewardpenaltytypes/{id}
        [HttpDelete("{id:guid}")]
        //[HasPermission("RewardPenaltyTypes.Manage")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.RewardPenaltyTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy loại KT/KL.");

                // Khuyến nghị: chặn xóa nếu đang được tham chiếu
                var inUse = await _db.RewardPenalties.AnyAsync(rp => rp.TypeId == id, ct);
                if (inUse)
                    return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá vì đang được tham chiếu bởi quyết định KT/KL.");

                _db.RewardPenaltyTypes.Remove(e);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá loại khen thưởng/kỷ luật thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do ràng buộc dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá loại khen thưởng/kỷ luật.");
            }
        }

        // ===== Helper: Project selective columns (IQueryable -> IQueryable<TypeFlat>)
        private static IQueryable<TypeFlat> ProjectTypes(IQueryable<RewardPenaltyType> source, List<string> fields)
        {
            var p = System.Linq.Expressions.Expression.Parameter(typeof(RewardPenaltyType), "p");
            static System.Linq.Expressions.Expression Prop(System.Linq.Expressions.Expression obj, string name)
                => System.Linq.Expressions.Expression.Property(obj, name);
            static System.Linq.Expressions.Expression Conv(System.Linq.Expressions.Expression exp, Type to)
                => System.Linq.Expressions.Expression.Convert(exp, to);

            var dtoType = typeof(TypeFlat);
            var bindings = new List<System.Linq.Expressions.MemberBinding>();
            void Bind(string field, System.Linq.Expressions.Expression valueExp)
            {
                var prop = dtoType.GetProperty(field);
                bindings.Add(System.Linq.Expressions.Expression.Bind(prop!, valueExp));
            }

            foreach (var f in fields)
            {
                switch (f)
                {
                    case "id": Bind(nameof(TypeFlat.Id), Prop(p, nameof(RewardPenaltyType.Id))); break;
                    case "name": Bind(nameof(TypeFlat.Name), Prop(p, nameof(RewardPenaltyType.Name))); break;
                    case "type": Bind(nameof(TypeFlat.Type), Conv(Prop(p, nameof(RewardPenaltyType.Type)), typeof(int?))); break;
                    case "defaultamount": Bind(nameof(TypeFlat.DefaultAmount), Prop(p, nameof(RewardPenaltyType.DefaultAmount))); break;
                    case "level": Bind(nameof(TypeFlat.Level), Conv(Prop(p, nameof(RewardPenaltyType.Level)), typeof(int?))); break;
                    case "form": Bind(nameof(TypeFlat.Form), Conv(Prop(p, nameof(RewardPenaltyType.Form)), typeof(int?))); break;
                    case "description": Bind(nameof(TypeFlat.Description), Prop(p, nameof(RewardPenaltyType.Description))); break;
                }
            }

            var init = System.Linq.Expressions.Expression.MemberInit(
                System.Linq.Expressions.Expression.New(dtoType), bindings);

            var selector = System.Linq.Expressions.Expression.Lambda<Func<RewardPenaltyType, TypeFlat>>(init, p);
            return source.Select(selector);
        }
    }
}
