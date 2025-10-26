using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace DeTaiNhanSu.Services.Email
{
    public sealed class MailKitEmailSender : IEmailSender
    {
        private readonly SmtpOptions _opt;
        public MailKitEmailSender(IOptions<SmtpOptions> opt) => _opt = opt.Value;

        public async Task SendAsync(string to, string subject, string html, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_opt.FromEmail)) throw new InvalidOperationException("FromEmail is required.");
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("Recipient email is required.", nameof(to));

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_opt.DisplayName ?? "HRM System", _opt.FromEmail));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject ?? string.Empty;
            msg.Body = new BodyBuilder { HtmlBody = html ?? string.Empty }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_opt.Host, _opt.Port, SecureSocketOptions.SslOnConnect, ct); // 465 implicit SSL
            await client.AuthenticateAsync(_opt.User, _opt.Pass, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}
