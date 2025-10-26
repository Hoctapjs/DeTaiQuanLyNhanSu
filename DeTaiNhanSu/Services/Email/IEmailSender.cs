namespace DeTaiNhanSu.Services.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string email, string subject, string htmlBody, CancellationToken ct);
    }
}
