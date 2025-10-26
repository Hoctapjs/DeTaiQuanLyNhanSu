using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using DeTaiNhanSu.Services.Log;

namespace DeTaiNhanSu.Infrastructure.Auditing
{
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IAuditScope _scope;
        private readonly IHttpContextAccessor _http;

        public AuditSaveChangesInterceptor(IHttpContextAccessor http, IAuditScope scope)
        {
            _http = http;
            _scope = scope;
        }

        //public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        //    DbContextEventData eventData,
        //    InterceptionResult<int> result,
        //    CancellationToken cancellationToken = default)
        //{
        //    var http = _http.HttpContext;

        //    if (_scope.Suppress || http?.User?.Identity?.IsAuthenticated != true)
        //    {
        //        return await ValueTask.FromResult(result);
        //    }

        //    if (eventData.Context is not AppDbContext ctx)
        //        return await base.SavingChangesAsync(eventData, result, cancellationToken);

        //    Guid.TryParse(http?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        //    var ip = http?.Connection.RemoteIpAddress?.ToString();

        //    var logs = new List<AuditLog>();

        //    foreach (var entry in ctx.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        //    {
        //        // Không tự ghi khi đang thêm/chỉnh sửa chính bảng AuditLogs
        //        if (entry.Entity is AuditLog) continue;

        //        var tableName = entry.Metadata.GetTableName();
        //        var recordId = GetPrimaryKeyValue(entry);
        //        string action = entry.State switch
        //        {
        //            EntityState.Added => "INSERT",
        //            EntityState.Modified => "UPDATE",
        //            EntityState.Deleted => "DELETE",
        //            _ => "UNKNOWN"
        //        };

        //        // Nếu muốn giới hạn chỉ 1 số bảng, có thể filter ở đây:
        //        // var allow = new HashSet<string> { "Employees", "Contracts", "Users", "Departments", "Positions", "Roles" };
        //        // if (!allow.Contains(tableName)) continue;

        //        string? description = null;

        //        if (entry.State == EntityState.Modified)
        //        {
        //            var diffs = entry.Properties
        //                .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
        //                .ToDictionary(
        //                    p => p.Metadata.Name,
        //                    p => new
        //                    {
        //                        before = Normalize(p.OriginalValue),
        //                        after = Normalize(p.CurrentValue)
        //                    });

        //            if (diffs.Count > 0)
        //            {
        //                description = JsonSerializer.Serialize(diffs);
        //            }
        //        }

        //        logs.Add(new AuditLog
        //        {
        //            Id = Guid.NewGuid(),
        //            UserId = userId == Guid.Empty ? SystemUser() : userId,
        //            Action = action,
        //            TableName = tableName,
        //            RecordId = recordId,
        //            Description = description,
        //            IPAddress = ip,
        //            CreatedAt = DateTime.UtcNow
        //        });
        //    }

        //    if (logs.Count > 0)
        //        ctx.AuditLogs.AddRange(logs);

        //    return await base.SavingChangesAsync(eventData, result, cancellationToken);
        //}

        private static readonly Guid SystemUserId = new("95149717-529B-499C-82D2-1CC47D0B01C9");


        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
        {
            var http = _http.HttpContext;
            if (_scope.Suppress)
                return new(result);

            if (eventData.Context is not AppDbContext ctx)
                return base.SavingChangesAsync(eventData, result, cancellationToken);

            Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var uidRaw);

            // CÁCH 1: nếu AuditLog.UserId là Guid?
            //Guid? actorId = uidRaw != Guid.Empty ? uidRaw : null;

            // CÁCH 2: nếu AuditLog.UserId là Guid (non-null)
             var actorId = uidRaw != Guid.Empty ? uidRaw : SystemUserId; // SystemUserId phải tồn tại trong Users

            var ip = http.Connection.RemoteIpAddress?.ToString();
            var logs = new List<AuditLog>();

            var entries = ctx.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity is not AuditLog);

            foreach (var entry in entries)
            {
                var tableName = entry.Metadata.GetTableName();
                var recordId = GetPrimaryKeyValue(entry); // string?
                var action = entry.State switch
                {
                    EntityState.Added => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => "UNKNOWN"
                };

                bool IsSensitive(string n) => n is "PasswordHash" or "RefreshToken" or "RefreshTokenExpire";
                string? description = null;

                if (entry.State == EntityState.Modified)
                {
                    var diffs = entry.Properties
                        .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue) && !IsSensitive(p.Metadata.Name))
                        .ToDictionary(p => p.Metadata.Name, p => new { before = Normalize(p.OriginalValue), after = Normalize(p.CurrentValue) });
                    if (diffs.Count > 0) description = JsonSerializer.Serialize(diffs);
                }
                else if (entry.State == EntityState.Added)
                {
                    var snapshot = entry.Properties
                        .Where(p => !IsSensitive(p.Metadata.Name))
                        .ToDictionary(p => p.Metadata.Name, p => Normalize(p.CurrentValue));
                    description = JsonSerializer.Serialize(new { after = snapshot });
                }
                else if (entry.State == EntityState.Deleted)
                {
                    var snapshot = entry.Properties
                        .Where(p => !IsSensitive(p.Metadata.Name))
                        .ToDictionary(p => p.Metadata.Name, p => Normalize(p.OriginalValue));
                    description = JsonSerializer.Serialize(new { before = snapshot });
                }

                logs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = actorId,               // <-- hợp lệ theo cách bạn chọn ở trên
                    Action = action,
                    TableName = tableName,
                    RecordId = recordId,
                    Description = description,
                    IPAddress = ip,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (logs.Count > 0)
                ctx.AuditLogs.AddRange(logs);

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            // Nếu cần hậu xử lý sau khi lưu, thêm ở đây
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }


        private static object? Normalize(object? val)
        {
            // Chuẩn hóa giá trị để log dễ đọc (DateOnly/TimeOnly/decimal…)
            return val switch
            {
                DateOnly d => d.ToString("yyyy-MM-dd"),
                TimeOnly t => t.ToString("HH:mm:ss"),
                DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                _ => val
            };
        }

        private static string? GetPrimaryKeyValue(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key is null || key.Properties.Count == 0) return null;
            var values = key.Properties.Select(p =>
            {
                var propEntry = entry.Property(p.Name);
                // khi DELETE, CurrentValue có thể null → lấy OriginalValue
                return propEntry.CurrentValue ?? propEntry.OriginalValue;
            });
            return string.Join("|", values);
        }

        private static Guid SystemUser() => Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
