using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Options;

namespace DeTaiNhanSu.Services.Email
{
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _opt;

        public SmtpEmailSender(IOptions<SmtpOptions> opt)
        {
            _opt = opt.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email is required.", nameof(toEmail));

            if (string.IsNullOrWhiteSpace(_opt.FromEmail))
                throw new InvalidOperationException("SMTP FromEmail is not configured.");

            using var client = new SmtpClient(_opt.Host, _opt.Port)
            {
                EnableSsl = _opt.EnableSsl,
                Credentials = new NetworkCredential(_opt.User, _opt.Pass)
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(_opt.FromEmail, _opt.DisplayName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            msg.To.Add(new MailAddress(toEmail));

            // SmtpClient không hỗ trợ CancellationToken trực tiếp
            await client.SendMailAsync(msg);
        }
    }
}
