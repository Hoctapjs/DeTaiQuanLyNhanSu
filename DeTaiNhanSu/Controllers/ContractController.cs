using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContractController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;
        public ContractController(AppDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }

        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] Guid? employeeId,
            [FromQuery] ContractType? type,
            [FromQuery] ContractStatus? status,
            [FromQuery] DateOnly? startFrom,
            [FromQuery] DateOnly? startTo,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = "StartDate",
            CancellationToken ct = default
            )
        {
            try
            {
                if (current < 1)
                {
                    current = 1;
                }

                if (pageSize is < 1 or > 200)
                {
                    pageSize = 20;
                }

                var query = _db.Contracts.AsNoTracking().Include(c => c.Employee).Include(c => c.Representative).AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(c =>
                        c.ContractNumber.Contains(q) ||
                        (c.Title != null && c.Title.Contains(q)) ||
                        (c.Notes != null && c.Notes.Contains(q)) ||
                        (c.Employee.FullName != null && c.Employee.FullName.Contains(q)) ||
                        (c.Employee.Code != null && c.Employee.Code.Contains(q)));
                }

                if (employeeId is not null)
                {
                    query = query.Where(c => c.EmployeeId == employeeId);
                }

                if (type is not null)
                {
                    query = query.Where(c => c.Type == type);
                }

                if (status is not null)
                {
                    query = query.Where(c => c.Status == status);
                }

                if (startFrom is not null)
                {
                    query = query.Where(c => c.StartDate >= startFrom);
                }

                if (startTo is not null)
                {
                    query = query.Where(c => c.StartDate <= startTo);
                }

                query = sort?.Trim() switch
                {
                    "-StartDate" => query.OrderByDescending(c => c.StartDate).ThenBy(c => c.ContractNumber),
                    "ContractNumber" => query.OrderBy(c => c.ContractNumber),
                    "-ContractNumber" => query.OrderByDescending(c => c.ContractNumber),
                    "Status" => query.OrderBy(c => c.Status).ThenBy(c => c.StartDate),
                    "-Status" => query.OrderByDescending(c => c.Status).ThenByDescending(c => c.StartDate),
                    _ => query.OrderBy(c => c.StartDate).ThenBy(c => c.ContractNumber)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new ContractDto
                    {
                        Id = c.Id,
                        EmployeeId = c.EmployeeId,
                        EmployeeCode = c.Employee.Code,
                        EmployeeName = c.Employee.FullName,
                        ContractNumber = c.ContractNumber,
                        Title = c.Title,
                        Type = c.Type,
                        SignedDate = c.SignedDate,
                        StartDate = c.StartDate,
                        EndDate = c.EndDate,
                        WorkType = c.WorkType,
                        BasicSalary = c.BasicSalary,
                        InsuranceSalary = c.InsuranceSalary,
                        RepresentativeId = c.RepresentativeId,
                        RepresentativeUserName = c.Representative != null ? c.Representative.UserName : null,
                        Status = c.Status,
                        AttachmentUrl = c.AttachmentUrl,
                        Notes = c.Notes,
                    }).ToListAsync();

                var meta = new
                {
                    current = current,
                    pageSize = pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total = total,
                };

                var payload = new { meta, result };
                return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} hợp đồng." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm hợp đồng.");
            }
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        //[HasPermission("Contracts.View")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var c = await _db.Contracts.AsNoTracking()
                    .Include(x => x.Employee)
                    .Include(x => x.Representative)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (c is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hợp đồng.");

                var dto = new ContractDto
                {
                    Id = c.Id,
                    EmployeeId = c.EmployeeId,
                    EmployeeCode = c.Employee.Code,
                    EmployeeName = c.Employee.FullName,
                    ContractNumber = c.ContractNumber,
                    Title = c.Title,
                    Type = c.Type,
                    SignedDate = c.SignedDate,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    WorkType = c.WorkType,
                    BasicSalary = c.BasicSalary,
                    InsuranceSalary = c.InsuranceSalary,
                    RepresentativeId = c.RepresentativeId,
                    RepresentativeUserName = c.Representative?.UserName,
                    Status = c.Status,
                    AttachmentUrl = c.AttachmentUrl,
                    Notes = c.Notes
                };

                return this.OKSingle(dto, "Lấy thông tin hợp đồng thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin hợp đồng.");
            }
        }

        // POST /api/contract
        [HttpPost]
        [Authorize(Roles = "HR, Admin")]
        //[HasPermission("Contracts.Manage")]
        public async Task<IActionResult> Create([FromBody] CreateContractRequest req, CancellationToken ct)
        {
            try
            {
                // Validate
                if (!await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct))
                    return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                if (req.RepresentativeId is not null &&
                    !await _db.Users.AnyAsync(u => u.Id == req.RepresentativeId, ct))
                    return this.FAIL(StatusCodes.Status404NotFound, "Người đại diện không tồn tại.");

                //if (await _db.Contracts.AnyAsync(c => c.ContractNumber == req.ContractNumber, ct))
                //    return this.FAIL(StatusCodes.Status409Conflict, "Số hợp đồng đã tồn tại.");

                if (req.EndDate is not null && req.EndDate < req.StartDate)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Ngày hết hạn phải >= ngày hiệu lực.");

                if (req.BasicSalary < 0 || (req.InsuranceSalary is not null && req.InsuranceSalary < 0))
                    return this.FAIL(StatusCodes.Status400BadRequest, "Mức lương không hợp lệ.");

                var contractNo = await GenerateContractNumberAsync(ct);

                var c = new Contract
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    //ContractNumber = req.ContractNumber!,
                    ContractNumber = contractNo,
                    Title = req.Title,
                    Type = req.Type,
                    SignedDate = req.SignedDate,
                    StartDate = req.StartDate,
                    EndDate = req.EndDate,
                    WorkType = req.WorkType ?? WorkType.fulltime,
                    BasicSalary = req.BasicSalary,
                    InsuranceSalary = req.InsuranceSalary,
                    RepresentativeId = req.RepresentativeId,
                    Status = req.Status,
                    AttachmentUrl = req.AttachmentUrl,
                    Notes = req.Notes
                };

                _db.Contracts.Add(c);
                await _db.SaveChangesAsync(ct);

                var emp = await _db.Employees
                    .AsNoTracking()
                    .Where(e => e.Id == c.EmployeeId)
                    .Select(e => new { e.FullName, e.Email })
                    .SingleAsync(ct);

                try
                {
                    if (!string.IsNullOrWhiteSpace(emp.Email))
                    {
                        var subject = $"[HRM] Gia hạn hợp đồng: {c.ContractNumber}";
                        //var body = $@"
                        //  <h3>Hợp đồng đã được gia hạn</h3>
                        //  <p>Nhân viên: <b>{emp.FullName}</b></p>
                        //  <p>Số HĐ: <b>{c.ContractNumber}</b></p>
                        //  <p>Ngày hiệu lực: {c.StartDate:yyyy-MM-dd}</p>
                        //  <p>Ngày hết hạn mới: <b>{c.EndDate:yyyy-MM-dd}</b></p>
                        //  <p>Trạng thái: {c.Status}</p>";
                        // Các biến dùng chung
                        string hrmUrl = "https://google.com";
                        string helpEmail = "support@huynhthanhson.io.vn";
                        string companyName = "Công Ty TNHH NPS";
                        string companyAddress = "140 Lê Trọng Tấn, Tây Thạnh, Tân Phú";

                        // 1) Email: TẠO HỢP ĐỒNG
                        var body = $@"
                            <!doctype html>
                            <html lang='vi'>
                            <head>
                              <meta charset='utf-8'>
                              <meta name='viewport' content='width=device-width, initial-scale=1'>
                              <title>Hợp đồng mới</title>
                            </head>
                            <body style='margin:0;padding:0;background:#f5f7fa;'>
                              <!-- Preheader -->
                              <div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
                                Hợp đồng mới {c.ContractNumber} đã được tạo cho {emp.FullName}.
                              </div>

                              <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                <tr>
                                  <td align='center' style='padding:24px 12px;'>
                                    <table role='presentation' width='600' cellspacing='0' cellpadding='0' style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
                                      <!-- Header -->
                                      <tr>
                                        <td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
                                          <h1 style='margin:0;font-size:20px;line-height:1.4;'>Hợp đồng mới đã được tạo</h1>
                                          <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Mã hợp đồng: {c.ContractNumber}</p>
                                        </td>
                                      </tr>

                                      <!-- Content -->
                                      <tr>
                                        <td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
                                          <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{emp.FullName}</b>,</p>
                                          <p style='margin:0 0 16px;font-size:15px;'>Hợp đồng làm việc của bạn đã được tạo. Dưới đây là thông tin tóm tắt:</p>

                                          <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                                            <tr>
                                              <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>Thông tin hợp đồng</td>
                                            </tr>
                                            <tr>
                                              <td style='padding:12px 16px;font-size:14px;line-height:1.7;'>
                                                <div><b>Số HĐ:</b> {c.ContractNumber}</div>
                                                <div><b>Tiêu đề:</b> {c.Title}</div>
                                                <div><b>Loại HĐ:</b> {c.Type}</div>
                                                <div><b>Ngày ký:</b> {(c.SignedDate == null ? "-" : c.SignedDate!.Value.ToString("yyyy-MM-dd"))}</div>
                                                <div><b>Ngày hiệu lực:</b> {c.StartDate:yyyy-MM-dd}</div>
                                                <div><b>Ngày hết hạn:</b> {(c.EndDate == null ? "-" : c.EndDate!.Value.ToString("yyyy-MM-dd"))}</div>
                                                <div><b>Trạng thái:</b> {c.Status}</div>
                                              </td>
                                            </tr>
                                          </table>

                                          <p style='margin:16px 0 0;font-size:13px;color:#334155;'>Nếu có thắc mắc, vui lòng liên hệ <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a>.</p>
                                        </td>
                                      </tr>

                                      <!-- Footer -->
                                      <tr>
                                        <td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;'>
                                          <div>{companyName} • {companyAddress}</div>
                                          <div style='margin-top:4px;'>Email hỗ trợ: <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a></div>
                                        </td>
                                      </tr>
                                    </table>

                                    <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:11px;color:#94a3b8;margin-top:12px;max-width:600px;'>
                                      Bạn nhận thư này vì có thay đổi về hợp đồng trên hệ thống HRM.
                                    </div>
                                  </td>
                                </tr>
                              </table>
                            </body>
                            </html>";

                        var to = emp.Email;
                        await _emailSender.SendAsync(to, subject, body, ct);
                    }
                }
                catch { /* không chặn nghiệp vụ nếu email lỗi */ }

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo hợp đồng thành công.",
                    data = new { result = c },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo hợp đồng do xung đột dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo hợp đồng.");
            }
        }

        //// PUT /api/contract/{id}
        //[HttpPut("{id:guid}")]
        //[Authorize(Roles = "HR, Admin")]
        ////[HasPermission("Contracts.Manage")]
        //public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
        //        if (c is null)
        //            return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hợp đồng.");

        //        if (!string.Equals(c.ContractNumber, req.ContractNumber, StringComparison.OrdinalIgnoreCase) &&
        //            await _db.Contracts.AnyAsync(x => x.ContractNumber == req.ContractNumber, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "Số hợp đồng đã tồn tại.");

        //        if (!await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct))
        //            return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

        //        if (req.RepresentativeId is not null &&
        //            !await _db.Users.AnyAsync(u => u.Id == req.RepresentativeId, ct))
        //            return this.FAIL(StatusCodes.Status404NotFound, "Người đại diện không tồn tại.");

        //        if (req.EndDate is not null && req.EndDate < req.StartDate)
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Ngày hết hạn phải >= ngày hiệu lực.");

        //        if (req.BasicSalary < 0 || (req.InsuranceSalary is not null && req.InsuranceSalary < 0))
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Mức lương không hợp lệ.");

        //        // Update fields
        //        c.EmployeeId = req.EmployeeId;
        //        c.ContractNumber = req.ContractNumber!;
        //        c.Title = req.Title;
        //        c.Type = req.Type;
        //        c.SignedDate = req.SignedDate;
        //        c.StartDate = req.StartDate;
        //        c.EndDate = req.EndDate;
        //        c.WorkType = req.WorkType ?? c.WorkType;
        //        c.BasicSalary = req.BasicSalary;
        //        c.InsuranceSalary = req.InsuranceSalary;
        //        c.RepresentativeId = req.RepresentativeId;
        //        c.Status = req.Status;
        //        c.AttachmentUrl = req.AttachmentUrl;
        //        c.Notes = req.Notes;

        //        await _db.SaveChangesAsync(ct);
        //        return this.OK(message: "Cập nhật hợp đồng thành công.");
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật hợp đồng.");
        //    }
        //}

        // PUT /api/contracts/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        //[HasPermission("Contracts.Manage")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (c is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hợp đồng.");

                // ===== Helpers =====
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
                    if (prop.ValueKind == JsonValueKind.Null) { value = null; return true; }
                    if (prop.ValueKind == JsonValueKind.String &&
                        DateOnly.TryParse(prop.GetString(), out var d)) { value = d; return true; }
                    return false;
                }

                static bool TryGetDecimal(JsonElement prop, out decimal? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) { value = null; return true; }
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d)) { value = d; return true; }
                    if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var s)) { value = s; return true; }
                    return false;
                }

                static bool TryGetEnum<TEnum>(JsonElement prop, out TEnum? value) where TEnum : struct
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) { value = null; return true; }
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i))
                    {
                        if (Enum.IsDefined(typeof(TEnum), i)) { value = (TEnum)Enum.ToObject(typeof(TEnum), i); return true; }
                        return false;
                    }
                    if (prop.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.GetString();
                        if (Enum.TryParse<TEnum>(s, true, out var e)) { value = e; return true; }
                    }
                    return false;
                }

                // ===== Thu thập giá trị muốn đổi (nếu client gửi) =====
                // Ta lưu "candidate" để có thể validate quan hệ ngày/tiền… trước khi gán vào entity
                Guid? candEmployeeId = c.EmployeeId;
                string? candContractNumber = c.ContractNumber;
                string? candTitle = c.Title;
                ContractType? candType = c.Type;
                DateOnly? candSignedDate = c.SignedDate;
                DateOnly candStartDate = c.StartDate;
                DateOnly? candEndDate = c.EndDate;
                WorkType? candWorkType = c.WorkType;
                decimal? candBasicSalary = c.BasicSalary;
                decimal? candInsuranceSalary = c.InsuranceSalary;
                Guid? candRepresentativeId = c.RepresentativeId;
                ContractStatus? candStatus = c.Status;
                string? candAttachmentUrl = c.AttachmentUrl;
                string? candNotes = c.Notes;

                // employeeId
                if (body.TryGetProperty("employeeId", out var empProp))
                {
                    var newEmp = GetGuidOrNull(empProp);
                    if (empProp.ValueKind != JsonValueKind.Null && newEmp is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "employeeId phải là GUID hoặc null.");

                    if (newEmp is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "employeeId không được null."); // business: bắt buộc

                    candEmployeeId = newEmp;
                }

                // contractNumber (unique, non-empty)
                if (body.TryGetProperty("contractNumber", out var cnProp))
                {
                    var newCN = GetStringOrNull(cnProp);
                    if (string.IsNullOrWhiteSpace(newCN))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Số hợp đồng không được để trống.");

                    // chỉ check unique khi đổi
                    if (!string.Equals(c.ContractNumber, newCN, StringComparison.OrdinalIgnoreCase) &&
                        await _db.Contracts.AnyAsync(x => x.ContractNumber == newCN, ct))
                        return this.FAIL(StatusCodes.Status409Conflict, "Số hợp đồng đã tồn tại.");

                    candContractNumber = newCN;
                }

                // title
                if (body.TryGetProperty("title", out var titleProp))
                {
                    candTitle = GetStringOrNull(titleProp); // cho phép null để xóa
                }

                // type (enum)
                if (body.TryGetProperty("type", out var typeProp))
                {
                    if (!TryGetEnum<ContractType>(typeProp, out var newType))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Giá trị type không hợp lệ.");
                    if (newType.HasValue) candType = newType.Value;
                }

                // signedDate
                if (body.TryGetProperty("signedDate", out var sdProp))
                {
                    if (!TryGetDateOnly(sdProp, out var newSigned))
                        return this.FAIL(StatusCodes.Status400BadRequest, "signedDate phải là 'yyyy-MM-dd' hoặc null.");
                    candSignedDate = newSigned;
                }

                // startDate (bắt buộc trong model, nhưng chỉ update nếu gửi)
                if (body.TryGetProperty("startDate", out var stProp))
                {
                    if (!TryGetDateOnly(stProp, out var newStart) || newStart is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "startDate phải là 'yyyy-MM-dd'.");
                    candStartDate = newStart.Value;
                }

                // endDate (nullable)
                if (body.TryGetProperty("endDate", out var edProp))
                {
                    if (!TryGetDateOnly(edProp, out var newEnd))
                        return this.FAIL(StatusCodes.Status400BadRequest, "endDate phải là 'yyyy-MM-dd' hoặc null.");
                    candEndDate = newEnd;
                }

                // workType (enum)
                if (body.TryGetProperty("workType", out var wtProp))
                {
                    if (!TryGetEnum<WorkType>(wtProp, out var newWorkType))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Giá trị workType không hợp lệ.");
                    if (newWorkType.HasValue) candWorkType = newWorkType.Value;
                }

                // basicSalary (>= 0, bắt buộc trong model nhưng chỉ update nếu gửi)
                if (body.TryGetProperty("basicSalary", out var bsProp))
                {
                    if (!TryGetDecimal(bsProp, out var newBasic) || newBasic is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "basicSalary phải là số.");
                    if (newBasic < 0) return this.FAIL(StatusCodes.Status400BadRequest, "basicSalary phải >= 0.");
                    candBasicSalary = newBasic;
                }

                // insuranceSalary (>= 0 hoặc null)
                if (body.TryGetProperty("insuranceSalary", out var insProp))
                {
                    if (!TryGetDecimal(insProp, out var newIns))
                        return this.FAIL(StatusCodes.Status400BadRequest, "insuranceSalary phải là số hoặc null.");
                    if (newIns.HasValue && newIns.Value < 0)
                        return this.FAIL(StatusCodes.Status400BadRequest, "insuranceSalary phải >= 0.");
                    candInsuranceSalary = newIns;
                }

                // representativeId (nullable, phải tồn tại user nếu có)
                if (body.TryGetProperty("representativeId", out var repProp))
                {
                    var newRep = GetGuidOrNull(repProp);
                    if (repProp.ValueKind != JsonValueKind.Null && newRep is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "representativeId phải là GUID hoặc null.");

                    candRepresentativeId = newRep;
                }

                // status (enum)
                if (body.TryGetProperty("status", out var sttProp))
                {
                    if (!TryGetEnum<ContractStatus>(sttProp, out var newStatus))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Giá trị status không hợp lệ.");
                    if (newStatus.HasValue) candStatus = newStatus.Value;
                }

                // attachmentUrl
                if (body.TryGetProperty("attachmentUrl", out var attProp))
                {
                    candAttachmentUrl = GetStringOrNull(attProp);
                }

                // notes
                if (body.TryGetProperty("notes", out var noteProp))
                {
                    candNotes = GetStringOrNull(noteProp);
                }

                // ===== Validate liên bảng / nghiệp vụ dựa trên candidate =====

                // Employee tồn tại
                if (!await _db.Employees.AnyAsync(e => e.Id == candEmployeeId, ct))
                    return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                // Representative (nếu có)
                if (candRepresentativeId is not null &&
                    !await _db.Users.AnyAsync(u => u.Id == candRepresentativeId, ct))
                    return this.FAIL(StatusCodes.Status404NotFound, "Người đại diện không tồn tại.");

                // Ngày: EndDate >= StartDate (nếu EndDate có)
                if (candEndDate is not null && candEndDate < candStartDate)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Ngày hết hạn phải >= ngày hiệu lực.");

                // ===== Apply vào entity =====
                c.EmployeeId = candEmployeeId.Value;
                c.ContractNumber = candContractNumber!;
                c.Title = candTitle;
                c.Type = candType ?? c.Type;
                c.SignedDate = candSignedDate;
                c.StartDate = candStartDate;
                c.EndDate = candEndDate;
                c.WorkType = candWorkType ?? c.WorkType;
                c.BasicSalary = candBasicSalary ?? c.BasicSalary;
                c.InsuranceSalary = candInsuranceSalary;
                c.RepresentativeId = candRepresentativeId;
                c.Status = candStatus ?? c.Status;
                c.AttachmentUrl = candAttachmentUrl;
                c.Notes = candNotes;

                await _db.SaveChangesAsync(ct);

                // === Load lại FULL để trả về ===
                var cvm = await _db.Contracts
                    .AsNoTracking()
                    .Include(x => x.Employee)
                    .Include(x => x.Representative)
                    .FirstAsync(x => x.Id == c.Id, ct);

                var dto = new
                {
                    id = cvm.Id,
                    employeeId = cvm.EmployeeId,
                    employeeName = cvm.Employee.FullName,
                    contractNumber = cvm.ContractNumber,
                    title = cvm.Title,
                    type = cvm.Type.ToString(),
                    signedDate = cvm.SignedDate,
                    startDate = cvm.StartDate,
                    endDate = cvm.EndDate,
                    workType = cvm.WorkType.ToString(),
                    basicSalary = cvm.BasicSalary,
                    insuranceSalary = cvm.InsuranceSalary,
                    representativeId = cvm.RepresentativeId,
                    representativeUsername = cvm.Representative?.UserName,
                    status = cvm.Status.ToString(),
                    attachmentUrl = cvm.AttachmentUrl,
                    notes = cvm.Notes
                };

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật hợp đồng thành công.",
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
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật hợp đồng.");
            }
        }


        // DELETE /api/contract/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        //[HasPermission("Contracts.Manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (c is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hợp đồng.");

                _db.Contracts.Remove(c);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá hợp đồng thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi xoá hợp đồng.");
            }
        }

        // gia hạn -> cập nhật ngày hết hạn và trạng thái của hợp đồng
        [HttpPost("{id:guid}/renew")]
        [Authorize(Roles = "HR, Admin")]
        //[HasPermission("Contracts.Manage")]
        public async Task<IActionResult> Renew(Guid id, [FromBody] RenewContractRequest req, CancellationToken ct)
        {
            try
            {
                var c = await _db.Contracts.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id, ct);
                if (c is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hợp đồng.");

                if (req.EndDate is null || req.EndDate < c.StartDate)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Ngày hết hạn phải ≥ ngày hiệu lực.");

                c.EndDate = req.EndDate;
                c.Status = ContractStatus.active; // tuỳ rule
                c.Notes = string.Join(Environment.NewLine, new[] { c.Notes, req.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));

                await _db.SaveChangesAsync(ct);

                // Gửi email (nếu cần)
                var emp = await _db.Employees
                    .AsNoTracking()
                    .Where(e => e.Id == c.EmployeeId)
                    .Select(e => new { e.FullName, e.Email })
                    .SingleAsync(ct);

                try
                {
                    if (!string.IsNullOrWhiteSpace(emp.Email))
                    {
                        var subject = $"[HRM] Gia hạn hợp đồng: {c.ContractNumber}";
                        //var body = $@"
                        //  <h3>Hợp đồng đã được gia hạn</h3>
                        //  <p>Nhân viên: <b>{emp.FullName}</b></p>
                        //  <p>Số HĐ: <b>{c.ContractNumber}</b></p>
                        //  <p>Ngày hiệu lực: {c.StartDate:yyyy-MM-dd}</p>
                        //  <p>Ngày hết hạn mới: <b>{c.EndDate:yyyy-MM-dd}</b></p>
                        //  <p>Trạng thái: {c.Status}</p>";
                        // Các biến dùng chung
                        string hrmUrl = "https://google.com";
                        string helpEmail = "support@huynhthanhson.io.vn";
                        string companyName = "Công Ty TNHH NPS";
                        string companyAddress = "140 Lê Trọng Tấn, Tây Thạnh, Tân Phú";

                        // 1) Email: TẠO HỢP ĐỒNG
                        // 2) Email: GIA HẠN HỢP ĐỒNG
                        var body = $@"
                            <!doctype html>
                            <html lang='vi'>
                            <head>
                              <meta charset='utf-8'>
                              <meta name='viewport' content='width=device-width, initial-scale=1'>
                              <title>Gia hạn hợp đồng</title>
                            </head>
                            <body style='margin:0;padding:0;background:#f5f7fa;'>
                              <div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
                                Hợp đồng {c.ContractNumber} đã được gia hạn cho {emp.FullName}.
                              </div>

                              <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                <tr>
                                  <td align='center' style='padding:24px 12px;'>
                                    <table role='presentation' width='600' cellspacing='0' cellpadding='0' style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
                                      <tr>
                                        <td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
                                          <h1 style='margin:0;font-size:20px;line-height:1.4;'>Gia hạn hợp đồng</h1>
                                          <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Mã hợp đồng: {c.ContractNumber}</p>
                                        </td>
                                      </tr>

                                      <tr>
                                        <td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
                                          <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{emp.FullName}</b>,</p>
                                          <p style='margin:0 0 16px;font-size:15px;'>Hợp đồng của bạn đã được <b>gia hạn</b>. Vui lòng xem thông tin bên dưới.</p>

                                          <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                                            <tr>
                                              <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>Chi tiết gia hạn</td>
                                            </tr>
                                            <tr>
                                              <td style='padding:12px 16px;font-size:14px;line-height:1.7;'>
                                                <div><b>Số HĐ:</b> {c.ContractNumber}</div>
                                                <div><b>Ngày hiệu lực:</b> {c.StartDate:yyyy-MM-dd}</div>
                                                <div><b>Ngày hết hạn mới:</b> {(c.EndDate == null ? "-" : c.EndDate!.Value.ToString("yyyy-MM-dd"))}</div>
                                                <div><b>Trạng thái:</b> {c.Status}</div>
                                                {(string.IsNullOrWhiteSpace(c.Notes) ? "" : $"<div><b>Ghi chú:</b> {System.Net.WebUtility.HtmlEncode(c.Notes)}</div>")}
                                              </td>
                                            </tr>
                                          </table>

                                          <div style='margin:12px 0 0;'>
                                            <a href='{hrmUrl}' style='background:#2563eb;text-decoration:none;color:#fff;padding:10px 16px;border-radius:8px;display:inline-block;font-weight:600;'>Xem trên HRM</a>
                                          </div>

                                          <p style='margin:16px 0 0;font-size:13px;color:#334155;'>Cần hỗ trợ? Liên hệ <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a>.</p>
                                        </td>
                                      </tr>

                                      <tr>
                                        <td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;'>
                                          <div>{companyName} • {companyAddress}</div>
                                          <div style='margin-top:4px;'>Email hỗ trợ: <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a></div>
                                        </td>
                                      </tr>
                                    </table>

                                    <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:11px;color:#94a3b8;margin-top:12px;max-width:600px;'>
                                      Bạn nhận thư này vì có thay đổi về hợp đồng trên hệ thống HRM.
                                    </div>
                                  </td>
                                </tr>
                              </table>
                            </body>
                            </html>";


                        var to = emp.Email;
                        await _emailSender.SendAsync(to, subject, body, ct);
                    }
                }
                catch { /* không chặn nghiệp vụ nếu email lỗi */ }

                return this.OK(message: "Gia hạn hợp đồng thành công.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi gia hạn hợp đồng."); }
        }

        // chấm dứt -> đánh dấu hợp đồng đã chấm dứt
        [HttpPost("{id:guid}/terminate")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Terminate(Guid id, [FromBody] TerminateContractRequest req, CancellationToken ct)
        {
            try
            {
                var c = await _db.Contracts.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id, ct);
                if (c is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hợp đồng.");

                c.Status = ContractStatus.terminated;
                c.EndDate ??= req.TerminatedAt ?? DateOnly.FromDateTime(DateTime.UtcNow);
                c.Notes = string.Join(Environment.NewLine,
                    new[] { c.Notes, $"Terminate: {req.Reason}" }.Where(s => !string.IsNullOrWhiteSpace(s)));

                await _db.SaveChangesAsync(ct);

                // Lấy thông tin email nhân viên
                var emp = await _db.Employees.AsNoTracking()
                    .Where(e => e.Id == c.EmployeeId)
                    .Select(e => new { e.FullName, e.Email })
                    .SingleAsync(ct);

                try
                {
                    if (!string.IsNullOrWhiteSpace(emp.Email))
                    {
                        var subject = $"[HRM] Chấm dứt hợp đồng: {c.ContractNumber}";

                        // Biến chung
                        string hrmUrl = "https://google.com";
                        string helpEmail = "support@huynhthanhson.io.vn";
                        string companyName = "Công Ty TNHH NPS";
                        string companyAddress = "140 Lê Trọng Tấn, Tây Thạnh, Tân Phú";

                        // render lý do (encode để an toàn)
                        var reasonHtml = !string.IsNullOrWhiteSpace(req?.Reason)
                            ? $"<div><b>Lý do:</b> {System.Net.WebUtility.HtmlEncode(req.Reason)}</div>"
                            : "";

                        var body = $@"
                        <!doctype html>
                        <html lang='vi'>
                        <head>
                          <meta charset='utf-8'>
                          <meta name='viewport' content='width=device-width, initial-scale=1'>
                          <title>Chấm dứt hợp đồng</title>
                        </head>
                        <body style='margin:0;padding:0;background:#f5f7fa;'>
                          <div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
                            Hợp đồng {c.ContractNumber} đã bị chấm dứt.
                          </div>
                          <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                            <tr>
                              <td align='center' style='padding:24px 12px;'>
                                <table role='presentation' width='600' cellspacing='0' cellpadding='0' style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
                                  <tr>
                                    <td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
                                      <h1 style='margin:0;font-size:20px;line-height:1.4;'>Thông báo chấm dứt hợp đồng</h1>
                                      <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Mã hợp đồng: {c.ContractNumber}</p>
                                    </td>
                                  </tr>
                                  <tr>
                                    <td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
                                      <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{emp.FullName}</b>,</p>
                                      <p style='margin:0 0 16px;font-size:15px;'>Hợp đồng của bạn đã được <b>chấm dứt</b>. Thông tin tóm tắt:</p>
                                      <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                                        <tr>
                                          <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>Chi tiết chấm dứt</td>
                                        </tr>
                                        <tr>
                                          <td style='padding:12px 16px;font-size:14px;line-height:1.7;'>
                                            <div><b>Số HĐ:</b> {c.ContractNumber}</div>
                                            <div><b>Ngày hiệu lực:</b> {c.StartDate:yyyy-MM-dd}</div>
                                            <div><b>Ngày chấm dứt:</b> {(c.EndDate?.ToString("yyyy-MM-dd") ?? "-")}</div>
                                            <div><b>Trạng thái:</b> {c.Status}</div>
                                            {reasonHtml}
                                          </td>
                                        </tr>
                                      </table>
                                      <p style='margin:0 0 8px;font-size:13px;color:#334155;'>
                                        Mọi câu hỏi vui lòng liên hệ <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a>.
                                      </p>
                                    </td>
                                  </tr>
                                  <tr>
                                    <td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;'>
                                      <div>{companyName} • {companyAddress}</div>
                                      <div style='margin-top:4px;'>Email hỗ trợ: <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a></div>
                                    </td>
                                  </tr>
                                </table>
                                <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:11px;color:#94a3b8;margin-top:12px;max-width:600px;'>
                                  Bạn nhận thư này vì có thay đổi về hợp đồng trên hệ thống HRM.
                                </div>
                              </td>
                            </tr>
                          </table>
                        </body>
                        </html>";

                        await _emailSender.SendAsync(emp.Email, subject, body, ct);
                    }
                }
                catch { /* không chặn nghiệp vụ nếu email lỗi */ }

                return this.OK(message: "Đã chấm dứt hợp đồng.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi chấm dứt hợp đồng.");
            }
        }


        // danh sách sắp hết hạn -> trả về danh sách các hợp đồng sẽ hết hạn trong 30 ngày
        [HttpGet("expiring")]
        [Authorize(Roles = "HR, Admin")]
        //[HasPermission("Contracts.View")]
        public async Task<IActionResult> GetExpiring([FromQuery] int withinDays = 30, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            try
            {
                if (withinDays < 1) withinDays = 30;
                if (page < 1) page = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                var until = today.AddDays(withinDays);

                var q = _db.Contracts.AsNoTracking()
                    .Include(c => c.Employee)
                    .Where(c => c.Status != ContractStatus.terminated &&
                                c.EndDate != null &&
                                c.EndDate >= today &&
                                c.EndDate <= until);

                var total = await q.CountAsync(ct);
                var result = await q.OrderBy(c => c.EndDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        id = c.Id,
                        employee_id = c.EmployeeId,
                        employee_name = c.Employee.FullName,
                        contract_number = c.ContractNumber,
                        end_date = c.EndDate,
                        status = c.Status.ToString().ToLower()
                    }).ToListAsync(ct);

                var meta = new { current = page, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize), total };
                return this.OKSingle(new { meta, result }, total > 0 ? $"Có {total} HĐ sắp hết hạn trong {withinDays} ngày." : "Không có hợp đồng sắp hết hạn.");
            }
            catch { return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy danh sách sắp hết hạn."); }
        }

        private async Task<string> GenerateContractNumberAsync(CancellationToken ct)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NEXT VALUE FOR dbo.DocSeq";
            var obj = await cmd.ExecuteScalarAsync(ct);   // <-- không bị wrap subquery
            var next = Convert.ToInt32(obj);

            return $"HD-{next:000000}";
        }


    }
}
