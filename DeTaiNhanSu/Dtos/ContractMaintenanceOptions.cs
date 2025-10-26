namespace DeTaiNhanSu.Dtos
{
    public class ContractMaintenanceOptions
    {
        public int PeriodMinutes { get; set; } = 15;
        public int NotifyWithinDays { get; set; } = 30;
        public string? RunStatusAt { get; set; } // "HH:mm" UTC
        public string? RunMailAt { get; set; }   // "HH:mm" UTC
        public string HrmUrl { get; set; } = "https://google.com";
        public string HelpEmail { get; set; } = "support@example.com";
        public string CompanyName { get; set; } = "Your Company";
        public string CompanyAddress { get; set; } = "Address";
        public bool UseSqlDistributedLock { get; set; } = false;
    }
}
