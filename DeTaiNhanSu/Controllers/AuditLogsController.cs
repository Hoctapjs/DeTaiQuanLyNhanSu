using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Infrastructure.Auditing;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [SkipAudit]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AuditLogsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        [Authorize(Roles ="Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] Guid? userId,
            [FromQuery] string? action,
            [FromQuery] string? tableName,
            [FromQuery] string? recordId,
            [FromQuery] string? ip,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = "-CreatedAt",
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

                var query = _db.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var v = q.Trim();
                    query = query.Where(a => a.Action.Contains(v) || (a.Description != null && a.Description.Contains(v)) || (a.RecordId != null && a.RecordId.Contains(v)) || (a.TableName != null && a.TableName.Contains(v)));
                }

                // filter theo userid
                if (userId is not null)
                {
                    query = query.Where(a => a.UserId == userId);
                }

                // filter theo tablename
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    var v = tableName.Trim();
                    query = query.Where(a => a.TableName != null && a.TableName.Contains(v));
                }

                //filter theo recordid
                if (!string.IsNullOrWhiteSpace(recordId))
                {
                    var v = recordId.Trim();
                    query = query.Where(a => a.RecordId != null && a.RecordId.Contains(v));
                }

                //filter theo ip
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    var v = ip.Trim();
                    query = query.Where(a => a.IPAddress != null && a.IPAddress.Contains(v));
                }

                if (from is not null)
                {
                    query = query.Where(a => a.CreatedAt >= from);
                }

                if (to is not null)
                {
                    query = query.Where(a => a.CreatedAt <= to);
                }

                query = (sort?.Trim()) switch
                {
                    "CreatedAt" => query.OrderBy(a => a.CreatedAt),
                    "-CreatedAt" => query.OrderByDescending(a => a.CreatedAt),
                    "Action" => query.OrderBy(a => a.Action).ThenByDescending(a => a.CreatedAt),
                    "-Action" => query.OrderByDescending(a => a.Action).ThenByDescending(a => a.CreatedAt),
                    _ => query.OrderByDescending(a => a.CreatedAt),
                };

                var total = await query.CountAsync(ct);

                var result = await query.Skip((current - 1) * pageSize).Take(pageSize).Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = a.User.UserName,
                    Action = a.Action,
                    TableName = a.TableName,
                    RecordId = a.RecordId,
                    Description = a.Description,
                    IpAddress = a.IPAddress,
                    CreatedAt = a.CreatedAt,
                }).ToListAsync();

                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                var payload = new { meta, result };

                return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} audit logs" : "Không có kết quả");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm audit logs");
            }
        }

        //[HttpPost]
        //[Authorize(Roles = "Admin")]
        //public async Task<IActionResult> Create(
        //    [FromBody] CreateAuditLogRequest req, CancellationToken ct = default
        //    )
        //{
        //    try
        //    {
        //        if (req.UserId == Guid.Empty || string.IsNullOrWhiteSpace(req.Action))
        //        {
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Thiếu UserId hoặc Action");
        //        }

        //        var existUser = await _db.Users.AnyAsync(u => u.Id == req.UserId, ct);

        //        if (!existUser)
        //        {
        //            return this.FAIL(StatusCodes.Status400BadRequest, "User không tồn tại");
        //        }

        //        var log = new AuditLog
        //        {
        //            Id = Guid.NewGuid(),
        //            UserId = req.UserId,
        //            Action = req.Action,
        //            TableName = string.IsNullOrWhiteSpace(req.TableName) ? null : req.TableName,
        //            RecordId = string.IsNullOrWhiteSpace(req.RecordId) ? null : req.RecordId,
        //            Description = req.Description,
        //            IPAddress = string.IsNullOrWhiteSpace(req.IpAddress)
        //                ? HttpContext.Connection.RemoteIpAddress?.ToString()
        //                : req.IpAddress!.Trim(),
        //            CreatedAt = DateTime.UtcNow,
        //        };

        //        _db.AuditLogs.Add(log);
        //        await _db.SaveChangesAsync(ct);

        //        var a = await _db.AuditLogs.AsNoTracking().Include(x => x.User).FirstAsync(x => x.Id == log.Id, ct);

        //        var dto = new AuditLogDto
        //        {
        //            Id = a.Id,
        //            UserId = a.UserId,
        //            UserName = a.User.UserName,
        //            Action = a.Action,
        //            TableName = a.TableName,
        //            RecordId= a.RecordId,
        //            Description = a.Description,
        //            IpAddress = a.IPAddress,
        //            CreatedAt = a.CreatedAt
        //        };

        //        return StatusCode(StatusCodes.Status201Created, new
        //        {
        //            StatusCode = StatusCodes.Status201Created,
        //            message = "Tạo audit log thành công",
        //            data = new[] {dto },
        //            success = true
        //        });
        //    }
        //    catch (DbUpdateException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo audit log do xung đột dữ liệu");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tạo audit log");
        //    }
        //}

    }
}
