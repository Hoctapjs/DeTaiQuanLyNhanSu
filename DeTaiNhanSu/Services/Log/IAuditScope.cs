namespace DeTaiNhanSu.Services.Log
{
    public interface IAuditScope
    {
        bool Suppress { get; set; }
        Guid? OverrideUserId { get; set; }
    }
}
