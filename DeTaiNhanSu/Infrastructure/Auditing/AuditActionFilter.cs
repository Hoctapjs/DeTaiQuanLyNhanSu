using System.Diagnostics;
using System.Security.Claims;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DeTaiNhanSu.Infrastructure.Auditing
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SkipAuditAttribute : Attribute { }
    public class AuditActionFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _db;

        public AuditActionFilter(AppDbContext db)
        {
            _db = db;
        }

        // BẮT BUỘC: triển khai đúng chữ ký interface
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Bỏ qua audit nếu có attribute hoặc chính endpoint audit
            var path = context.HttpContext.Request.Path.Value ?? "";
            var skip = context.ActionDescriptor.EndpointMetadata.OfType<SkipAuditAttribute>().Any()
                       || path.StartsWith("/api/auditlogs", StringComparison.OrdinalIgnoreCase);

            if (skip)
            {
                await next();
                return;
            }

            var sw = Stopwatch.StartNew();
            var executed = await next(); // thực thi action
            sw.Stop();

            try
            {
                var http = executed.HttpContext;

                Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
                var method = http.Request.Method;
                var query = http.Request.QueryString.Value ?? "";
                var ip = http.Connection.RemoteIpAddress?.ToString();
                var status = http.Response.StatusCode;
                var ua = http.Request.Headers.UserAgent.ToString();

                var log = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId == Guid.Empty ? SystemUser() : userId,
                    Action = $"{method} {path}",
                    TableName = null,
                    RecordId = null,
                    Description = $"Status={status}; Elapsed={sw.ElapsedMilliseconds}ms; Query={query}; UA={ua}",
                    IPAddress = ip,
                    CreatedAt = DateTime.UtcNow
                };

                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Không để audit làm lỗi request chính
            }
        }

        private static Guid SystemUser() => Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
