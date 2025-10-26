namespace DeTaiNhanSu.Services.Email
{
    public sealed class SmtpOptions
    {
        public string Host { get; set; } = default!;
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = default!;
        public string Pass { get; set; } = default!;
        public string FromEmail { get; set; } = default!;
        public string DisplayName { get; set; } = "HRM System";
    }
}
