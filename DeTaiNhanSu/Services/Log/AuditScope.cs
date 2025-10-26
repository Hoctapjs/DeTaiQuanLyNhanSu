namespace DeTaiNhanSu.Services.Log
{
    public class AuditScope : IAuditScope
    {
        public bool Suppress { get; set; }
        public Guid? OverrideUserId { get; set; }
    }
}
