using DeTaiNhanSu.DbContextProject;
using System.Data.Common;
using System.Data;
using System.Text;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Email;

namespace DeTaiNhanSu.Services.ContractMaintenance
{
    public class ContractMaintenanceWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContractMaintenanceWorker> _logger;
        private readonly ContractMaintenanceOptions _opt;

        private readonly TimeSpan _period;
        private DateOnly _lastStatusRun = default;
        private DateOnly _lastMailRun = default;
        private readonly SemaphoreSlim _loopGate = new(1, 1);

        public ContractMaintenanceWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ContractMaintenanceWorker> logger,
        IOptions<ContractMaintenanceOptions> opt)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _opt = opt.Value;
            _period = TimeSpan.FromMinutes(Math.Max(1, _opt.PeriodMinutes));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(_period);
            _logger.LogInformation("ContractMaintenanceWorker started. Period={Period}", _period);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // no-overlap trong 1 instance
                if (!await _loopGate.WaitAsync(0, stoppingToken)) continue;
                try
                {
                    var nowUtc = DateTime.UtcNow;
                    var today = DateOnly.FromDateTime(nowUtc);

                    // Lịch chạy theo giờ cấu hình (UTC). Nếu không cấu hình sẽ “1 lần/ngày” ở lần đầu tick.
                    bool shouldRunStatus = _lastStatusRun != today && IsTimePassed(_opt.RunStatusAt, nowUtc);
                    bool shouldRunMail = _lastMailRun != today && IsTimePassed(_opt.RunMailAt, nowUtc);

                    if (shouldRunStatus)
                    {
                        if (await TryWithDistributedLock("ContractStatus", RunUpdateStatusesAsync, stoppingToken))
                            _lastStatusRun = today;
                    }

                    if (shouldRunMail)
                    {
                        if (await TryWithDistributedLock("ContractExpiryMail", ct => RunSendExpiringEmailsAsync(_opt.NotifyWithinDays, ct), stoppingToken))
                            _lastMailRun = today;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ContractMaintenanceWorker loop error");
                }
                finally
                {
                    _loopGate.Release();
                }
            }
        }

        private static bool IsTimePassed(string? hhmm, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(hhmm)) return true; // chạy ngay lần đầu trong ngày
            if (!TimeOnly.TryParse(hhmm, out var time)) return true;
            var scheduled = nowUtc.Date.AddHours(time.Hour).AddMinutes(time.Minute);
            return nowUtc >= scheduled;
        }

        /// <summary>
        /// Khoá phân tán qua SQL (sp_getapplock) để tránh 2 instance chạy trùng job cùng lúc (nếu scale out).
        /// </summary>
        private async Task<bool> TryWithDistributedLock(string name, Func<CancellationToken, Task> work, CancellationToken ct)
        {
            if (!_opt.UseSqlDistributedLock)
            {
                await work(ct);
                return true;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conn = db.Database.GetDbConnection();
            await using var _ = await EnsureOpenAsync(conn, ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
DECLARE @res int;
EXEC @res = sp_getapplock
    @Resource = @name,
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = 5000;  -- 5s
SELECT @res;";
            var p = cmd.CreateParameter();
            p.ParameterName = "@name";
            p.Value = $"ContractWorker-{name}";
            cmd.Parameters.Add(p);

            var result = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            if (result < 0)
            {
                // 0/1 là thành công. Âm là lỗi/timeout.
                _logger.LogWarning("Skip job {Job} due to lock not acquired. res={Result}", name, result);
                return false;
            }

            try { await work(ct); return true; }
            finally
            {
                await using var rel = conn.CreateCommand();
                rel.CommandText = "EXEC sp_releaseapplock @Resource=@name, @LockOwner='Session';";
                var p2 = rel.CreateParameter(); p2.ParameterName = "@name"; p2.Value = $"ContractWorker-{name}";
                rel.Parameters.Add(p2);
                await rel.ExecuteNonQueryAsync(ct);
            }
        }

        private async Task RunUpdateStatusesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ContractMaintenanceWorker>>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // set active
            var active = await db.Contracts
                .Where(c => c.Status != ContractStatus.terminated &&
                            c.StartDate <= today &&
                            (c.EndDate == null || c.EndDate >= today) &&
                            c.Status != ContractStatus.active)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContractStatus.active), ct);

            // set terminated
            var terminated = await db.Contracts
                .Where(c => c.Status != ContractStatus.terminated &&
                            c.EndDate != null && c.EndDate < today)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContractStatus.terminated), ct);

            logger.LogInformation("Statuses updated: active={Active}, terminated={Terminated}", active, terminated);
        }

        private async Task RunSendExpiringEmailsAsync(int withinDays, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ContractMaintenanceWorker>>();
            var opt = _opt;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var until = today.AddDays(withinDays);

            var items = await db.Contracts.AsNoTracking()
                .Include(c => c.Employee)
                .Where(c => c.Status != ContractStatus.terminated &&
                            c.EndDate != null &&
                            c.EndDate >= today &&
                            c.EndDate <= until &&
                            c.Employee.Email != null && c.Employee.Email != "")
                .Select(c => new
                {
                    c.Id,
                    c.ContractNumber,
                    c.StartDate,
                    c.EndDate,
                    EmpName = c.Employee.FullName,
                    EmpEmail = c.Employee.Email!
                })
                .ToListAsync(ct);

            if (items.Count == 0) { logger.LogInformation("No expiring contracts."); return; }

            // Dedup theo ngày nếu có cột
            List<Guid> pendingIds;
            if (db.Model.FindEntityType(typeof(Contract))!.FindProperty(nameof(Contract.LastExpiryNotifyDate)) != null)
            {
                var ids = items.Select(i => i.Id).ToList();
                var already = await db.Contracts
                    .Where(c => ids.Contains(c.Id) && c.LastExpiryNotifyDate == today)
                    .Select(c => c.Id)
                    .ToListAsync(ct);
                pendingIds = ids.Except(already).ToList();
                items = items.Where(i => pendingIds.Contains(i.Id)).ToList();
            }

            foreach (var i in items)
            {
                var subject = $"[HRM] Hợp đồng sắp hết hạn: {i.ContractNumber}";
                var body = BuildExpiryEmailHtml(
                    opt.HrmUrl, opt.HelpEmail, opt.CompanyName, opt.CompanyAddress,
                    i.ContractNumber, i.EmpName, i.StartDate, i.EndDate!.Value);

                try { await email.SendAsync(i.EmpEmail, subject, body, ct); }
                catch (Exception ex) { logger.LogWarning(ex, "Send expiring mail failed: {Contract}", i.ContractNumber); }
            }

            // Ghi dấu đã gửi
            if (items.Count > 0 && db.Model.FindEntityType(typeof(Contract))!.FindProperty(nameof(Contract.LastExpiryNotifyDate)) != null)
            {
                var ids = items.Select(x => x.Id).ToList();
                await db.Contracts.Where(c => ids.Contains(c.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastExpiryNotifyDate, today), ct);
            }

            logger.LogInformation("Sent expiring emails: {Count}", items.Count);
        }

        private static async Task<IAsyncDisposable> EnsureOpenAsync(DbConnection conn, CancellationToken ct)
        {
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
            return new AsyncDisposable(conn);
        }

        private sealed class AsyncDisposable : IAsyncDisposable
        {
            private readonly DbConnection _c; public AsyncDisposable(DbConnection c) => _c = c;
            public ValueTask DisposeAsync() { if (_c.State == ConnectionState.Open) _c.Close(); return ValueTask.CompletedTask; }
        }

        private static string BuildExpiryEmailHtml(
            string hrmUrl, string helpEmail, string companyName, string companyAddress,
            string contractNumber, string employeeName, DateOnly startDate, DateOnly endDate)
        {
            string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);
            var sb = new StringBuilder();
            sb.Append($@"<!doctype html><html lang='vi'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Hợp đồng sắp hết hạn</title></head>
<body style='margin:0;padding:0;background:#f5f7fa;'>
<div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
Hợp đồng {Enc(contractNumber)} sắp hết hạn vào {endDate:yyyy-MM-dd}.
</div>
<table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
<tr><td align='center' style='padding:24px 12px;'>
  <table role='presentation' width='600' cellspacing='0' cellpadding='0'
         style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
    <tr><td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
      <h1 style='margin:0;font-size:20px;'>Thông báo hợp đồng sắp hết hạn</h1>
      <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Mã hợp đồng: {Enc(contractNumber)}</p>
    </td></tr>
    <tr><td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
      <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{Enc(employeeName)}</b>,</p>
      <p style='margin:0 0 16px;font-size:15px;'>Hợp đồng của bạn dự kiến hết hạn vào <b>{endDate:yyyy-MM-dd}</b>.</p>
      <table role='presentation' width='100%' cellspacing='0' cellpadding='0'
             style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
        <tr><td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>Chi tiết</td></tr>
        <tr><td style='padding:12px 16px;font-size:14px;line-height:1.7;'>
          <div><b>Số HĐ:</b> {Enc(contractNumber)}</div>
          <div><b>Ngày hiệu lực:</b> {startDate:yyyy-MM-dd}</div>
          <div><b>Ngày hết hạn:</b> {endDate:yyyy-MM-dd}</div>
        </td></tr>
      </table>
      <div style='margin:12px 0 0;'>
        <a href='{hrmUrl}' style='background:#2563eb;text-decoration:none;color:#fff;padding:10px 16px;border-radius:8px;display:inline-block;font-weight:600;'>Xem trên HRM</a>
      </div>
      <p style='margin:16px 0 0;font-size:13px;color:#334155;'>Cần hỗ trợ? Liên hệ <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a>.</p>
    </td></tr>
    <tr><td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;'>
      <div>{Enc(companyName)} • {Enc(companyAddress)}</div>
      <div style='margin-top:4px;'>Email hỗ trợ: <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a></div>
    </td></tr>
  </table>
</td></tr>
</table>
</body></html>");
            return sb.ToString();
        }
    }
}
